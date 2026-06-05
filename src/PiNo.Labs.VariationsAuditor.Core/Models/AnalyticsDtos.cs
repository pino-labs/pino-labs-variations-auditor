// Variations Auditor - drift-matrix and sync DTOs, bound to the same VariantIdentity tuple as the core models.
namespace PiNo.Labs.VariationsAuditor.Models
{
    public sealed class DriftMatrix
    {
        public int ContentId { get; set; }
        public string ContentName { get; set; } = string.Empty;
        public IReadOnlyList<string> Languages { get; set; } = Array.Empty<string>();
        public IReadOnlyList<DriftColumn> Columns { get; set; } = Array.Empty<DriftColumn>();
        public IReadOnlyList<DriftCell> Cells { get; set; } = Array.Empty<DriftCell>();
    }

    public sealed class DriftColumn
    {
        public string? VariationKey { get; set; }
        public string Audience { get; set; } = string.Empty;
        public bool IsResolvedAudience { get; set; }
    }

    public sealed class DriftMatrixQuery
    {
        public int ContentId { get; set; }
    }

    // One cell at (language × variation). Exists=false ⇒ the combination has no variant.
    public sealed class DriftCell
    {
        public string LanguageBranch { get; set; } = string.Empty;
        public string? VariationKey { get; set; }
        public bool Exists { get; set; }
        public DivergenceState DivergenceState { get; set; }
        public bool HasStaleProperties { get; set; }
        public int OverriddenPropertiesCount { get; set; }
        public VariantIdentity? Identity { get; set; }
        public string EditUrl { get; set; } = string.Empty;
    }

    // "Sync from default" (inverse of Promote): pull the master's current value into a variant, per property.
    public sealed class SyncFromDefaultCommand
    {
        public VariantIdentity Target { get; set; } = default!;
        // Properties to pull from master. Empty ⇒ every stale property.
        public IReadOnlyList<string> PropertyNames { get; set; } = Array.Empty<string>();
    }

    public sealed class SyncPreview
    {
        public VariantIdentity Target { get; set; } = default!;
        public IReadOnlyList<SyncPropertyChange> Changes { get; set; } = Array.Empty<SyncPropertyChange>();
        public string? BlockingReason { get; set; }
        public bool CanApply => BlockingReason == null && Changes.Count > 0;
    }

    public sealed class SyncPropertyChange
    {
        public string PropertyName { get; set; } = string.Empty;
        public string? CurrentVariantValue { get; set; }
        public string? IncomingMasterValue { get; set; }
    }

    public sealed class SyncResult
    {
        public VariantIdentity Target { get; set; } = default!;
        public bool Success { get; set; }
        public IReadOnlyList<string> AppliedProperties { get; set; } = Array.Empty<string>();
        public string Message { get; set; } = string.Empty;
    }
}

