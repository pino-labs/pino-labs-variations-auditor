using System.Linq;
using System.Threading.Tasks;
using EPiServer.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using PiNo.Labs.VariationsAuditor.Security;
using Xunit;

namespace PiNo.Labs.VariationsAuditor.Tests.Security
{
    // Pins the access-policy contract: authenticated user in one of the canonical CMS roles.
    public sealed class VariationsAuditorAuthorizationTests
    {
        private static async Task<AuthorizationPolicy?> ResolvePolicyAsync()
        {
            var services = new ServiceCollection();
            services.AddVariationsAuditorAuthorization();
            var provider = services.BuildServiceProvider().GetRequiredService<IAuthorizationPolicyProvider>();
            return await provider.GetPolicyAsync(VariationsAuditorAuthorization.PolicyName);
        }

        [Fact]
        public async Task Policy_is_registered_under_the_published_name()
        {
            var policy = await ResolvePolicyAsync();

            Assert.NotNull(policy);
        }

        [Fact]
        public async Task Policy_requires_an_authenticated_user()
        {
            var policy = await ResolvePolicyAsync();

            Assert.Contains(policy!.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
        }

        [Fact]
        public async Task Policy_requires_one_of_the_canonical_cms_roles()
        {
            var policy = await ResolvePolicyAsync();

            var roleRequirement = policy!.Requirements.OfType<RolesAuthorizationRequirement>().Single();

            Assert.Equal(
                new[]
                {
                    Roles.Administrators,
                    Roles.CmsAdmins,
                    Roles.CmsEditors,
                    Roles.WebAdmins,
                    Roles.WebEditors,
                }.OrderBy(r => r),
                roleRequirement.AllowedRoles.OrderBy(r => r));
        }

        [Fact]
        public void Allowed_roles_are_the_platform_role_constants_not_magic_strings()
        {
            Assert.Equal(
                new[]
                {
                    Roles.Administrators,
                    Roles.CmsAdmins,
                    Roles.CmsEditors,
                    Roles.WebAdmins,
                    Roles.WebEditors,
                },
                VariationsAuditorAuthorization.AllowedRoles);
        }

        [Fact]
        public void Policy_name_is_namespaced_to_the_addon()
        {
            Assert.Equal("PiNo.Labs.VariationsAuditor:Access", VariationsAuditorAuthorization.PolicyName);
        }
    }
}

