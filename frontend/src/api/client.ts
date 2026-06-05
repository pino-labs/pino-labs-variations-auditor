import type {
  BulkActionCommand,
  BulkOperationResult,
  BulkPreviewResult,
  DivergenceReport,
  DriftMatrix,
  PagedResult,
  SyncFromDefaultCommand,
  SyncPreview,
  SyncResult,
  VariantIdentity,
  VariationAuditDto,
  VariationQuery,
} from './types';

// Runs inside the authenticated shell context — credentials are carried by the host session cookie.
const BASE = '/api/auditor';

function getCsrfToken(): string {
  // Primary: hidden input emitted by the addon's UI page (VariationsAuditorUiController).
  const input = document.querySelector<HTMLInputElement>('input[name="__RequestVerificationToken"]');
  if (input?.value) {
    return input.value;
  }
  // Fallback: meta tag injected by the standalone dev server.
  const meta = document.querySelector<HTMLMetaElement>('meta[name="auditor-csrf-token"]');
  if (meta?.content) {
    return meta.content;
  }
  throw new Error('Missing CSRF token. Ensure the page is loaded within the CMS shell.');
}

async function post<TReq, TRes>(path: string, body: TReq): Promise<TRes> {
  const csrfToken = getCsrfToken();
  const res = await fetch(`${BASE}${path}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      RequestVerificationToken: csrfToken,
    },
    credentials: 'include',
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    throw new Error(`${path} failed: ${res.status} ${res.statusText}`);
  }
  return (await res.json()) as TRes;
}

export const auditorApi = {
  list: (query: VariationQuery) =>
    post<VariationQuery, PagedResult<VariationAuditDto>>('/list', query),

  divergence: (id: VariantIdentity) =>
    post<VariantIdentity, DivergenceReport>('/divergence', id),

  bulkPreview: (cmd: BulkActionCommand) =>
    post<BulkActionCommand, BulkPreviewResult>('/bulk-preview', cmd),

  bulkExec: (cmd: BulkActionCommand) =>
    post<BulkActionCommand, BulkOperationResult>('/bulk-exec', cmd),

  driftMatrix: (contentId: number) =>
    post<{ contentId: number }, DriftMatrix>('/drift-matrix', { contentId }),

  syncPreview: (cmd: SyncFromDefaultCommand) =>
    post<SyncFromDefaultCommand, SyncPreview>('/sync-preview', cmd),
  syncExec: (cmd: SyncFromDefaultCommand) =>
    post<SyncFromDefaultCommand, SyncResult>('/sync-exec', cmd),
};

