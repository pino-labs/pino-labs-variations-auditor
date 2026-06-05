using System.Text.RegularExpressions;
using PiNo.Labs.VariationsAuditor.Hosting;
using PiNo.Labs.VariationsAuditor.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PiNo.Labs.VariationsAuditor.Controllers
{
    // Shell entry-point for the Variations Auditor module. The view and its CMS-shell layout are compiled into
    // this addon assembly (a Razor Class Library) and shipped in the NuGet package; the host needs no views of
    // its own. CSRF: the layout emits @Html.AntiForgeryToken(), which the React client sends back as the
    // RequestVerificationToken header on every POST.
    [Authorize(Policy = VariationsAuditorAuthorization.PolicyName)]
    [Route("ui/variations-auditor")]
    public sealed class VariationsAuditorUiController : Controller
    {
        private static readonly Regex JsPattern  = new(@"src=""(/variations-auditor/assets/[^""]+\.js)""",  RegexOptions.Compiled);
        private static readonly Regex CssPattern = new(@"href=""(/variations-auditor/assets/[^""]+\.css)""", RegexOptions.Compiled);

        [HttpGet("")]
        [HttpGet("index")]
        public ActionResult Index()
        {
            // Parse the content-hashed asset paths from the embedded Vite index.html.
            var html = VariationsAuditorAssets.ReadIndexHtml();
            if (!string.IsNullOrEmpty(html))
            {
                var js  = JsPattern.Match(html);
                var css = CssPattern.Match(html);
                if (js.Success)  ViewData["JsUrl"]  = js.Groups[1].Value;
                if (css.Success) ViewData["CssUrl"] = css.Groups[1].Value;
            }

            // Explicit application-relative path to the view compiled into THIS assembly, avoiding the CMS 13
            // content-routing view-resolution pitfall.
            return View("/Views/VariationsAuditor/Index.cshtml");
        }
    }
}
