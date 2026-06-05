using PiNo.Labs.VariationsAuditor.Models;
using PiNo.Labs.VariationsAuditor.Services;
using PiNo.Labs.VariationsAuditor.Tests.TestData;
using Xunit;

namespace PiNo.Labs.VariationsAuditor.Tests.Services
{
    // Pins the drift-matrix densification, ordering and header-resolution rules.
    public sealed class DriftMatrixServiceTests
    {
        private readonly IDriftMatrixService _service = new DriftMatrixService();

        [Fact]
        public void Builds_a_dense_grid_of_every_language_times_column()
        {
            // 2 languages × 2 variation columns (default + "vip") with one combination missing.
            var variants = new[]
            {
                AuditDtoBuilder.For(7).Language("en").Variation(null).Build(),
                AuditDtoBuilder.For(7).Language("en").Variation("vip").Stale().Build(),
                AuditDtoBuilder.For(7).Language("sv").Variation(null).Build(),
                // sv/vip intentionally absent
            };

            var matrix = _service.Build(7, variants);

            Assert.Equal(2, matrix.Languages.Count);
            Assert.Equal(2, matrix.Columns.Count);
            Assert.Equal(4, matrix.Cells.Count); // dense: 2 × 2

            var missing = Assert.Single(matrix.Cells, c => c is { LanguageBranch: "sv", VariationKey: "vip" });
            Assert.False(missing.Exists);
        }

        [Fact]
        public void Default_language_column_is_rendered_first()
        {
            var variants = new[]
            {
                AuditDtoBuilder.For(7).Language("en").Variation("vip").Build(),
                AuditDtoBuilder.For(7).Language("en").Variation(null).Build(),
            };

            var matrix = _service.Build(7, variants);

            Assert.Null(matrix.Columns[0].VariationKey); // the default / language column leads
        }

        [Fact]
        public void Existing_cell_carries_divergence_and_edit_url()
        {
            var variants = new[]
            {
                AuditDtoBuilder.For(7).Language("en").Variation("vip").Stale(overridden: 3).Build(),
            };

            var matrix = _service.Build(7, variants);
            var cell = Assert.Single(matrix.Cells);

            Assert.True(cell.Exists);
            Assert.True(cell.HasStaleProperties);
            Assert.Equal(3, cell.OverriddenPropertiesCount);
            Assert.Equal(DivergenceState.OutdatedOverride, cell.DivergenceState);
            Assert.NotEmpty(cell.EditUrl);
        }

        [Fact]
        public void Column_header_uses_resolved_audience_name_when_available()
        {
            var resolved = new AudienceInfo { DisplayName = "VIP Customers", IsResolved = true, VariationKey = "vip" };
            var variants = new[]
            {
                AuditDtoBuilder.For(7).Language("en").Variation("vip").Audience(resolved).Build(),
            };

            var matrix = _service.Build(7, variants);

            Assert.Equal("VIP Customers", matrix.Columns[0].Audience);
            Assert.True(matrix.Columns[0].IsResolvedAudience);
        }

        [Fact]
        public void Column_header_falls_back_to_key_when_audience_unresolved()
        {
            var variants = new[]
            {
                AuditDtoBuilder.For(7).Language("en").Variation("raw-key").Build(),
            };

            var matrix = _service.Build(7, variants);

            Assert.Equal("raw-key", matrix.Columns[0].Audience);
            Assert.False(matrix.Columns[0].IsResolvedAudience);
        }

        [Fact]
        public void Variants_for_other_content_ids_are_ignored()
        {
            var variants = new[]
            {
                AuditDtoBuilder.For(7).Language("en").Build(),
                AuditDtoBuilder.For(99).Language("en").Build(), // different content — must be filtered out
            };

            var matrix = _service.Build(7, variants);

            Assert.Single(matrix.Cells);
            Assert.Equal(7, matrix.ContentId);
        }

        [Fact]
        public void Empty_input_produces_an_empty_but_valid_matrix()
        {
            var matrix = _service.Build(7, System.Array.Empty<VariationAuditDto>());

            Assert.Equal("#7", matrix.ContentName);
            Assert.Empty(matrix.Languages);
            Assert.Empty(matrix.Columns);
            Assert.Empty(matrix.Cells);
        }
    }
}

