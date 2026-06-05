using EPiServer.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace PiNo.Labs.VariationsAuditor.Security
{
    // Authorization for the Variations Auditor module. A policy (not literal [Authorize(Roles)]) lets the addon
    // integrate with whatever scheme the host configures (Opti ID on DXP, CMS ASP.NET Identity locally), which
    // both surface the canonical CMS roles as claims. RequireAuthenticatedUser challenges anonymous requests.
    public static class VariationsAuditorAuthorization
    {
        public const string PolicyName = "PiNo.Labs.VariationsAuditor:Access";

        internal static readonly string[] AllowedRoles =
        {
            Roles.Administrators,
            Roles.CmsAdmins,
            Roles.CmsEditors,
            Roles.WebAdmins,
            Roles.WebEditors,
        };

        public static IServiceCollection AddVariationsAuditorAuthorization(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy(PolicyName, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole(AllowedRoles);
                });
            });
            return services;
        }
    }
}

