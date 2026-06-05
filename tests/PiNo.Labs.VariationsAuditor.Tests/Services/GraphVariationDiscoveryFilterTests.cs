using PiNo.Labs.VariationsAuditor.Services;
using Xunit;

namespace PiNo.Labs.VariationsAuditor.Tests.Services
{
    // A content variation has a non-empty IVersionable.Variation key; localization-only versions are excluded.
    public sealed class GraphVariationDiscoveryFilterTests
    {
        [Theory]
        [InlineData("audience-returning")]
        [InlineData("summer_sale")]
        [InlineData("control")]
        [InlineData("a")]
        public void NonEmpty_variation_key_is_a_content_variation(string key)
            => Assert.True(GraphVariationDiscoveryService.IsContentVariation(key));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Empty_or_null_variation_key_is_not_a_content_variation(string? key)
            => Assert.False(GraphVariationDiscoveryService.IsContentVariation(key));
    }
}

