// Variations Auditor - service abstractions. All bind to REAL EPiServer CMS 13 types.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EPiServer.Core;
using EPiServer.Security;
using PiNo.Labs.VariationsAuditor.Models;
namespace PiNo.Labs.VariationsAuditor.Services
{
    public interface IContentVariationService
    {
        Task<PagedResult<VariationAuditDto>> ListVariationsAsync(VariationQuery query, CancellationToken cancellationToken = default);
        DivergenceReport GetDivergence(VariantIdentity id);
        BulkOperationResult ExecAction(BulkActionCommand cmd);
        BulkPreviewResult PreviewAction(BulkActionCommand cmd, string editor);
        Task<IReadOnlyList<VariationAuditDto>> GetAuditUniverseAsync(CancellationToken cancellationToken = default);
        SyncPreview PreviewSync(SyncFromDefaultCommand cmd);
        SyncResult ExecSync(SyncFromDefaultCommand cmd);
    }
    // Site-wide discovery of content that HAS variations (no in-process API satisfies this). Backed by Graph.
    public interface IVariationDiscoveryService
    {
        Task<PagedResult<VariantIdentity>> DiscoverVariantsAsync(VariationQuery query, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<VariantIdentity>> DiscoverAllAsync(CancellationToken cancellationToken = default);
    }
    public enum GraphStatus
    {
        Deliverable = 0,
        IndexGap = 1,            // Orphan: known to CMS but missing from the Graph index
        GraphQL_Error = 2,
        Unpublished_Excluded = 3,
    }
    public interface IGraphDeliverabilityService
    {
        Task<GraphStatus> VerifyDeliverabilityAsync(VariantIdentity identity, CancellationToken cancellationToken = default);
        Task<IReadOnlyDictionary<VariantIdentity, GraphStatus>> VerifyDeliverabilityBatchAsync(
            IReadOnlyCollection<VariantIdentity> identities, CancellationToken cancellationToken = default);
    }
    public interface IDivergenceEngine
    {
        DivergenceReport BuildReport(VariantIdentity id);
        IReadOnlyList<string> GetStaleProperties(VariantIdentity id);
    }
    public interface ICurrentEditorAccessor
    {
        string GetCurrentEditor();
        bool HasAccess(ContentReference contentLink, AccessLevel access);
    }

    // Builds a CMS 13 editor deep-link for a variant identity. Single place that knows the URL scheme.
    public interface IVariationEditUrlResolver
    {
        string GetEditUrl(VariantIdentity identity);
    }

    // Resolves a variation key to its human-readable audience (visitor group).
    // ⚠ verify-with-docs: the supported CMS 13 API mapping a variationKey to its audience is not yet confirmed;
    // VisitorGroupAudienceResolver does a best-effort match and degrades gracefully (humanized key).
    public interface IVariationAudienceResolver
    {
        AudienceInfo Resolve(string? variationKey);
    }
}