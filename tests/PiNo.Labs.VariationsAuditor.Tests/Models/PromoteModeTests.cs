using System;
using PiNo.Labs.VariationsAuditor.Models;
using Xunit;

namespace PiNo.Labs.VariationsAuditor.Tests.Models
{
    // Promote supports only delta-merge semantics; the former Replace mode was removed.
    public sealed class PromoteModeTests
    {
        [Fact]
        public void Replace_mode_no_longer_exists()
            => Assert.False(Enum.IsDefined(typeof(PromoteMode), "Replace"));

        [Fact]
        public void Merge_is_the_only_supported_mode()
            => Assert.Equal(new[] { "Merge" }, Enum.GetNames<PromoteMode>());

        [Fact]
        public void Bulk_command_defaults_to_merge()
            => Assert.Equal(PromoteMode.Merge, new BulkActionCommand().PromoteMode);
    }
}

