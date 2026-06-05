using PiNo.Labs.VariationsAuditor.Models;
using PiNo.Labs.VariationsAuditor.Security;
using PiNo.Labs.VariationsAuditor.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace PiNo.Labs.VariationsAuditor.Controllers
{
    // REST surface for the Variations Auditor shell module. Runs same-origin inside the authenticated CMS
    // shell; access is gated by the CMS-integrated policy (see VariationsAuditorAuthorization).
    [Authorize(Policy = VariationsAuditorAuthorization.PolicyName)]
    [ApiController]
    [Route("api/auditor")]
    public sealed class VariationsAuditorController : ControllerBase
    {
        private readonly IContentVariationService _service;
        private readonly ICurrentEditorAccessor _editor;
        private readonly IDriftMatrixService _driftMatrix;
        public VariationsAuditorController(
            IContentVariationService service,
            ICurrentEditorAccessor editor,
            IDriftMatrixService driftMatrix)
        {
            _service = service;
            _editor = editor;
            _driftMatrix = driftMatrix;
        }
        [HttpPost("list")]
        public async Task<ActionResult<PagedResult<VariationAuditDto>>> List([FromBody] VariationQuery query, CancellationToken cancellationToken)
            => Ok(await _service.ListVariationsAsync(query, cancellationToken));
        [HttpPost("divergence")]
        public ActionResult<DivergenceReport> Divergence([FromBody] VariantIdentity id)
            => Ok(_service.GetDivergence(id));
        [HttpPost("bulk-preview")]
        public ActionResult<BulkPreviewResult> BulkPreview([FromBody] BulkActionCommand cmd)
            => Ok(_service.PreviewAction(cmd, _editor.GetCurrentEditor()));
        [HttpPost("bulk-exec")]
        [ValidateAntiForgeryToken]
        public ActionResult<BulkOperationResult> BulkExec([FromBody] BulkActionCommand cmd)
            => Ok(_service.ExecAction(cmd));

        [HttpPost("drift-matrix")]
        public async Task<ActionResult<DriftMatrix>> DriftMatrix([FromBody] DriftMatrixQuery query, CancellationToken cancellationToken)
        {
            var universe = await _service.GetAuditUniverseAsync(cancellationToken);
            var forContent = universe.Where(v => v.Identity.ContentId == query.ContentId).ToList();
            return Ok(_driftMatrix.Build(query.ContentId, forContent));
        }

        // "Sync from default": mandatory dry-run, then apply.
        [HttpPost("sync-preview")]
        public ActionResult<SyncPreview> SyncPreview([FromBody] SyncFromDefaultCommand cmd)
            => Ok(_service.PreviewSync(cmd));
        [HttpPost("sync-exec")]
        [ValidateAntiForgeryToken]
        public ActionResult<SyncResult> SyncExec([FromBody] SyncFromDefaultCommand cmd)
            => Ok(_service.ExecSync(cmd));
    }
}