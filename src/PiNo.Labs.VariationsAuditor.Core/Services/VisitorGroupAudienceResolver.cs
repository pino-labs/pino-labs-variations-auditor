using System.Globalization;
using EPiServer.Personalization.VisitorGroups;
using PiNo.Labs.VariationsAuditor.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PiNo.Labs.VariationsAuditor.Services
{
    // Resolves a content-variation key to the human-readable audience (visitor group) it targets.
    // ⚠ verify-with-docs: the supported CMS 13 API mapping a variationKey to its audience definition is not yet
    // confirmed. This does a best-effort, defensive match against the host's visitor groups (key == group Id,
    // key == group Name, or an embedded GUID) and always degrades to a humanized key. IVisitorGroupRepository is
    // optional (may be null). The catalogue is cached (short TTL).
    public sealed class VisitorGroupAudienceResolver : IVariationAudienceResolver
    {
        private static readonly TimeSpan CatalogueTtl = TimeSpan.FromSeconds(60);
        private const string CatalogueCacheKey = "VariationsAuditor:Audience:VisitorGroupCatalogue";

        private readonly IVisitorGroupRepository? _visitorGroups; // optional - null when host has no visitor groups
        private readonly IMemoryCache _cache;
        private readonly ILogger<VisitorGroupAudienceResolver> _logger;

        public VisitorGroupAudienceResolver(
            IServiceProvider serviceProvider,
            IMemoryCache cache,
            ILogger<VisitorGroupAudienceResolver> logger)
        {
            _visitorGroups = serviceProvider.GetService<IVisitorGroupRepository>();
            _cache = cache;
            _logger = logger;
        }

        public AudienceInfo Resolve(string? variationKey)
        {
            if (string.IsNullOrWhiteSpace(variationKey))
            {
                return new AudienceInfo { VariationKey = variationKey, DisplayName = string.Empty, IsResolved = false };
            }

            var catalogue = GetCatalogue();
            var group = catalogue.Count > 0 ? TryMatch(catalogue, variationKey) : null;
            if (group != null)
            {
                return new AudienceInfo
                {
                    VariationKey = variationKey,
                    DisplayName = group.Name,
                    VisitorGroupId = group.Id.ToString(),
                    EstimatedSize = null,
                    IsResolved = true,
                };
            }

            return new AudienceInfo
            {
                VariationKey = variationKey,
                DisplayName = Humanize(variationKey),
                IsResolved = false,
            };
        }

        // Catalogue snapshot (Id + Name lookups), built once per TTL window.
        private VisitorGroupCatalogue GetCatalogue()
        {
            if (_cache.TryGetValue(CatalogueCacheKey, out VisitorGroupCatalogue? cached) && cached != null)
            {
                return cached;
            }

            var built = BuildCatalogue();
            _cache.Set(CatalogueCacheKey, built, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CatalogueTtl,
                Size = Math.Max(1, built.Count),
            });
            return built;
        }

        private VisitorGroupCatalogue BuildCatalogue()
        {
            var catalogue = new VisitorGroupCatalogue();
            if (_visitorGroups == null)
            {
                return catalogue;
            }
            try
            {
                foreach (var group in _visitorGroups.List())
                {
                    if (group == null)
                    {
                        continue;
                    }
                    catalogue.Add(group);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VariationsAuditor: visitor-group catalogue build failed; audiences will use key fallback.");
            }
            return catalogue;
        }

        private static VisitorGroup? TryMatch(VisitorGroupCatalogue catalogue, string variationKey)
        {
            var key = variationKey.Trim();

            foreach (var candidate in ExtractGuids(key))
            {
                if (catalogue.ById.TryGetValue(candidate, out var byId))
                {
                    return byId;
                }
            }

            return catalogue.ByName.TryGetValue(key, out var byName) ? byName : null;
        }

        // A variation key may be a bare GUID or a composite (e.g. "vg_{guid}", "{guid}:control"). Pull any GUIDs.
        private static IEnumerable<Guid> ExtractGuids(string key)
        {
            if (Guid.TryParse(key, out var whole))
            {
                yield return whole;
            }
            foreach (var token in key.Split(new[] { '_', '-', ':', '|', '/', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.Length >= 32 && Guid.TryParse(token, out var part))
                {
                    yield return part;
                }
            }
        }

        // Turn "summer_sale-audience" / "ReturningCustomers" into "Summer Sale Audience" / "Returning Customers".
        private static string Humanize(string key)
        {
            var spaced = System.Text.RegularExpressions.Regex
                .Replace(key.Replace('_', ' ').Replace('-', ' '), "(?<=[a-z0-9])(?=[A-Z])", " ")
                .Trim();
            if (spaced.Length == 0)
            {
                return key;
            }
            var words = spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => char.ToUpper(w[0], CultureInfo.InvariantCulture) + w.Substring(1));
            return string.Join(' ', words);
        }

        private sealed class VisitorGroupCatalogue
        {
            public Dictionary<Guid, VisitorGroup> ById { get; } = new();
            public Dictionary<string, VisitorGroup> ByName { get; } =
                new(StringComparer.OrdinalIgnoreCase);

            public int Count => ById.Count;

            public void Add(VisitorGroup group)
            {
                ById[group.Id] = group;
                if (!string.IsNullOrWhiteSpace(group.Name))
                {
                    ByName[group.Name] = group;
                }
            }
        }
    }
}

