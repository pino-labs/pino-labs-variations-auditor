using System.Threading;
using Microsoft.AspNetCore.Mvc;
using PiNo.Labs.VariationsAuditor.Controllers;
using PiNo.Labs.VariationsAuditor.Models;
using PiNo.Labs.VariationsAuditor.Services;
using PiNo.Labs.VariationsAuditor.Tests.TestData;
using PiNo.Labs.VariationsAuditor.Tests.TestDoubles;
using Xunit;

namespace PiNo.Labs.VariationsAuditor.Tests.Controllers
{
    // Verifies the controller delegates to the right service and wraps results as 200 OK.
    public sealed class VariationsAuditorControllerTests
    {
        private readonly FakeContentVariationService _service = new();
        private readonly FakeCurrentEditorAccessor _editor = new();
        private readonly VariationsAuditorController _controller;

        public VariationsAuditorControllerTests()
        {
            _controller = new VariationsAuditorController(
                _service,
                _editor,
                new DriftMatrixService());
        }

        private static T Ok<T>(ActionResult<T> result)
        {
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            return Assert.IsAssignableFrom<T>(ok.Value);
        }

        [Fact]
        public async Task List_returns_the_service_page()
        {
            _service.ListResult = new PagedResult<VariationAuditDto> { TotalCount = 42, CurrentPage = 2 };
            var query = new VariationQuery { Page = 2 };

            var payload = Ok(await _controller.List(query, CancellationToken.None));

            Assert.Equal(42, payload.TotalCount);
            Assert.Same(query, _service.LastQuery);
        }

        [Fact]
        public void Divergence_returns_the_service_report()
        {
            var id = new VariantIdentity(7, 1, "en", "vip");
            _service.DivergenceResult = new DivergenceReport { Identity = id };

            var payload = Ok(_controller.Divergence(id));

            Assert.Same(id, payload.Identity);
        }

        [Fact]
        public void BulkPreview_threads_the_current_editor_into_the_service()
        {
            _editor.Editor = "lead@acme.com";

            _ = _controller.BulkPreview(new BulkActionCommand { Action = BulkActionType.Promote });

            Assert.Equal("lead@acme.com", _service.PreviewEditorArgument);
        }

        [Fact]
        public async Task DriftMatrix_builds_the_grid_for_the_requested_content_only()
        {
            _service.Universe = new[]
            {
                AuditDtoBuilder.For(7, "Hero").Language("en").Variation(null).Build(),
                AuditDtoBuilder.For(7, "Hero").Language("sv").Variation(null).Build(),
                AuditDtoBuilder.For(99, "Other").Language("en").Build(),
            };

            var payload = Ok(await _controller.DriftMatrix(new DriftMatrixQuery { ContentId = 7 }, CancellationToken.None));

            Assert.Equal(7, payload.ContentId);
            Assert.Equal(2, payload.Languages.Count);
        }

        [Fact]
        public void SyncPreview_returns_the_dry_run()
        {
            _service.SyncPreviewResult = new SyncPreview
            {
                Changes = new[] { new SyncPropertyChange { PropertyName = "Title" } },
            };

            var payload = Ok(_controller.SyncPreview(new SyncFromDefaultCommand()));

            Assert.True(payload.CanApply);
        }
    }
}

