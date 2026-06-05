using System.Linq;
using System.Security.Principal;
using EPiServer.Security;
using EPiServer.Shell.Navigation;
using PiNo.Labs.VariationsAuditor.Security;

namespace PiNo.Labs.VariationsAuditor
{
    // Registers "Variations Auditor" in the Optimizely CMS shell navigation.
    [MenuProvider]
    public sealed class VariationsAuditorMenuProvider : IMenuProvider
    {
        private const string SectionPath = MenuPaths.Global + "/cms/audit";
        private const string ItemPath = SectionPath + "/variations-auditor";
        private const string ShellUrl = "/ui/variations-auditor/";

        // Mirror the controller/REST authorization gate so the item is only shown to principals who can open it.
        private static bool IsAuthorized(IPrincipal principal)
            => VariationsAuditorAuthorization.AllowedRoles.Any(principal.IsInRole);

        public IEnumerable<MenuItem> GetMenuItems()
        {
            yield return new SectionMenuItem("Audit", SectionPath)
            {
                IsAvailable = _ => IsAuthorized(PrincipalInfo.CurrentPrincipal),
                SortIndex = 7000,
            };

            yield return new UrlMenuItem("Variations Auditor", ItemPath, ShellUrl)
            {
                IsAvailable = _ => IsAuthorized(PrincipalInfo.CurrentPrincipal),
                SortIndex = 100,
            };
        }
    }
}

