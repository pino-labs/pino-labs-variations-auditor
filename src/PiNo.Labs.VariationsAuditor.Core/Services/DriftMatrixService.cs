using PiNo.Labs.VariationsAuditor.Models;

namespace PiNo.Labs.VariationsAuditor.Services
{
    // Language drift matrix: pivots one content item's variants into a languages × variations grid. Pure
    // projection of the already-computed audit DTOs plus audience resolution for the column headers.
    public interface IDriftMatrixService
    {
        DriftMatrix Build(int contentId, IReadOnlyCollection<VariationAuditDto> variantsForContent);
    }

    public sealed class DriftMatrixService : IDriftMatrixService
    {
        public DriftMatrix Build(int contentId, IReadOnlyCollection<VariationAuditDto> variantsForContent)
        {
            var list = (variantsForContent ?? Array.Empty<VariationAuditDto>())
                .Where(v => v.Identity.ContentId == contentId)
                .ToList();

            var contentName = list.FirstOrDefault()?.ContentName ?? $"#{contentId}";

            var languages = list
                .Select(v => v.LanguageBranch)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Columns: distinct variations by key (a null key column is the language/default variant).
            var columns = list
                .GroupBy(v => v.VariationKey, StringComparer.Ordinal)
                .Select(g =>
                {
                    var sample = g.First();
                    return new DriftColumn
                    {
                        VariationKey = g.Key,
                        Audience = string.IsNullOrEmpty(sample.Audience?.DisplayName)
                            ? (g.Key == null ? "Default / language" : g.Key)
                            : sample.Audience!.DisplayName,
                        IsResolvedAudience = sample.Audience?.IsResolved ?? false,
                    };
                })
                .OrderBy(c => c.VariationKey == null ? 0 : 1)   // default column first
                .ThenBy(c => c.Audience, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Dense cell set: every (language × column) pair, Exists=false when no variant occupies it.
            var byKey = list.ToDictionary(
                v => (v.LanguageBranch, v.VariationKey),
                v => v,
                new LangVarComparer());

            var cells = new List<DriftCell>(languages.Count * columns.Count);
            foreach (var lang in languages)
            {
                foreach (var col in columns)
                {
                    if (byKey.TryGetValue((lang, col.VariationKey), out var dto))
                    {
                        cells.Add(new DriftCell
                        {
                            LanguageBranch = lang,
                            VariationKey = col.VariationKey,
                            Exists = true,
                            DivergenceState = dto.DivergenceState,
                            HasStaleProperties = dto.HasStaleProperties,
                            OverriddenPropertiesCount = dto.OverriddenPropertiesCount,
                            Identity = dto.Identity,
                            EditUrl = dto.EditUrl,
                        });
                    }
                    else
                    {
                        cells.Add(new DriftCell
                        {
                            LanguageBranch = lang,
                            VariationKey = col.VariationKey,
                            Exists = false,
                            DivergenceState = DivergenceState.Clear,
                        });
                    }
                }
            }

            return new DriftMatrix
            {
                ContentId = contentId,
                ContentName = contentName,
                Languages = languages,
                Columns = columns,
                Cells = cells,
            };
        }

        private sealed class LangVarComparer : IEqualityComparer<(string Lang, string? Var)>
        {
            public bool Equals((string Lang, string? Var) x, (string Lang, string? Var) y)
                => string.Equals(x.Lang, y.Lang, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(x.Var ?? string.Empty, y.Var ?? string.Empty, StringComparison.Ordinal);

            public int GetHashCode((string Lang, string? Var) obj)
                => HashCode.Combine(
                    obj.Lang.ToLowerInvariant(),
                    obj.Var ?? string.Empty);
        }
    }
}

