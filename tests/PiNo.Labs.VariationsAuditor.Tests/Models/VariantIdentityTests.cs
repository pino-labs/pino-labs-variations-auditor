using EPiServer.Core;
using PiNo.Labs.VariationsAuditor.Models;
using Xunit;

namespace PiNo.Labs.VariationsAuditor.Tests.Models
{
    // Variant identity is a value tuple (no Guid); pins value semantics and the ContentReference projection.
    public sealed class VariantIdentityTests
    {
        [Fact]
        public void Records_with_same_tuple_are_equal()
        {
            var a = new VariantIdentity(42, 7, "en", "audience-a");
            var b = new VariantIdentity(42, 7, "en", "audience-a");

            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Theory]
        [InlineData(42, 7, "sv", "audience-a")]   // different language
        [InlineData(42, 8, "en", "audience-a")]   // different work id
        [InlineData(99, 7, "en", "audience-a")]   // different content id
        [InlineData(42, 7, "en", null)]           // different variation key
        public void Records_differing_in_any_component_are_not_equal(int id, int work, string lang, string? key)
        {
            var baseline = new VariantIdentity(42, 7, "en", "audience-a");
            var other = new VariantIdentity(id, work, lang, key);

            Assert.NotEqual(baseline, other);
        }

        [Fact]
        public void ContentLink_projects_id_and_work_id()
        {
            var id = new VariantIdentity(42, 7, "en", null);

            ContentReference link = id.ContentLink;

            Assert.Equal(42, link.ID);
            Assert.Equal(7, link.WorkID);
        }

        [Fact]
        public void From_factory_round_trips_a_content_reference()
        {
            var id = VariantIdentity.From(new ContentReference(123, 4), "de", "vip");

            Assert.Equal(123, id.ContentId);
            Assert.Equal(4, id.WorkId);
            Assert.Equal("de", id.LanguageBranch);
            Assert.Equal("vip", id.VariationKey);
        }
    }

    public sealed class PagingAndResultTests
    {
        [Theory]
        [InlineData(1, 20, 0)]    // page 1 → startIndex 0
        [InlineData(2, 20, 20)]
        [InlineData(5, 50, 200)]
        [InlineData(0, 20, 0)]    // page clamped to >= 1
        public void VariationQuery_maps_paging_onto_native_startIndex(int page, int size, int expectedStart)
        {
            var query = new VariationQuery { Page = page, PageSize = size };

            Assert.Equal(expectedStart, query.StartIndex);
            Assert.Equal(System.Math.Max(size, 1), query.MaxRows);
        }

        [Theory]
        [InlineData(0, 20, 0)]
        [InlineData(20, 20, 1)]
        [InlineData(21, 20, 2)]
        [InlineData(100, 20, 5)]
        [InlineData(10, 0, 0)]    // guard against divide-by-zero
        public void PagedResult_computes_total_pages_by_ceiling(int total, int size, int expectedPages)
        {
            var result = new PagedResult<string> { TotalCount = total, PageSize = size };

            Assert.Equal(expectedPages, result.TotalPages);
        }

        [Fact]
        public void BulkOperationResult_counts_successes_failures_and_partial_state()
        {
            var result = new BulkOperationResult
            {
                Results = new[]
                {
                    new BulkItemResult { Outcome = BulkItemOutcome.Success },
                    new BulkItemResult { Outcome = BulkItemOutcome.BlockedByLock },
                    new BulkItemResult { Outcome = BulkItemOutcome.Failed },
                },
            };

            Assert.Equal(1, result.SuccessCount);
            Assert.Equal(2, result.FailureCount);
            Assert.True(result.IsPartialSuccess);
        }

        [Fact]
        public void BulkOperationResult_is_not_partial_when_all_succeed()
        {
            var result = new BulkOperationResult
            {
                Results = new[] { new BulkItemResult { Outcome = BulkItemOutcome.Success } },
            };

            Assert.False(result.IsPartialSuccess);
        }
    }
}

