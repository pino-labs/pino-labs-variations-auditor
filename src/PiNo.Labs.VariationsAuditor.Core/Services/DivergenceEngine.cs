using System.Globalization;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using PiNo.Labs.VariationsAuditor.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
namespace PiNo.Labs.VariationsAuditor.Services
{
    // CMS 13 exposes no branched-from/origin-version pointer, so the baseline is resolved by timestamp
    // heuristic: the master version whose Saved date is latest at or just before the variant's Saved date.
    public sealed class DivergenceEngine : IDivergenceEngine
    {
        private const int MaxMasterVersionsScanned = 500;
        private const string BaselineStrategy =
            "Baseline resolved from version history (nearest preceding master version by save date).";
        private static readonly TimeSpan ReportCacheTtl = TimeSpan.FromSeconds(60);
        private readonly IContentVersionRepository _versions;
        private readonly IContentLoader _contentLoader;
        private readonly IMemoryCache _cache;
        private readonly ILogger<DivergenceEngine> _logger;
        public DivergenceEngine(
            IContentVersionRepository versions,
            IContentLoader contentLoader,
            IMemoryCache cache,
            ILogger<DivergenceEngine> logger)
        {
            _versions = versions;
            _contentLoader = contentLoader;
            _cache = cache;
            _logger = logger;
        }
        public DivergenceReport BuildReport(VariantIdentity id)
        {
            var variantVersion = _versions.Load(id.ContentLink);
            if (variantVersion == null)
            {
                return new DivergenceReport { Identity = id, BaselineResolutionStrategy = "Variant version not found." };
            }
            // The cache key folds in BOTH the variant's and the published master's Saved ticks, so a master
            // republished after the variant's last save still invalidates a stale report.
            var masterLink = new ContentReference(id.ContentId);
            var masterPublished = _versions.LoadPublished(masterLink, id.LanguageBranch) ?? _versions.LoadPublished(masterLink);
            var masterTick = masterPublished?.Saved.Ticks ?? 0L;
            var cacheKey = $"VariationsAuditor:Divergence:{id.ContentId}:{id.WorkId}:{id.LanguageBranch}:{id.VariationKey}:{variantVersion.Saved.Ticks}:{masterTick}";
            if (_cache.TryGetValue(cacheKey, out DivergenceReport? cached) && cached != null)
            {
                return cached;
            }
            var report = BuildReportCore(id, variantVersion, masterLink, masterPublished);
            _cache.Set(cacheKey, report, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ReportCacheTtl,
                Size = 1,
            });
            return report;
        }
        private DivergenceReport BuildReportCore(VariantIdentity id, ContentVersion variantVersion, ContentReference masterLink, ContentVersion? masterPublished)
        {
            var baseline = ResolveBaseline(masterLink, id.LanguageBranch, variantVersion.Saved) ?? masterPublished;
            var variantContent = TryLoad(variantVersion.ContentLink);
            var masterContent = masterPublished != null ? TryLoad(masterPublished.ContentLink) : null;
            var baselineContent = baseline != null ? TryLoad(baseline.ContentLink) : null;
            var masterScalars = GetScalars(masterContent);
            var variantScalars = GetScalars(variantContent);
            var baselineScalars = GetScalars(baselineContent);
            var properties = new List<PropertyOverride>();
            foreach (var key in masterScalars.Keys.Union(variantScalars.Keys))
            {
                var mc = masterScalars.GetValueOrDefault(key);
                var vv = variantScalars.GetValueOrDefault(key);
                var bv = baselineScalars.GetValueOrDefault(key);
                var overrides = !Eq(vv, mc);
                var masterMoved = !Eq(mc, bv);
                properties.Add(new PropertyOverride
                {
                    PropertyName = key,
                    MasterCurrentValue = mc,
                    VariantValue = vv,
                    IsStale = masterMoved && overrides,
                });
            }
            var masterAreas = GetContentAreas(masterContent);
            var variantAreas = GetContentAreas(variantContent);
            var diffs = masterAreas.Keys.Union(variantAreas.Keys)
                .Select(prop => BuildAreaDiff(prop,
                    masterAreas.GetValueOrDefault(prop) ?? new List<int>(),
                    variantAreas.GetValueOrDefault(prop) ?? new List<int>()))
                .Where(d => d.HasStructuralChange)
                .ToList();
            return new DivergenceReport
            {
                Identity = id,
                Properties = properties,
                ContentAreaDiffs = diffs,
                BaselineResolutionStrategy = BaselineStrategy,
            };
        }
        public IReadOnlyList<string> GetStaleProperties(VariantIdentity id)
            => BuildReport(id).Properties.Where(p => p.IsStale).Select(p => p.PropertyName).ToList();
        private ContentVersion? ResolveBaseline(ContentReference masterLink, string language, DateTime variantSaved)
        {
            try
            {
                var filter = new VersionFilter
                {
                    ContentLink = masterLink,
                    Languages = new[] { CultureInfo.GetCultureInfo(language) },
                    IncludeTotalCount = true,
                };
                var versions = _versions.List(filter, 0, MaxMasterVersionsScanned, out _);
                return versions
                    .Where(v => v.Saved <= variantSaved)
                    .OrderByDescending(v => v.Saved)
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Baseline resolution failed for {MasterLink} ({Language}).", masterLink, language);
                return null;
            }
        }
        private IContent? TryLoad(ContentReference link)
        {
            try { return _contentLoader.Get<IContent>(link); }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not load content {Link}.", link);
                return null;
            }
        }
        private static Dictionary<string, string?> GetScalars(IContent? content)
        {
            var map = new Dictionary<string, string?>(StringComparer.Ordinal);
            if (content == null)
            {
                return map;
            }
            foreach (var prop in content.Property)
            {
                if (prop.IsMetaData || prop.Value is ContentArea)
                {
                    continue;
                }
                map[prop.Name] = prop.Value?.ToString();
            }
            return map;
        }
        private static Dictionary<string, List<int>> GetContentAreas(IContent? content)
        {
            var map = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            if (content == null)
            {
                return map;
            }
            foreach (var prop in content.Property)
            {
                if (prop.Value is ContentArea area)
                {
                    map[prop.Name] = (area.Items ?? new List<ContentAreaItem>())
                        .Where(i => i?.ContentLink != null)
                        .Select(i => i.ContentLink.ID)
                        .ToList();
                }
            }
            return map;
        }
        private static ContentAreaDiff BuildAreaDiff(string property, List<int> masterIds, List<int> variantIds)
        {
            var masterSet = masterIds.ToHashSet();
            var variantSet = variantIds.ToHashSet();
            return new ContentAreaDiff
            {
                PropertyName = property,
                AddedBlockIds = variantSet.Except(masterSet).OrderBy(x => x).ToList(),
                RemovedBlockIds = masterSet.Except(variantSet).OrderBy(x => x).ToList(),
            };
        }
        private static bool Eq(string? a, string? b)
            => string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal);
    }
}