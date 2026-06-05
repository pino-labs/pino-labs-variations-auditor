using EPiServer;
using EPiServer.Core;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PiNo.Labs.VariationsAuditor.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Optimizely.Graph.Cms.Query;
namespace PiNo.Labs.VariationsAuditor.Services
{
    // Optimizely Graph deliverability probe: is the published variant indexed/served by Graph? Lets the UI
    // flag orphans (known to CMS, missing from Graph). Probes by content identity, never by variation key.
    public sealed class GraphDeliverabilityService : IGraphDeliverabilityService
    {
        private static readonly TimeSpan IndexCacheTtl = TimeSpan.FromSeconds(60);
        // Bounds how many index probes hit Graph at once so a cold-cache re-list cannot exhaust the HTTP
        // connection pool or trip Graph's shared rate limit (429).
        private const int MaxConcurrentProbes = 8;
        private readonly IGraphContentClient _graph;       // optional - null when AddGraphContentClient() not called
        private readonly IContentVersionRepository _versions;
        private readonly IContentLoader _contentLoader;
        private readonly IMemoryCache _cache;
        private readonly ILogger<GraphDeliverabilityService> _logger;
        public GraphDeliverabilityService(
            IServiceProvider serviceProvider,
            IContentVersionRepository versions,
            IContentLoader contentLoader,
            IMemoryCache cache,
            ILogger<GraphDeliverabilityService> logger)
        {
            _graph = serviceProvider.GetService<IGraphContentClient>();
            _versions = versions;
            _contentLoader = contentLoader;
            _cache = cache;
            _logger = logger;
        }

        public async Task<GraphStatus> VerifyDeliverabilityAsync(VariantIdentity identity, CancellationToken cancellationToken = default)
        {
            try
            {
                var version = _versions.Load(identity.ContentLink);
                if (version == null)
                {
                    return GraphStatus.IndexGap;
                }
                // Graph only serves published content.
                if (version.Status != VersionStatus.Published)
                {
                    return GraphStatus.Unpublished_Excluded;
                }
                if (!_contentLoader.TryGet<IContent>(new ContentReference(identity.ContentId), out var content) || content == null)
                {
                    return GraphStatus.IndexGap;
                }
                if (_graph == null)
                {
                    // Graph unavailable - never mislabel as Orphan; treat as deliverable.
                    return GraphStatus.Deliverable;
                }
                var indexed = await IsIndexedAsync(identity.ContentId, cancellationToken).ConfigureAwait(false);
                return indexed ? GraphStatus.Deliverable : GraphStatus.IndexGap;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Graph deliverability probe failed for {ContentId}.", identity.ContentId);
                return GraphStatus.GraphQL_Error;
            }
        }

        // Resolves a whole page in one pass: the remote index lookup is de-duplicated per ContentId and probes
        // run concurrently, removing the per-row N+1 round-trip.
        public async Task<IReadOnlyDictionary<VariantIdentity, GraphStatus>> VerifyDeliverabilityBatchAsync(
            IReadOnlyCollection<VariantIdentity> identities, CancellationToken cancellationToken = default)
        {
            var statuses = new Dictionary<VariantIdentity, GraphStatus>();
            var toProbe = new HashSet<int>();

            foreach (var identity in identities)
            {
                if (statuses.ContainsKey(identity))
                {
                    continue;
                }
                var version = _versions.Load(identity.ContentLink);
                if (version == null)
                {
                    statuses[identity] = GraphStatus.IndexGap;
                    continue;
                }
                if (version.Status != VersionStatus.Published)
                {
                    statuses[identity] = GraphStatus.Unpublished_Excluded;
                    continue;
                }
                if (!_contentLoader.TryGet<IContent>(new ContentReference(identity.ContentId), out var content) || content == null)
                {
                    statuses[identity] = GraphStatus.IndexGap;
                    continue;
                }
                if (_graph == null)
                {
                    statuses[identity] = GraphStatus.Deliverable;
                    continue;
                }
                toProbe.Add(identity.ContentId);
            }

            // Bounded-concurrency probes. A Graph failure is treated as deliverable (unverified) - never an
            // Orphan, never a 500.
            var probeResults = new Dictionary<int, bool>();
            if (toProbe.Count > 0)
            {
                var gate = new SemaphoreSlim(MaxConcurrentProbes);
                try
                {
                    var tasks = toProbe.Select(async contentId =>
                    {
                        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                        try
                        {
                            return (ContentId: contentId, Indexed: await IsIndexedAsync(contentId, cancellationToken).ConfigureAwait(false));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex,
                                "Graph deliverability probe failed for content {ContentId}; treating as deliverable (not orphan).", contentId);
                            return (ContentId: contentId, Indexed: true);
                        }
                        finally
                        {
                            gate.Release();
                        }
                    }).ToList();
                    foreach (var r in await Task.WhenAll(tasks).ConfigureAwait(false))
                    {
                        probeResults[r.ContentId] = r.Indexed;
                    }
                }
                finally
                {
                    gate.Dispose();
                }
            }

            foreach (var identity in identities)
            {
                if (statuses.ContainsKey(identity))
                {
                    continue;
                }
                statuses[identity] = probeResults.TryGetValue(identity.ContentId, out var indexed) && indexed
                    ? GraphStatus.Deliverable
                    : GraphStatus.IndexGap;
            }
            return statuses;
        }

        // Single remote Graph index lookup for one content item, memoised per ContentId for a short TTL. Probes
        // by exact ContentLink.ID (not full-text) to avoid false orphans. Variation key is not used.
        private async Task<bool> IsIndexedAsync(int contentId, CancellationToken cancellationToken)
        {
            var cacheKey = "VariationsAuditor:Graph:Indexed:" + contentId;
            if (_cache.TryGetValue(cacheKey, out bool cached))
            {
                return cached;
            }
            var result = await _graph!
                .QueryContent<PageData>()
                .Where(c => c.ContentLink.ID == contentId)
                .Limit(1)
                .GetAsContentAsync()
                .ConfigureAwait(false);
            var indexed = result.Any(c => c?.ContentLink != null && c.ContentLink.ID == contentId);
            _cache.Set(cacheKey, indexed, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = IndexCacheTtl,
                Size = 1,
            });
            return indexed;
        }
    }
}