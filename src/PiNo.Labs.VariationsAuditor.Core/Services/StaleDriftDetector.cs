using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.ServiceLocation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PiNo.Labs.VariationsAuditor.Models;
using PiNo.Labs.VariationsAuditor.Services.Notifications;

namespace PiNo.Labs.VariationsAuditor.Services
{
    // Proactive stale notifications: subscribes to content publishes; when a DEFAULT/master version is
    // published, recomputes the staleness of that content's variants and notifies each newly-stale variant's
    // owner. Inert unless the host opted in (Notifications.Enabled && NotifyOnPublishDrift). A failure here
    // can never fail the triggering publish.
    [InitializableModule]
    [ModuleDependency(typeof(EPiServer.Web.InitializationModule))]
    public sealed class StaleDriftDetector : IInitializableModule
    {
        private const int MaxVariantsPerContent = 200;
        private static readonly TimeSpan DedupeTtl = TimeSpan.FromHours(6);

        private IContentEvents? _contentEvents;
        private IServiceScopeFactory? _scopeFactory;
        private ILogger<StaleDriftDetector>? _logger;

        public void Initialize(InitializationEngine context)
        {
            _scopeFactory = context.Services.GetInstance<IServiceScopeFactory>();
            _logger = context.Services.GetInstance<ILogger<StaleDriftDetector>>();
            _contentEvents = context.Services.GetInstance<IContentEvents>();
            _contentEvents.PublishedContent += OnPublishedContent;
        }

        public void Uninitialize(InitializationEngine context)
        {
            if (_contentEvents != null)
            {
                _contentEvents.PublishedContent -= OnPublishedContent;
            }
        }

        private void OnPublishedContent(object? sender, ContentEventArgs e)
        {
            try
            {
                if (_scopeFactory == null || e?.Content == null || e.ContentLink == null)
                {
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var sp = scope.ServiceProvider;

                var options = sp.GetService<IOptions<VariationsAuditorNotificationOptions>>()?.Value;
                if (options is not { Enabled: true, NotifyOnPublishDrift: true })
                {
                    return;
                }

                var versions = sp.GetRequiredService<IContentVersionRepository>();

                // Act only when a DEFAULT/master version was published (a default edit creates drift).
                var publishedVersion = versions.Load(e.ContentLink);
                if (publishedVersion == null ||
                    !publishedVersion.IsMasterLanguageBranch ||
                    !string.IsNullOrEmpty(publishedVersion.Variation))
                {
                    return;
                }

                NotifyNewlyStaleVariants(sp, e.Content.ContentLink.ID, publishedVersion, options);
            }
            catch (Exception ex)
            {
                // Must never break the publish pipeline.
                _logger?.LogWarning(ex, "VariationsAuditor stale-drift detection failed for {ContentLink}.", e?.ContentLink);
            }
        }

        private void NotifyNewlyStaleVariants(
            IServiceProvider sp, int contentId, ContentVersion masterVersion, VariationsAuditorNotificationOptions options)
        {
            var versions = sp.GetRequiredService<IContentVersionRepository>();
            var divergence = sp.GetRequiredService<IDivergenceEngine>();
            var editUrl = sp.GetRequiredService<IVariationEditUrlResolver>();
            var audiences = sp.GetRequiredService<IVariationAudienceResolver>();
            var dispatcher = sp.GetRequiredService<INotificationDispatcher>();
            var cache = sp.GetRequiredService<IMemoryCache>();

            var filter = new VersionFilter { ContentLink = new ContentReference(contentId), IncludeTotalCount = true };
            var allVersions = versions.List(filter, 0, MaxVariantsPerContent, out _);

            // Collapse to one representative version per (language, variation) variant, newest first.
            var variants = allVersions                .Where(v => !string.IsNullOrEmpty(v.Variation) || !v.IsMasterLanguageBranch)
                .GroupBy(v => (v.LanguageBranch, v.Variation))
                .Select(g => g.OrderByDescending(v => v.Saved).First())
                .ToList();

            var contentName = masterVersion.Name;

            foreach (var variant in variants)
            {
                var identity = VariantIdentity.From(variant.ContentLink, variant.LanguageBranch, variant.Variation);
                var stale = divergence.GetStaleProperties(identity);
                if (stale.Count < Math.Max(1, options.MinimumStaleProperties))
                {
                    continue;
                }

                // De-dupe per variant × this master save, so re-publishing an unchanged master is silent.
                var dedupeKey = $"VariationsAuditor:Notified:{identity.ContentId}:{identity.WorkId}:{identity.LanguageBranch}:{identity.VariationKey}:{masterVersion.Saved.Ticks}";                if (cache.TryGetValue(dedupeKey, out _))
                {
                    continue;
                }
                cache.Set(dedupeKey, true, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = DedupeTtl, Size = 1 });

                var audience = audiences.Resolve(identity.VariationKey);
                var audienceLabel = string.IsNullOrEmpty(audience.DisplayName)
                    ? (identity.VariationKey ?? identity.LanguageBranch)
                    : audience.DisplayName;
                var owner = string.IsNullOrWhiteSpace(variant.SavedBy) ? variant.StatusChangedBy : variant.SavedBy;

                var message = new NotificationMessage
                {
                    Title = $"Variation drift: \"{contentName}\" — {audienceLabel}",
                    Body = $"The default of \"{contentName}\" was just published, which left the {audienceLabel} variation " +
                           $"({identity.LanguageBranch}) showing outdated content for {stale.Count} propert" +
                           (stale.Count == 1 ? "y" : "ies") + $": [{string.Join(", ", stale)}]. " +
                           "Review and Sync-from-default or re-promote in Variations Auditor.",
                    Recipient = owner,
                    Link = editUrl.GetEditUrl(identity),
                    Subject = identity,
                    Severity = NotificationSeverity.Warning,
                };

                // Fire-and-forget: never block the publish thread on remote delivery.
                _ = dispatcher.DispatchAsync(message)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            _logger?.LogWarning(t.Exception, "VariationsAuditor drift notification dispatch faulted.");
                        }
                    }, TaskScheduler.Default);
            }
        }
    }
}

