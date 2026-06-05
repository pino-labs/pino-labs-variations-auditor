using PiNo.Labs.VariationsAuditor.Hosting;
using Xunit;

namespace PiNo.Labs.VariationsAuditor.Tests.Hosting
{
    // Regression guard: the React UI must ship embedded in Core.dll (no wwwroot copy), else the UI 404s.
    public sealed class EmbeddedUiAssetTests
    {
        [Fact]
        public void The_shipped_assembly_embeds_the_react_bundle()
        {
            Assert.True(
                VariationsAuditorAssets.HasEmbeddedAssets,
                "The Variations Auditor UI bundle is not embedded in Core.dll. Run 'npm run build' in frontend/ " +
                "(pack-addon.ps1 does this automatically) so the SPA ships inside the assembly.");
        }

        [Fact]
        public void Embedded_file_provider_is_available_when_assets_are_embedded()
        {
            Assert.NotNull(VariationsAuditorAssets.EmbeddedFileProvider);
        }

        [Fact]
        public void Index_html_is_readable_and_references_the_hashed_asset_path()
        {
            var html = VariationsAuditorAssets.ReadIndexHtml();

            Assert.False(string.IsNullOrWhiteSpace(html));
            Assert.Contains(VariationsAuditorAssets.RequestPath + "/assets", html);
        }

        [Fact]
        public void Request_path_matches_the_vite_base()
        {
            Assert.Equal("/variations-auditor", VariationsAuditorAssets.RequestPath);
        }
    }
}

