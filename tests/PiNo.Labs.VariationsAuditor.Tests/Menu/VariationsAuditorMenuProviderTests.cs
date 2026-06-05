using System.Linq;
using EPiServer.Shell.Navigation;
using PiNo.Labs.VariationsAuditor;
using Xunit;

namespace PiNo.Labs.VariationsAuditor.Tests.Menu
{
    // Pins the shell menu structure (section + url item), the shell URL and the placement.
    public sealed class VariationsAuditorMenuProviderTests
    {
        private static readonly string ExpectedSectionPath = MenuPaths.Global + "/cms/audit";
        private static readonly string ExpectedItemPath = ExpectedSectionPath + "/variations-auditor";

        [Fact]
        public void Provides_exactly_a_section_and_a_url_item()
        {
            var items = new VariationsAuditorMenuProvider().GetMenuItems().ToList();

            Assert.Equal(2, items.Count);
            Assert.IsType<SectionMenuItem>(items[0]);
            Assert.IsType<UrlMenuItem>(items[1]);
        }

        [Fact]
        public void Section_item_is_placed_under_the_cms_audit_path()
        {
            var section = new VariationsAuditorMenuProvider().GetMenuItems().OfType<SectionMenuItem>().Single();

            Assert.Equal("Audit", section.Text);
            Assert.Equal(ExpectedSectionPath, section.Path);
            Assert.Equal(7000, section.SortIndex);
        }

        [Fact]
        public void Url_item_links_to_the_shell_route()
        {
            var item = new VariationsAuditorMenuProvider().GetMenuItems().OfType<UrlMenuItem>().Single();

            Assert.Equal("Variations Auditor", item.Text);
            Assert.Equal(ExpectedItemPath, item.Path);
            Assert.Equal("/ui/variations-auditor/", item.Url);
        }

        [Fact]
        public void Every_menu_item_has_an_availability_gate()
        {
            // The gate mirrors the controller policy so the entry only shows for principals who can open it.
            var items = new VariationsAuditorMenuProvider().GetMenuItems().ToList();

            Assert.All(items, i => Assert.NotNull(i.IsAvailable));
        }
    }
}

