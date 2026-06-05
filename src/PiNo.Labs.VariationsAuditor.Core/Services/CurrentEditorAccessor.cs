using System.Security.Claims;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Security;
using Microsoft.Extensions.Logging;
namespace PiNo.Labs.VariationsAuditor.Services
{
    // Resolves the current editor from the authenticated principal and checks the real content ACL.
    public sealed class CurrentEditorAccessor : ICurrentEditorAccessor
    {
        private readonly IPrincipalAccessor _principalAccessor;
        private readonly IContentSecurityRepository _securityRepository;
        private readonly ILogger<CurrentEditorAccessor> _logger;
        public CurrentEditorAccessor(
            IPrincipalAccessor principalAccessor,
            IContentSecurityRepository securityRepository,
            ILogger<CurrentEditorAccessor> logger)
        {
            _principalAccessor = principalAccessor;
            _securityRepository = securityRepository;
            _logger = logger;
        }
        public string GetCurrentEditor()
        {
            var principal = _principalAccessor.Principal;
            var name =
                (principal as ClaimsPrincipal)?.FindFirst("preferred_username")?.Value ??
                (principal as ClaimsPrincipal)?.FindFirst(ClaimTypes.Email)?.Value ??
                principal?.Identity?.Name;
            return string.IsNullOrWhiteSpace(name) ? "anonymous" : name;
        }
        public bool HasAccess(ContentReference contentLink, AccessLevel access)
        {
            try
            {
                var descriptor = _securityRepository.Get(contentLink);
                return descriptor != null && descriptor.HasAccess(_principalAccessor.Principal, access);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ACL check failed for {ContentLink} ({Access}); denying by default.", contentLink, access);
                return false;
            }
        }
    }
}