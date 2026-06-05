// Variations Auditor - application models, bound to the EPiServer CMS 13 types.

using System.Text.Json.Serialization;
using EPiServer.Core;

namespace PiNo.Labs.VariationsAuditor.Models
{
    // Canonical identity of a variant: no Guid exists, identity is the tuple
    // (contentLink, languageBranch, variationKey). Transported as primitives for robust JSON.
    public sealed record VariantIdentity(int ContentId, int WorkId, string LanguageBranch, string? VariationKey)
    {
        [JsonIgnore]
        public ContentReference ContentLink => new(ContentId, WorkId);

        public static VariantIdentity From(ContentReference link, string languageBranch, string? variationKey)
            => new(link.ID, link.WorkID, languageBranch, variationKey);
    }

    public sealed class PropertyOverride
    {
        public string PropertyName { get; set; } = string.Empty;
        public string? MasterCurrentValue { get; set; }
        public string? VariantValue { get; set; }
        public bool IsStale { get; set; }
    }

    // The human-readable audience a variant targets, resolved from a variationKey. Editors think in audiences.
    public sealed class AudienceInfo
    {
        public string? VariationKey { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? VisitorGroupId { get; set; }
        public int? EstimatedSize { get; set; }
        public bool IsResolved { get; set; }
    }

    public sealed class VariationQuery
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Term { get; set; }

        // Smart filters. All optional; null/empty means "no constraint". Applied server-side before paging.
        public string? Language { get; set; }
        public List<VersionStatus>? Statuses { get; set; }
        public List<DivergenceState>? DivergenceStates { get; set; }
        public bool NeedsAttentionOnly { get; set; }

        [JsonIgnore] public int StartIndex => (Math.Max(Page, 1) - 1) * Math.Max(PageSize, 1);
        [JsonIgnore] public int MaxRows => Math.Max(PageSize, 1);
    }

    public sealed class PagedResult<T>
    {
        public IEnumerable<T> Items { get; set; } = Array.Empty<T>();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}

