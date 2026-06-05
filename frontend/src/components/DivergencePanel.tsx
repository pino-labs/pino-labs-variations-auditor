import { useCallback, useEffect, useState } from 'react';
import { auditorApi } from '../api/client';
import type { DivergenceReport, VariantIdentity } from '../api/types';
import OpenInEditorButton from './OpenInEditorButton';
import Modal from '../ui/Modal';
import Button from '../ui/Button';
import Spinner from '../ui/Spinner';
import { cn } from '../ui/cn';
import { toneSurface } from '../ui/tokens';

interface Props {
  identity: VariantIdentity | null;
  onClose: () => void;
  onResolved?: () => void;
}

function valueText(v: unknown): string {
  if (v === null || v === undefined) return '\u2205';
  return String(v);
}

export default function DivergencePanel({ identity, onClose, onResolved }: Props) {
  const [report, setReport] = useState<DivergenceReport | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pendingProp, setPendingProp] = useState<string | null>(null); // property awaiting confirm ('*' = all stale)
  const [busy, setBusy] = useState(false);
  const [syncMessage, setSyncMessage] = useState<string | null>(null);
  const [syncError, setSyncError] = useState<string | null>(null);

  const loadReport = useCallback(() => {
    if (!identity) {
      setReport(null);
      return;
    }
    setLoading(true);
    setError(null);
    auditorApi
      .divergence(identity)
      .then((r) => setReport(r))
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  }, [identity]);

  useEffect(() => {
    setPendingProp(null);
    setSyncMessage(null);
    setSyncError(null);
    loadReport();
  }, [loadReport]);

  const applySync = useCallback(
    (propertyNames: string[]) => {
      if (!identity) return;
      setBusy(true);
      setSyncError(null);
      setSyncMessage(null);
      auditorApi
        .syncExec({ target: identity, propertyNames })
        .then((res) => {
          if (res.success) {
            setSyncMessage(res.message);
            setPendingProp(null);
            loadReport();
            onResolved?.();
          } else {
            setSyncError(res.message);
          }
        })
        .catch((e) => setSyncError(e.message))
        .finally(() => setBusy(false));
    },
    [identity, loadReport, onResolved],
  );

  if (!identity) return null;

  const hasStale = report?.properties.some((p) => p.isStale) ?? false;

  return (
    <Modal
      size="lg"
      z={30}
      title="Structural Divergence"
      ariaLabel="Structural divergence"
      onClose={onClose}
      subtitle={
        <span className="opti-auditor-truncate opti-auditor-font-mono">
          #{identity.contentId}_{identity.workId} &middot; {identity.languageBranch} &middot;{' '}
          {identity.variationKey ?? '(none)'}
        </span>
      }
      headerActions={
        <>
          {report && hasStale && (
            <Button
              variant="success"
              size="xs"
              disabled={busy}
              title="Pull the default's current values into this variation for every stale property."
              onClick={() => {
                setSyncError(null);
                setSyncMessage(null);
                setPendingProp('*');
              }}
            >
              Sync all stale
            </Button>
          )}
          {report && <OpenInEditorButton editUrl={report.editUrl} />}
        </>
      }
    >
      {loading && <Spinner label="Loading..." />}
      {error && <div className="opti-auditor-text-sm opti-auditor-text-risk-600">{error}</div>}

      {syncMessage && (
        <div className={cn('opti-auditor-mb-3 opti-auditor-rounded-md opti-auditor-border opti-auditor-px-3 opti-auditor-py-2 opti-auditor-text-xs opti-auditor-text-green-700', toneSurface.success)}>
          ✓ {syncMessage}
        </div>
      )}
      {syncError && (
        <div className={cn('opti-auditor-mb-3 opti-auditor-rounded-md opti-auditor-border opti-auditor-px-3 opti-auditor-py-2 opti-auditor-text-xs opti-auditor-text-risk-700', toneSurface.risk)}>
          ⛔ {syncError}
        </div>
      )}
      {pendingProp && (
        <div className="opti-auditor-mb-3 opti-auditor-rounded-md opti-auditor-border opti-auditor-border-green-300 opti-auditor-bg-green-50 opti-auditor-px-3 opti-auditor-py-2.5">
          <div className="opti-auditor-text-xs opti-auditor-text-gray-700">
            Pull the default&apos;s current value into this variation
            {pendingProp === '*' ? ' for all stale properties' : (
              <> for <span className="opti-auditor-font-mono opti-auditor-font-medium">{pendingProp}</span></>
            )}
            ? This publishes the variation with the default&apos;s value - the inverse of Promote.
          </div>
          <div className="opti-auditor-mt-2 opti-auditor-flex opti-auditor-items-center opti-auditor-gap-2">
            <Button
              variant="success"
              size="xs"
              onClick={() => applySync(pendingProp === '*' ? [] : [pendingProp])}
              disabled={busy}
            >
              {busy ? 'Applying…' : 'Confirm sync'}
            </Button>
            <Button variant="secondary" size="xs" onClick={() => setPendingProp(null)} disabled={busy}>
              Cancel
            </Button>
          </div>
        </div>
      )}

      {report && (
        <>
          <section>
            <h3 className="opti-auditor-mb-2 opti-auditor-text-xs opti-auditor-font-semibold opti-auditor-uppercase opti-auditor-tracking-wide opti-auditor-text-gray-400">
              Properties
            </h3>
            <div className="opti-auditor-space-y-2">
              {report.properties.map((p) => (
                <div
                  key={p.propertyName}
                  className={cn('opti-auditor-rounded-md opti-auditor-border opti-auditor-p-2.5', p.isStale ? toneSurface.caution : toneSurface.neutral)}
                >
                  <div className="opti-auditor-flex opti-auditor-items-center opti-auditor-justify-between opti-auditor-gap-2">
                    <span className="opti-auditor-font-medium opti-auditor-text-gray-800">{p.propertyName}</span>
                    {p.isStale && (
                      <span className="opti-auditor-flex opti-auditor-flex-none opti-auditor-items-center opti-auditor-gap-1.5">
                        <Button
                          variant="secondary"
                          size="xs"
                          className="opti-auditor-border-green-300 opti-auditor-px-1.5 opti-auditor-py-0.5 opti-auditor-text-[10px] opti-auditor-font-semibold opti-auditor-text-green-700 hover:opti-auditor-bg-green-50"
                          disabled={busy}
                          title="Pull the default's current value into this property."
                          onClick={() => {
                            setSyncError(null);
                            setSyncMessage(null);
                            setPendingProp(p.propertyName);
                          }}
                        >
                          Sync from default
                        </Button>
                        <span className="opti-auditor-rounded opti-auditor-bg-orange-500 opti-auditor-px-2 opti-auditor-py-0.5 opti-auditor-text-[10px] opti-auditor-font-semibold opti-auditor-uppercase opti-auditor-tracking-wide opti-auditor-text-white">
                          Stale regression risk
                        </span>
                      </span>
                    )}
                  </div>
                  <div className="opti-auditor-mt-2 opti-auditor-grid opti-auditor-grid-cols-[1fr_auto_1fr] opti-auditor-items-center opti-auditor-gap-2 opti-auditor-text-xs">
                    <div className="opti-auditor-min-w-0">
                      <div className="opti-auditor-mb-0.5 opti-auditor-text-gray-400">Master state</div>
                      <div className="opti-auditor-break-words opti-auditor-rounded opti-auditor-bg-gray-50 opti-auditor-px-1.5 opti-auditor-py-1 opti-auditor-font-mono opti-auditor-text-gray-700">
                        {valueText(p.masterCurrentValue)}
                      </div>
                    </div>
                    <svg className="opti-auditor-h-4 opti-auditor-w-4 opti-auditor-flex-none opti-auditor-text-gray-300" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                      <path fillRule="evenodd" d="M3 10a.75.75 0 01.75-.75h8.69L9.22 6.03a.75.75 0 011.06-1.06l4.5 4.5a.75.75 0 010 1.06l-4.5 4.5a.75.75 0 11-1.06-1.06l3.22-3.22H3.75A.75.75 0 013 10z" clipRule="evenodd" />
                    </svg>
                    <div className="opti-auditor-min-w-0">
                      <div className="opti-auditor-mb-0.5 opti-auditor-text-gray-400">Variant override</div>
                      <div
                        className={cn(
                          'opti-auditor-break-words opti-auditor-rounded opti-auditor-px-1.5 opti-auditor-py-1 opti-auditor-font-mono',
                          p.isStale ? 'opti-auditor-bg-orange-100 opti-auditor-text-orange-700' : 'opti-auditor-bg-gray-50 opti-auditor-text-gray-700',
                        )}
                      >
                        {valueText(p.variantValue)}
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </section>

          <section className="opti-auditor-mt-4">
            <h3 className="opti-auditor-mb-2 opti-auditor-text-xs opti-auditor-font-semibold opti-auditor-uppercase opti-auditor-tracking-wide opti-auditor-text-gray-400">
              Content Areas (structural)
            </h3>
            {report.contentAreaDiffs.length === 0 && (
              <div className="opti-auditor-text-sm opti-auditor-text-gray-400">No structural changes.</div>
            )}
            {report.contentAreaDiffs.map((d) => (
              <div key={d.propertyName} className="opti-auditor-mb-2 opti-auditor-rounded-md opti-auditor-border opti-auditor-border-gray-200 opti-auditor-p-2.5 opti-auditor-text-xs">
                <div className="opti-auditor-font-medium opti-auditor-text-gray-800">{d.propertyName}</div>
                <div className="opti-auditor-mt-1 opti-auditor-text-green-700">
                  Added Blocks: [{d.addedBlockIds.join(', ') || '\u2014'}]
                </div>
                <div className="opti-auditor-text-risk-600">
                  Removed Blocks: [{d.removedBlockIds.join(', ') || '\u2014'}]
                </div>
              </div>
            ))}
          </section>

          <p className="opti-auditor-mt-4 opti-auditor-border-t opti-auditor-border-gray-100 opti-auditor-pt-2 opti-auditor-text-xs opti-auditor-text-gray-400">
            {report.baselineResolutionStrategy}
          </p>
        </>
      )}
    </Modal>
  );
}
