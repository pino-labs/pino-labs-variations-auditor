using EPiServer;
using EPiServer.Core;
using System.Threading;
using System.Threading.Tasks;
using PiNo.Labs.VariationsAuditor.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Optimizely.Graph.Cms.Query;
namespace PiNo.Labs.VariationsAuditor.Services
{
    // Site-wide discovery of content that HAS variations. Enumerates the content universe from Optimizely
    // Graph when available, falling back to an in-process content-tree walk. Per-content variation keys always
    // come from IContentVersionRepository. IGraphContentClient is OPTIONAL (resolved via GetService, may be null).
    public sealed class GraphVariationDiscoveryService : IVariationDiscoveryService
    {
        private const int GraphBatch = 100;
        private const int MaxContentScanned = 2000;
        private const int MaxVersionsPerContent = 200;
        private static readonly TimeSpan DiscoveryCacheTtl = TimeSpan.FromSeconds(60);
        private const string DiscoveryCacheKey = "VariationsAuditor:Discovery:AllVariants";
        private readonly IGraphContentClient _graph;       // optional — null when AddGraphContentClient() not called
        private readonly IContentVersionRepository _versions;
        private readonly IContentLoader _contentLoader;
        private readonly IMemoryCache _cache;
        private readonly ILogger<GraphVariationDiscoveryService> _logger;
        public GraphVariationDiscoveryService(
            IServiceProvider serviceProvider,
            IContentVersionRepository versions,
            IContentLoader contentLoader,
            IMemoryCache cache,
            ILogger<GraphVariationDiscoveryService> logger)
        {
            _graph = serviceProvider.GetService<IGraphContentClient>();
            _versions = versions;
            _contentLoader = contentLoader;
            _cache = cache;
            _logger = logger;
        }
        public async Task<PagedResult<VariantIdentity>> DiscoverVariantsAsync(VariationQuery query, CancellationToken cancellationToken = default)
        {
            var distinct = await GetOrBuildVariantUniverseAsync(cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(query.Term))
            {
                distinct = distinct.Where(i =>
                        (i.VariationKey?.Contains(query.Term!, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        i.LanguageBranch.Contains(query.Term!, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            var page = distinct.Skip(query.StartIndex).Take(query.MaxRows).ToList();
            return new PagedResult<VariantIdentity>
            {
                Items = page,
                TotalCount = distinct.Count,
                CurrentPage = query.Page,
                PageSize = query.PageSize,
            };
        }

        // Full distinct variant universe (unpaged, unfiltered), from the cached enumeration.
        public async Task<IReadOnlyList<VariantIdentity>> DiscoverAllAsync(CancellationToken cancellationToken = default)
            => await GetOrBuildVariantUniverseAsync(cancellationToken).ConfigureAwait(false);

        private async Task<List<VariantIdentity>> GetOrBuildVariantUniverseAsync(CancellationToken cancellationToken)
        {
            if (_cache.TryGetValue(DiscoveryCacheKey, out List<VariantIdentity>? cached) && cached != null)
            {
                return cached;
            }
            var built = await BuildVariantUniverseAsync(cancellationToken).ConfigureAwait(false);
            _cache.Set(DiscoveryCacheKey, built, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = DiscoveryCacheTtl,
                Size = built.Count,
            });
            return built;
        }

        private async Task<List<VariantIdentity>> BuildVariantUniverseAsync(CancellationToken cancellationToken)
        {
            var contentLinks = await EnumerateSiteContentAsync(cancellationToken).ConfigureAwait(false);
            var identities = new List<VariantIdentity>();
            foreach (var link in contentLinks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var v in ListVariationVersions(link))
                {
                    identities.Add(VariantIdentity.From(v.ContentLink, v.LanguageBranch, v.Variation));
                }
            }
            _logger.LogInformation(
                "VariationsAuditor discovery: enumerated {ContentCount} content item(s); " +
                "{VariantCount} content-variation version(s) found (non-empty IVersionable.Variation keys).",
                contentLinks.Count, identities.Count);
            return identities
                .GroupBy(i => (i.ContentId, i.WorkId, i.LanguageBranch, i.VariationKey))
                .Select(g => g.First())
                .ToList();
        }
        // Enumerate the site-wide content universe: Optimizely Graph first, in-process tree walk as fallback.
        private async Task<List<ContentReference>> EnumerateSiteContentAsync(CancellationToken cancellationToken)
        {
            var fromGraph = await EnumerateFromGraphAsync(cancellationToken).ConfigureAwait(false);
            if (fromGraph.Count > 0)
            {
                _logger.LogInformation("VariationsAuditor: enumerated {Count} content item(s) from Optimizely Graph.", fromGraph.Count);
                return Dedupe(fromGraph);
            }

            // Graph returned nothing — fall back to an in-process content-tree walk.
            var fromTree = EnumerateInProcess();
            _logger.LogInformation(
                "VariationsAuditor: Graph returned 0 content; in-process tree walk enumerated {Count} content item(s).",
                fromTree.Count);
            return Dedupe(fromTree);
        }

        private static List<ContentReference> Dedupe(List<ContentReference> links)
            => links.GroupBy(l => l.ID).Select(g => new ContentReference(g.Key)).ToList();

        private async Task<List<ContentReference>> EnumerateFromGraphAsync(CancellationToken cancellationToken)
        {
            var links = new List<ContentReference>();
            if (_graph == null)
            {
                _logger.LogInformation(
                    "VariationsAuditor: IGraphContentClient not registered; using in-process content enumeration.");
                return links;
            }
            try
            {
                var skip = 0;
                while (links.Count < MaxContentScanned)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = await _graph
                        .QueryContent<PageData>()
                        .Skip(skip)
                        .Limit(GraphBatch)
                        .IncludeTotal()
                        .GetAsContentAsync()
                        .ConfigureAwait(false);
                    var batch = result.ToList();
                    if (batch.Count == 0)
                    {
                        break;
                    }
                    links.AddRange(batch.Where(c => c?.ContentLink != null).Select(c => c.ContentLink));
                    skip += GraphBatch;
                    if ((result.Total ?? 0) > 0 && skip >= result.Total)
                    {
                        break;
                    }
                }
                if (links.Count >= MaxContentScanned)
                {
                    _logger.LogWarning(
                        "VariationsAuditor: Graph enumeration hit the {Cap}-item scan cap; discovery may be truncated. " +
                        "Increase MaxContentScanned if the site has more content with variations.", MaxContentScanned);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Optimizely Graph site enumeration failed; falling back to in-process tree walk.");
            }
            return links;
        }

        // Fallback path: walk the real content tree in-process from the system root (no Graph dependency).
        private List<ContentReference> EnumerateInProcess()
        {
            var links = new List<ContentReference>();
            try
            {
                foreach (var descendant in _contentLoader.GetDescendents(ContentReference.RootPage))
                {
                    if (descendant != null && !ContentReference.IsNullOrEmpty(descendant))
                    {
                        links.Add(descendant);
                    }
                    if (links.Count >= MaxContentScanned)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "In-process content-tree enumeration failed; discovery returns empty.");
            }
            return links;
        }
        // A CMS 13 "content variation" is a version with a non-empty IVersionable.Variation key; an empty key
        // is the language baseline / plain localization and is NOT a variation.
        internal static bool IsContentVariation(string? variationKey)
            => !string.IsNullOrEmpty(variationKey);

        private IEnumerable<EPiServer.DataAbstraction.ContentVersion> ListVariationVersions(ContentReference contentLink)
        {
            try
            {
                var filter = new VersionFilter
                {
                    ContentLink = contentLink,
                    IncludeTotalCount = true,
                };
                var versions = _versions.List(filter, 0, MaxVersionsPerContent, out _);

                var variants = versions.Where(v => IsContentVariation(v.Variation));

                // Collapse to one representative (newest) version per (LanguageBranch, Variation) tuple.
                return variants
                    .GroupBy(v => (v.LanguageBranch, v.Variation))
                    .Select(g => g.OrderByDescending(v => v.Saved).First())
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Version listing failed for {ContentLink}.", contentLink);
                return Enumerable.Empty<EPiServer.DataAbstraction.ContentVersion>();
            }
        }
    }
}