using System.Threading;
using System.Threading.Tasks;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAccess;
using EPiServer.Security;
using PiNo.Labs.VariationsAuditor.Models;
using Microsoft.Extensions.Logging;
namespace PiNo.Labs.VariationsAuditor.Services
{
    public sealed class ContentVariationService : IContentVariationService
    {
        private readonly IVariationDiscoveryService _discovery;
        private readonly IDivergenceEngine _divergence;
        private readonly IGraphDeliverabilityService _graph;
        private readonly IContentVersionRepository _versions;
        private readonly IContentLockEvaluator _lockEvaluator;
        private readonly ICurrentEditorAccessor _editor;
        private readonly IContentLoader _contentLoader;
        private readonly IContentRepository _contentRepository;
        private readonly IVariationEditUrlResolver _editUrl;
        private readonly IVariationAudienceResolver _audiences;
        private readonly ILogger<ContentVariationService> _logger;
        public ContentVariationService(
            IVariationDiscoveryService discovery,
            IDivergenceEngine divergence,
            IGraphDeliverabilityService graph,
            IContentVersionRepository versions,
            IContentLockEvaluator lockEvaluator,
            ICurrentEditorAccessor editor,
            IContentLoader contentLoader,
            IContentRepository contentRepository,
            IVariationEditUrlResolver editUrl,
            IVariationAudienceResolver audiences,
            ILogger<ContentVariationService> logger)
        {
            _discovery = discovery;
            _divergence = divergence;
            _graph = graph;
            _versions = versions;
            _lockEvaluator = lockEvaluator;
            _editor = editor;
            _contentLoader = contentLoader;
            _contentRepository = contentRepository;
            _editUrl = editUrl;
            _audiences = audiences;
            _logger = logger;
        }
        public async Task<PagedResult<VariationAuditDto>> ListVariationsAsync(VariationQuery query, CancellationToken cancellationToken = default)
        {
            // Divergence/status are computed per variant, so build DTOs for the whole (cached) variant universe,
            // filter, then page - keeping TotalCount/paging correct.
            var universe = await _discovery.DiscoverAllAsync(cancellationToken).ConfigureAwait(false);

            var graphStatuses = await _graph
                .VerifyDeliverabilityBatchAsync(universe, cancellationToken)
                .ConfigureAwait(false);

            var allDtos = universe
                .Select(id => BuildAuditDto(id, graphStatuses.TryGetValue(id, out var gs) ? gs : GraphStatus.Deliverable))
                .Where(d => d != null)
                .Select(d => d!);

            var filtered = ApplyFilters(allDtos, query).ToList();

            var pageItems = filtered.Skip(query.StartIndex).Take(query.MaxRows).ToList();
            return new PagedResult<VariationAuditDto>
            {
                Items = pageItems,
                TotalCount = filtered.Count,
                CurrentPage = query.Page,
                PageSize = query.PageSize,
            };
        }

        // The full, unfiltered audit-DTO universe shared by the grid, health scoring and the drift matrix.
        public async Task<IReadOnlyList<VariationAuditDto>> GetAuditUniverseAsync(CancellationToken cancellationToken = default)
        {
            var universe = await _discovery.DiscoverAllAsync(cancellationToken).ConfigureAwait(false);
            var graphStatuses = await _graph
                .VerifyDeliverabilityBatchAsync(universe, cancellationToken)
                .ConfigureAwait(false);
            return universe
                .Select(id => BuildAuditDto(id, graphStatuses.TryGetValue(id, out var gs) ? gs : GraphStatus.Deliverable))
                .Where(d => d != null)
                .Select(d => d!)
                .ToList();
        }

