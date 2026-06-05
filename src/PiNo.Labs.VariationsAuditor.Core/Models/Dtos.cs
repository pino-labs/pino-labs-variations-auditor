// Variations Auditor - DTOs, commands and results. Bound to EPiServer.Core.VersionStatus.
using EPiServer.Core;
namespace PiNo.Labs.VariationsAuditor.Models
{
    public enum DivergenceState
    {
        Clear = 0,
        OutdatedOverride = 1, // at least one property IsStale
        Orphan = 2,           // not deliverable / not indexed in Optimizely Graph
    }
    public sealed class VariationAuditDto
    {
        public VariantIdentity Identity { get; set; } = default!;
        public string ContentName { get; set; } = string.Empty;
        public string? VariationKey { get; set; }
        public string LanguageBranch { get; set; } = string.Empty;
        public VersionStatus Status { get; set; }
        public int OverriddenPropertiesCount { get; set; }
        public DivergenceState DivergenceState { get; set; }
        public bool HasStaleProperties { get; set; }
        public DateTime VariantSaved { get; set; }
        public string SavedBy { get; set; } = string.Empty;
        public string EditUrl { get; set; } = string.Empty;
        public AudienceInfo Audience { get; set; } = new();
    }
    public sealed class DivergenceReport
    {
        public VariantIdentity Identity { get; set; } = default!;
        public IReadOnlyList<PropertyOverride> Properties { get; set; } = Array.Empty<PropertyOverride>();
        public IReadOnlyList<ContentAreaDiff> ContentAreaDiffs { get; set; } = Array.Empty<ContentAreaDiff>();
        public string BaselineResolutionStrategy { get; set; } = string.Empty;
        public string EditUrl { get; set; } = string.Empty;
    }
    public sealed class ContentAreaDiff
    {
        public string PropertyName { get; set; } = string.Empty;
        public IReadOnlyList<int> AddedBlockIds { get; set; } = Array.Empty<int>();
        public IReadOnlyList<int> RemovedBlockIds { get; set; } = Array.Empty<int>();
        public bool HasStructuralChange => AddedBlockIds.Count > 0 || RemovedBlockIds.Count > 0;
    }
    public enum BulkActionType
    {
        Promote = 0,
        Unpublish = 1,
        Delete = 2,
    }
    // Merge is the only supported promote mode (the platform's "Copy changes to Original" delta semantics).
    public enum PromoteMode
    {
        Merge = 0,
    }
    public sealed class BulkActionCommand
    {
        public BulkActionType Action { get; set; }
        public IReadOnlyList<VariantIdentity> Targets { get; set; } = Array.Empty<VariantIdentity>();
        public bool ForceOverwriteStale { get; set; }
        public PromoteMode PromoteMode { get; set; } = PromoteMode.Merge;
    }
    public enum BulkItemOutcome
    {
        Success = 0,
        BlockedByStatus = 1,
        BlockedByLock = 2,
        BlockedByStaleConflict = 3,
        Failed = 4,
    }
    public sealed class BulkItemResult
    {
        public VariantIdentity Identity { get; set; } = default!;
        public BulkItemOutcome Outcome { get; set; }
        public string Message { get; set; } = string.Empty;
        public IReadOnlyList<string> StaleProperties { get; set; } = Array.Empty<string>();
    }
    public sealed class BulkOperationResult
    {
        public BulkActionType Action { get; set; }
        public string ExecutedBy { get; set; } = string.Empty;
        public DateTime ExecutedUtc { get; set; }
        public IReadOnlyList<BulkItemResult> Results { get; set; } = Array.Empty<BulkItemResult>();
        public int SuccessCount => Results.Count(r => r.Outcome == BulkItemOutcome.Success);
        public int FailureCount => Results.Count(r => r.Outcome != BulkItemOutcome.Success);
        public bool IsPartialSuccess => SuccessCount > 0 && FailureCount > 0;
    }
    public sealed class BulkPreviewResult
    {
        public BulkActionType Action { get; set; }
        public IReadOnlyList<BulkPreviewItem> Items { get; set; } = Array.Empty<BulkPreviewItem>();
        public bool HasCriticalWarnings => Items.Any(i => i.IsCriticalReversionRisk);
    }
    public sealed class BulkPreviewItem
    {
        public VariantIdentity Identity { get; set; } = default!;
        public bool IsCriticalReversionRisk { get; set; }
        public IReadOnlyList<string> StaleProperties { get; set; } = Array.Empty<string>();
        public string? Warning { get; set; }
        public string? BlockingReason { get; set; }
    }
}