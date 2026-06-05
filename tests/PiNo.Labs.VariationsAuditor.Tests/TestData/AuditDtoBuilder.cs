using EPiServer.Core;
using PiNo.Labs.VariationsAuditor.Models;

namespace PiNo.Labs.VariationsAuditor.Tests.TestData
{
    // Fluent builder for VariationAuditDto so behavioural tests read as specifications.
    internal sealed class AuditDtoBuilder
    {
        private int _contentId = 1;
        private int _workId;
        private string _contentName = "Sample";
        private string _language = "en";
        private string? _variationKey;
        private VersionStatus _status = VersionStatus.Published;
        private DivergenceState _divergence = DivergenceState.Clear;
        private bool _stale;
        private int _overridden;
        private DateTime _savedUtc = DateTime.UtcNow;
        private AudienceInfo _audience = new();

        public static AuditDtoBuilder For(int contentId, string contentName = "Sample")
            => new() { _contentId = contentId, _contentName = contentName };

        public AuditDtoBuilder Language(string language) { _language = language; return this; }
        public AuditDtoBuilder Variation(string? key) { _variationKey = key; return this; }
        public AuditDtoBuilder Status(VersionStatus status) { _status = status; return this; }
        public AuditDtoBuilder Stale(int overridden = 1) { _stale = true; _overridden = overridden; _divergence = DivergenceState.OutdatedOverride; return this; }
        public AuditDtoBuilder Orphan() { _divergence = DivergenceState.Orphan; return this; }
        public AuditDtoBuilder SavedDaysAgo(double days) { _savedUtc = DateTime.UtcNow.AddDays(-days); return this; }
        public AuditDtoBuilder Work(int workId) { _workId = workId; return this; }
        public AuditDtoBuilder Audience(AudienceInfo audience) { _audience = audience; return this; }

        public VariationAuditDto Build()
        {
            var identity = new VariantIdentity(_contentId, _workId, _language, _variationKey);
            return new VariationAuditDto
            {
                Identity = identity,
                ContentName = _contentName,
                VariationKey = _variationKey,
                LanguageBranch = _language,
                Status = _status,
                OverriddenPropertiesCount = _overridden,
                DivergenceState = _divergence,
                HasStaleProperties = _stale,
                VariantSaved = _savedUtc,
                SavedBy = "editor@acme.com",
                EditUrl = $"/EPiServer/CMS/#context=epi.cms.contentdata:///{_contentId}",
                Audience = _audience,
            };
        }

        public static implicit operator VariationAuditDto(AuditDtoBuilder b) => b.Build();
    }
}

