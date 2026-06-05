using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EPiServer.Core;
using EPiServer.Security;
using PiNo.Labs.VariationsAuditor.Models;
using PiNo.Labs.VariationsAuditor.Services;

namespace PiNo.Labs.VariationsAuditor.Tests.TestDoubles
{
    // In-memory recording stand-in for IContentVariationService (no mocking-framework dependency).
    internal sealed class FakeContentVariationService : IContentVariationService
    {
        public PagedResult<VariationAuditDto> ListResult { get; set; } = new();
        public DivergenceReport DivergenceResult { get; set; } = new();
        public BulkOperationResult ExecResult { get; set; } = new();
        public BulkPreviewResult PreviewResult { get; set; } = new();
        public IReadOnlyList<VariationAuditDto> Universe { get; set; } = System.Array.Empty<VariationAuditDto>();
        public SyncPreview SyncPreviewResult { get; set; } = new();
        public SyncResult SyncExecResult { get; set; } = new();

        public string? PreviewEditorArgument { get; private set; }
        public VariationQuery? LastQuery { get; private set; }

        public Task<PagedResult<VariationAuditDto>> ListVariationsAsync(VariationQuery query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(ListResult);
        }

        public DivergenceReport GetDivergence(VariantIdentity id) => DivergenceResult;

        public BulkOperationResult ExecAction(BulkActionCommand cmd) => ExecResult;

        public BulkPreviewResult PreviewAction(BulkActionCommand cmd, string editor)
        {
            PreviewEditorArgument = editor;
            return PreviewResult;
        }

        public Task<IReadOnlyList<VariationAuditDto>> GetAuditUniverseAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Universe);

        public SyncPreview PreviewSync(SyncFromDefaultCommand cmd) => SyncPreviewResult;

        public SyncResult ExecSync(SyncFromDefaultCommand cmd) => SyncExecResult;
    }

    internal sealed class FakeCurrentEditorAccessor : ICurrentEditorAccessor
    {
        public string Editor { get; set; } = "editor@acme.com";
        public string GetCurrentEditor() => Editor;
        public bool HasAccess(ContentReference contentLink, AccessLevel access) => true;
    }
}

