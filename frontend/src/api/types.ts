// Types mirror the backend DTOs. System.Text.Json serializes enums as NUMBERS, so the enums below use
// numeric members matching the C# definitions.

export interface ContentReference {
  id: number;
  workId: number;
}

// A variant has no Guid; identity is this tuple.
export interface VariantIdentity {
  contentId: number;
  workId: number;
  languageBranch: string;
  variationKey: string | null;
}

// EPiServer.Core.VersionStatus
export enum VersionStatus {
  NotCreated = 0,
  Rejected = 1,
  CheckedOut = 2,
  CheckedIn = 3,
  Published = 4,
  PreviouslyPublished = 5,
  DelayedPublish = 6,
  AwaitingApproval = 7,
}

export const VersionStatusLabel: Record<number, string> = {
  0: 'Not Created',
  1: 'Rejected',
  2: 'Checked Out',
  3: 'Checked In',
  4: 'Published',
  5: 'Previously Published',
  6: 'Delayed Publish',
  7: 'Awaiting Approval',
};

export enum DivergenceState {
  Clear = 0,
  OutdatedOverride = 1,
  Orphan = 2,
}

export const DivergenceStateLabel: Record<number, string> = {
  0: 'Clear',
  1: 'Outdated Override',
  2: 'Orphan',
};

export interface VariationAuditDto {
  identity: VariantIdentity;
  contentName: string;
  variationKey: string | null;
  languageBranch: string;
  status: VersionStatus;
  overriddenPropertiesCount: number;
  divergenceState: DivergenceState;
  hasStaleProperties: boolean;
  variantSaved: string;
  savedBy: string;
  editUrl: string;
  audience: AudienceInfo;
}

export interface AudienceInfo {
  variationKey: string | null;
  displayName: string;
  visitorGroupId: string | null;
  estimatedSize: number | null;
  // true ⇒ matched a real audience (visitor group); false ⇒ displayName is a humanized-key fallback.
  isResolved: boolean;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  currentPage: number;
  pageSize: number;
  totalPages: number;
}

export interface PropertyOverride {
  propertyName: string;
  masterCurrentValue: unknown;
  variantValue: unknown;
  isStale: boolean;
}

export interface ContentAreaDiff {
  propertyName: string;
  addedBlockIds: number[];
  removedBlockIds: number[];
  hasStructuralChange: boolean;
}

export interface DivergenceReport {
  identity: VariantIdentity;
  properties: PropertyOverride[];
  contentAreaDiffs: ContentAreaDiff[];
  baselineResolutionStrategy: string;
  editUrl: string;
}

export enum BulkActionType {
  Promote = 0,
  Unpublish = 1,
  Delete = 2,
}

export const BulkActionTypeLabel: Record<number, string> = {
  0: 'Promote',
  1: 'Unpublish',
  2: 'Delete',
};

// Merge is the only supported promote mode (mirrors backend PromoteMode).
export enum PromoteMode {
  Merge = 0,
}

export const PromoteModeLabel: Record<number, string> = {
  0: 'Merge',
};

export interface BulkActionCommand {
  action: BulkActionType;
  targets: VariantIdentity[];
  forceOverwriteStale: boolean;
  // Only meaningful for Promote; defaults to Merge on the backend.
  promoteMode?: PromoteMode;
}

export interface BulkPreviewItem {
  identity: VariantIdentity;
  isCriticalReversionRisk: boolean;
  staleProperties: string[];
  warning: string | null;
  blockingReason: string | null;
}

export interface BulkPreviewResult {
  action: BulkActionType;
  items: BulkPreviewItem[];
  hasCriticalWarnings: boolean;
}

// Mirrors backend BulkItemOutcome (System.Text.Json serializes enums as numbers).
export enum BulkItemOutcome {
  Success = 0,
  BlockedByStatus = 1,
  BlockedByLock = 2,
  BlockedByStaleConflict = 3,
  Failed = 4,
}

export const BulkItemOutcomeLabel: Record<number, string> = {
  0: 'Success',
  1: 'Blocked (status)',
  2: 'Blocked (locked)',
  3: 'Blocked (stale conflict)',
  4: 'Failed',
};

export interface BulkItemResult {
  identity: VariantIdentity;
  outcome: BulkItemOutcome;
  message: string;
  staleProperties: string[];
}

export interface BulkOperationResult {
  action: BulkActionType;
  executedBy: string;
  executedUtc: string;
  results: BulkItemResult[];
  successCount: number;
  failureCount: number;
  isPartialSuccess: boolean;
}

export interface VersionFilter {
  statuses?: VersionStatus[];
  contentLink?: ContentReference | null;
  languages?: string[];
  variations?: string[];
  excludeDeleted?: boolean;
  includeTotalCount: boolean;
  name?: string | null;
}

export interface VariationQuery {
  page: number;
  pageSize: number;
  filter: VersionFilter;
  term: string | null;
  // Smart filters — applied server-side before paging.
  language?: string | null;
  statuses?: VersionStatus[] | null;
  divergenceStates?: DivergenceState[] | null;
  needsAttentionOnly?: boolean;
}

export interface DriftColumn {
  variationKey: string | null;
  audience: string;
  isResolvedAudience: boolean;
}

export interface DriftCell {
  languageBranch: string;
  variationKey: string | null;
  exists: boolean;
  divergenceState: DivergenceState;
  hasStaleProperties: boolean;
  overriddenPropertiesCount: number;
  identity: VariantIdentity | null;
  editUrl: string;
}

export interface DriftMatrix {
  contentId: number;
  contentName: string;
  languages: string[];
  columns: DriftColumn[];
  cells: DriftCell[];
}

// "Sync from default" (inverse of Promote).
export interface SyncFromDefaultCommand {
  target: VariantIdentity;
  propertyNames: string[]; // empty ⇒ all stale
}

export interface SyncPropertyChange {
  propertyName: string;
  currentVariantValue: unknown;
  incomingMasterValue: unknown;
}

export interface SyncPreview {
  target: VariantIdentity;
  changes: SyncPropertyChange[];
  blockingReason: string | null;
  canApply: boolean;
}

export interface SyncResult {
  target: VariantIdentity;
  success: boolean;
  appliedProperties: string[];
  message: string;
}