        // Server-side smart filters, applied before paging so totals stay correct.
        private static IEnumerable<VariationAuditDto> ApplyFilters(IEnumerable<VariationAuditDto> source, VariationQuery query)        {
            var result = source;

            if (!string.IsNullOrWhiteSpace(query.Term))
            {
                var term = query.Term!.Trim();
                result = result.Where(d =>
                    (d.VariationKey?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    d.LanguageBranch.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    d.ContentName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (d.Audience?.DisplayName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (!string.IsNullOrWhiteSpace(query.Language))
            {
                result = result.Where(d => string.Equals(d.LanguageBranch, query.Language, StringComparison.OrdinalIgnoreCase));
            }

            if (query.Statuses is { Count: > 0 })
            {
                result = result.Where(d => query.Statuses.Contains(d.Status));
            }

            if (query.DivergenceStates is { Count: > 0 })
            {
                result = result.Where(d => query.DivergenceStates.Contains(d.DivergenceState));
            }

            if (query.NeedsAttentionOnly)
            {
                result = result.Where(d => d.DivergenceState != DivergenceState.Clear || d.HasStaleProperties);
            }

            return result;
        }
        public DivergenceReport GetDivergence(VariantIdentity id)
        {
            var report = _divergence.BuildReport(id);
            report.EditUrl = _editUrl.GetEditUrl(id);
            return report;
        }
        public BulkPreviewResult PreviewAction(BulkActionCommand cmd, string editor)
        {
            var items = new List<BulkPreviewItem>();
            // SAFETY: promote republishes the shared master, so only the first variant per content can be
            // promoted in a batch; later siblings are blocked here and in ExecAction (same iteration order).
            var conflictSiblings = FindConflictingPromoteSiblings(cmd);
            foreach (var target in cmd.Targets)
            {
                var version = _versions.Load(target.ContentLink);
                if (version == null)
                {
                    items.Add(new BulkPreviewItem { Identity = target, BlockingReason = "Variant version not found." });
                    continue;
                }
                var blocking = EvaluateBlock(cmd.Action, target, version.Status);
                if (blocking != null)
                {
                    items.Add(new BulkPreviewItem { Identity = target, BlockingReason = blocking });
                    continue;
                }
                if (conflictSiblings.Contains(target))
                {
                    items.Add(new BulkPreviewItem
                    {
                        Identity = target,
                        BlockingReason = "Another selected variant already promotes onto content " + target.ContentId +
                                         " earlier in this batch. Promoting multiple variants of the same content at " +
                                         "once is unsafe (each promote republishes the shared master). Promote this one " +
                                         "separately after reviewing the updated divergence.",
                    });
                    continue;
                }

                var stale = _divergence.GetStaleProperties(target);
                var isCritical = cmd.Action == BulkActionType.Promote && stale.Count > 0;
                items.Add(new BulkPreviewItem
                {
                    Identity = target,
                    IsCriticalReversionRisk = isCritical,
                    StaleProperties = stale,
                    Warning = isCritical
                        ? "CRITICAL REVERSION RISK: Promoting this variant will overwrite newer changes on Master for " +
                          "properties: [" + string.Join(", ", stale) + "]. Review the baseline divergence before continuing."
                        : null,
                });
            }
            return new BulkPreviewResult { Action = cmd.Action, Items = items };
        }
        public BulkOperationResult ExecAction(BulkActionCommand cmd)
        {
            var editor = _editor.GetCurrentEditor();
            // At most ONE variant per content may be promoted per batch (each promote republishes the shared
            // master). Only a SUCCESSFUL prior promote reserves the content id.
            var results = new List<BulkItemResult>(cmd.Targets.Count);
            var promotedContentIds = new HashSet<int>();
            foreach (var target in cmd.Targets)
            {
                if (cmd.Action == BulkActionType.Promote && promotedContentIds.Contains(target.ContentId))
                {
                    results.Add(new BulkItemResult
                    {
                        Identity = target,
                        Outcome = BulkItemOutcome.BlockedByStaleConflict,
                        Message = "Another variant of content " + target.ContentId + " was already promoted in this " +
                                  "batch; the shared master has moved. Promote this variant separately after reviewing " +
                                  "the updated divergence.",
                    });
                    continue;
                }
                var single = ExecuteSingle(cmd, target, editor);
                if (cmd.Action == BulkActionType.Promote && single.Outcome == BulkItemOutcome.Success)
                {
                    promotedContentIds.Add(target.ContentId);
                }
                results.Add(single);
            }
            var result = new BulkOperationResult
            {
                Action = cmd.Action,
                ExecutedBy = editor,
                ExecutedUtc = DateTime.UtcNow,
                Results = results,
            };
            foreach (var failed in results.Where(r => r.Outcome != BulkItemOutcome.Success))
            {
                _logger.LogWarning(
                    "Bulk {Action} partial failure. Editor={Editor} Content={ContentId} Work={WorkId} " +
                    "Lang={Lang} Variation={Variation} Outcome={Outcome} Reason={Reason}",
                    cmd.Action, editor, failed.Identity.ContentId, failed.Identity.WorkId,
                    failed.Identity.LanguageBranch, failed.Identity.VariationKey ?? "(none)",
                    failed.Outcome, failed.Message);
            }
            _logger.LogInformation(
                "Bulk {Action} completed by {Editor}: {Success} succeeded, {Failure} failed.",
                cmd.Action, editor, result.SuccessCount, result.FailureCount);
            return result;
        }
        private BulkItemResult ExecuteSingle(BulkActionCommand cmd, VariantIdentity target, string editor)
        {
            var version = _versions.Load(target.ContentLink);
            if (version == null)
            {
                return new BulkItemResult { Identity = target, Outcome = BulkItemOutcome.Failed, Message = "Variant version not found." };
            }
            var statusBlock = EvaluateBlock(cmd.Action, target, version.Status);
            if (statusBlock != null)
            {
                return new BulkItemResult { Identity = target, Outcome = BulkItemOutcome.BlockedByStatus, Message = statusBlock };
            }
            var contentLock = _lockEvaluator.IsLocked(new ContentReference(target.ContentId));
            if (contentLock != null)
            {
                return new BulkItemResult
                {
                    Identity = target,
                    Outcome = BulkItemOutcome.BlockedByLock,
                    Message = "Cannot mutate: content " + target.ContentId + " is locked by " + contentLock.LockedBy +
                              " since " + contentLock.Locked.ToString("u") + ".",
                };
            }
            if (cmd.Action == BulkActionType.Promote)
            {
                var stale = _divergence.GetStaleProperties(target);
                if (stale.Count > 0 && !cmd.ForceOverwriteStale)
                {
                    return new BulkItemResult
                    {
                        Identity = target,
                        Outcome = BulkItemOutcome.BlockedByStaleConflict,
                        StaleProperties = stale,
                        Message = "CRITICAL REVERSION RISK: Promoting would overwrite newer Master changes for properties: [" +
                                  string.Join(", ", stale) + "]. Re-run with ForceOverwriteStale to proceed.",
                    };
                }
            }
            try
            {
                return cmd.Action switch
                {
                    BulkActionType.Promote => Promote(target),
                    BulkActionType.Unpublish => Unpublish(target),
                    BulkActionType.Delete => Delete(target, version.ContentLink, editor),
                    _ => new BulkItemResult { Identity = target, Outcome = BulkItemOutcome.Failed, Message = "Unknown action." },
                };
            }
            catch (Exception ex)
            {
                return new BulkItemResult { Identity = target, Outcome = BulkItemOutcome.Failed, Message = ex.Message };
            }
        }
        // Promote copies the variant's DELTA (only the properties it overrides) onto the published master -
        // the platform's "Copy changes to Original" semantics; master-only properties are preserved.
        private BulkItemResult Promote(VariantIdentity target)
        {
            var variant = _contentLoader.Get<IContent>(target.ContentLink);
            if (variant == null)
            {
                return new BulkItemResult { Identity = target, Outcome = BulkItemOutcome.Failed, Message = "Variant content could not be loaded for promote." };
            }

            var masterLink = new ContentReference(target.ContentId);
            var masterPublished = _versions.LoadPublished(masterLink, target.LanguageBranch) ?? _versions.LoadPublished(masterLink);
            if (masterPublished != null)
            {
                var master = _contentLoader.Get<IContent>(masterPublished.ContentLink);
                if (master != null)
                {
                    var clone = (IContent)((EPiServer.Data.Entity.IReadOnly)master).CreateWritableClone();
                    var applied = OverlayChangedProperties(variant, clone);
                    if (applied.Count == 0)
                    {
                        return new BulkItemResult { Identity = target, Outcome = BulkItemOutcome.Success, Message = "Variant matched master - nothing to promote." };
                    }
                    _contentRepository.Save(clone, SaveAction.Publish, AccessLevel.Publish);
                    return new BulkItemResult
                    {
                        Identity = target,
                        Outcome = BulkItemOutcome.Success,
                        Message = "Promoted " + applied.Count + " overridden propert" + (applied.Count == 1 ? "y" : "ies") +
                                  " onto master: [" + string.Join(", ", applied) + "].",
                    };
                }
            }

            // No master baseline yet (e.g. a brand-new language branch) - publish the full variant as the
            // initial master version.
            var firstPublish = (IContent)((EPiServer.Data.Entity.IReadOnly)variant).CreateWritableClone();
            _contentRepository.Save(firstPublish, SaveAction.Publish, AccessLevel.Publish);
            return new BulkItemResult
            {
                Identity = target,
                Outcome = BulkItemOutcome.Success,
                Message = "Promoted (no master baseline yet - published the variant as the initial master version).",
            };
        }

        // Copy onto the master clone only the editable properties the variant actually changed; returns their
        // names. ContentArea properties are compared structurally so an equal area is not re-published.
        private static List<string> OverlayChangedProperties(IContent variant, IContent masterClone)
        {
            var applied = new List<string>();
            foreach (var variantProp in variant.Property)
            {
                if (variantProp == null || variantProp.IsMetaData)
                {
                    continue;
                }
                var name = variantProp.Name;
                var masterProp = masterClone.Property[name];
                if (masterProp == null || masterProp.IsReadOnly)
                {
                    continue;
                }
                if (PropertyValuesEqual(masterProp, variantProp))
                {
                    continue;
                }
                masterProp.Value = CloneForWrite(variantProp.Value);
                applied.Add(name);
            }
            return applied;
        }

        // Values read from published content can be read-only (XhtmlString, ContentArea); clone them so the
        // target owns a mutable copy before assignment, else Save() throws.
        private static object? CloneForWrite(object? value)
        {
            if (value is EPiServer.Core.XhtmlString xhtml)
            {
                return xhtml.IsReadOnly ? xhtml.CreateWritableClone() : xhtml;
            }
            if (value is ContentArea area && ((EPiServer.Data.Entity.IReadOnly)area).IsReadOnly)
            {
                return ((EPiServer.Data.Entity.IReadOnly<ContentArea>)area).CreateWritableClone();
            }
            return value;
        }

        private static bool PropertyValuesEqual(EPiServer.Core.PropertyData a, EPiServer.Core.PropertyData b)
        {
            if (a.Value is ContentArea areaA && b.Value is ContentArea areaB)
            {
                var idsA = (areaA.Items ?? new List<ContentAreaItem>()).Where(i => i?.ContentLink != null).Select(i => i.ContentLink.ID).OrderBy(x => x);
                var idsB = (areaB.Items ?? new List<ContentAreaItem>()).Where(i => i?.ContentLink != null).Select(i => i.ContentLink.ID).OrderBy(x => x);
                return idsA.SequenceEqual(idsB);
            }
            return string.Equals(a.Value?.ToString() ?? string.Empty, b.Value?.ToString() ?? string.Empty, StringComparison.Ordinal);
        }
        // Unpublish by setting IVersionable.StopPublish to the past, then republishing (supported pattern).
        private BulkItemResult Unpublish(VariantIdentity target)
        {
            var variant = _contentLoader.Get<IContent>(target.ContentLink);
            if (variant == null)
            {
                return new BulkItemResult { Identity = target, Outcome = BulkItemOutcome.Failed, Message = "Variant content could not be loaded for unpublish." };
            }
            var clone = (IContent)((EPiServer.Data.Entity.IReadOnly)variant).CreateWritableClone();
            if (clone is IVersionable versionable)
            {
                versionable.StopPublish = DateTime.UtcNow.AddSeconds(-1);
            }
            _contentRepository.Save(clone, SaveAction.Publish, AccessLevel.Publish);
            return new BulkItemResult { Identity = target, Outcome = BulkItemOutcome.Success, Message = "Unpublished (StopPublish set to past)." };
        }
        private BulkItemResult Delete(VariantIdentity target, ContentReference versionLink, string editor)
        {
            var masterLink = new ContentReference(target.ContentId);
            if (!_editor.HasAccess(masterLink, AccessLevel.Delete))
            {
                return new BulkItemResult
                {
                    Identity = target,
                    Outcome = BulkItemOutcome.Failed,
                    Message = "Access denied: " + editor + " lacks AccessLevel.Delete on content " + target.ContentId + ".",
                };
            }
            // No Delete(..., variationKey) exists; delete the specific version via its WorkID ContentReference.
            _versions.Delete(versionLink, AccessLevel.Delete);
            return new BulkItemResult { Identity = target, Outcome = BulkItemOutcome.Success, Message = "Version deleted." };
        }
        private static HashSet<VariantIdentity> FindConflictingPromoteSiblings(BulkActionCommand cmd)
        {
            var siblings = new HashSet<VariantIdentity>();
            if (cmd.Action != BulkActionType.Promote)
            {
                return siblings;
            }
            var seenContentIds = new HashSet<int>();
            foreach (var target in cmd.Targets)
            {
                if (!seenContentIds.Add(target.ContentId))
                {
                    siblings.Add(target);
                }
            }
            return siblings;
        }

        // Status-based blocking policy. Promote is gated by the same status/lock/stale-conflict layer as the
        // other actions.
        private static string? EvaluateBlock(BulkActionType action, VariantIdentity target, VersionStatus status)
        {

            if (status == VersionStatus.AwaitingApproval)
            {
                return "Cannot mutate: content " + target.ContentId + " is currently awaiting approval.";
            }
            if (status == VersionStatus.DelayedPublish && action == BulkActionType.Promote)
            {
                return "Cannot promote: content " + target.ContentId + " has a delayed (scheduled) publish.";
            }
            if (status == VersionStatus.CheckedOut && (action == BulkActionType.Promote || action == BulkActionType.Delete))
            {
                return "Cannot " + action.ToString().ToLowerInvariant() + ": content " + target.ContentId + " is checked out (work in progress).";
            }
            // Unpublish only makes sense on a currently-published variant: the representative version is the
            // newest, which for a draft/checked-out variant would otherwise publish work-in-progress content.
            if (action == BulkActionType.Unpublish && status != VersionStatus.Published)
            {
                return "Cannot unpublish: variant " + (target.VariationKey ?? target.LanguageBranch) + " of content " +
                       target.ContentId + " is not currently published (status: " + status + "). Only a published variant can be unpublished.";
            }
            return null;
        }
        // "Sync from default" - the inverse of Promote.
        public SyncPreview PreviewSync(SyncFromDefaultCommand cmd)
        {
            var report = _divergence.BuildReport(cmd.Target);
            var selected = NormalizeSyncSelection(cmd, report);
            if (selected.Count == 0)
            {
                return new SyncPreview
                {
                    Target = cmd.Target,
                    BlockingReason = "Nothing to sync: the selected properties already match the default.",
                };
            }

            var changes = report.Properties
                .Where(p => selected.Contains(p.PropertyName))
                .Select(p => new SyncPropertyChange
                {
                    PropertyName = p.PropertyName,
                    CurrentVariantValue = p.VariantValue,
                    IncomingMasterValue = p.MasterCurrentValue,
                })
                .ToList();

            return new SyncPreview { Target = cmd.Target, Changes = changes };
        }

        public SyncResult ExecSync(SyncFromDefaultCommand cmd)
        {
            try
            {
                var contentLock = _lockEvaluator.IsLocked(new ContentReference(cmd.Target.ContentId));
                if (contentLock != null)
                {
                    return FailSync(cmd.Target, "Content " + cmd.Target.ContentId + " is locked by " + contentLock.LockedBy + ".");
                }

                var report = _divergence.BuildReport(cmd.Target);
                var selected = NormalizeSyncSelection(cmd, report);
                if (selected.Count == 0)
                {
                    return FailSync(cmd.Target, "Nothing to sync: the selected properties already match the default.");
                }

                var variant = _contentLoader.Get<IContent>(cmd.Target.ContentLink);
                var masterLink = new ContentReference(cmd.Target.ContentId);
                var masterPublished = _versions.LoadPublished(masterLink, cmd.Target.LanguageBranch) ?? _versions.LoadPublished(masterLink);
                var master = masterPublished != null ? _contentLoader.Get<IContent>(masterPublished.ContentLink) : null;
                if (variant == null || master == null)
                {
                    return FailSync(cmd.Target, "Could not load the variant or its default for sync.");
                }

                var clone = (IContent)((EPiServer.Data.Entity.IReadOnly)variant).CreateWritableClone();
                var applied = new List<string>();
                foreach (var name in selected)
                {
                    var masterProp = master.Property[name];
                    var variantProp = clone.Property[name];
                    if (masterProp == null || variantProp == null || variantProp.IsReadOnly)
                    {
                        continue;
                    }
                    variantProp.Value = CloneForWrite(masterProp.Value);
                    applied.Add(name);
                }

                if (applied.Count == 0)
                {
                    return FailSync(cmd.Target, "No writable properties matched the sync selection.");
                }

                _contentRepository.Save(clone, SaveAction.Publish, AccessLevel.Publish);

                return new SyncResult
                {
                    Target = cmd.Target,
                    Success = true,
                    AppliedProperties = applied,
                    Message = "Synced " + applied.Count + " propert" + (applied.Count == 1 ? "y" : "ies") +
                              " from default: [" + string.Join(", ", applied) + "].",
                };
            }
            catch (Exception ex)
            {
                return FailSync(cmd.Target, ex.Message);
            }
        }

        // Empty selection ⇒ all stale properties; otherwise intersect with what currently differs from master.
        private static HashSet<string> NormalizeSyncSelection(SyncFromDefaultCommand cmd, DivergenceReport report)
        {
            var stale = report.Properties.Where(p => p.IsStale).Select(p => p.PropertyName)
                .ToHashSet(StringComparer.Ordinal);
            if (cmd.PropertyNames == null || cmd.PropertyNames.Count == 0)
            {
                return stale;
            }
            var overridden = report.Properties
                .Where(p => !string.Equals(p.VariantValue ?? string.Empty, p.MasterCurrentValue ?? string.Empty, StringComparison.Ordinal))
                .Select(p => p.PropertyName).ToHashSet(StringComparer.Ordinal);
            return cmd.PropertyNames.Where(overridden.Contains).ToHashSet(StringComparer.Ordinal);
        }

        private static SyncResult FailSync(VariantIdentity target, string message)
            => new SyncResult { Target = target, Success = false, Message = message };

        private VariationAuditDto? BuildAuditDto(VariantIdentity id, GraphStatus graphStatus)
        {
            var version = _versions.Load(id.ContentLink);
            if (version == null)
            {
                return null;
            }
            var name = _contentLoader.TryGet<IContent>(new ContentReference(id.ContentId), out var content) && content != null
                ? content.Name
                : "(" + id.ContentId + ")";
            var report = _divergence.BuildReport(id);
            var staleCount = report.Properties.Count(p => p.IsStale);
            var overriddenCount = report.Properties.Count(p => !string.Equals(p.VariantValue ?? string.Empty, p.MasterCurrentValue ?? string.Empty, StringComparison.Ordinal))
                                  + report.ContentAreaDiffs.Count;
            var divergenceState = graphStatus switch
            {
                GraphStatus.IndexGap => DivergenceState.Orphan,
                GraphStatus.Unpublished_Excluded => DivergenceState.Orphan,
                _ when staleCount > 0 => DivergenceState.OutdatedOverride,
                _ => DivergenceState.Clear,
            };
            return new VariationAuditDto
            {
                Identity = id,
                ContentName = name,
                VariationKey = id.VariationKey,
                LanguageBranch = id.LanguageBranch,
                Status = version.Status,
                OverriddenPropertiesCount = overriddenCount,
                DivergenceState = divergenceState,
                HasStaleProperties = staleCount > 0,
                VariantSaved = version.Saved,
                SavedBy = version.SavedBy,
                EditUrl = _editUrl.GetEditUrl(id),
                Audience = _audiences.Resolve(id.VariationKey),
            };
        }
    }
}