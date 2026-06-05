import { useEffect, useState } from 'react';
import { auditorApi } from '../api/client';
import {
  BulkActionType,
  BulkActionTypeLabel,
  BulkItemOutcome,
  BulkItemOutcomeLabel,
  PromoteMode,
  type BulkActionCommand,
  type BulkOperationResult,
  type BulkPreviewResult,
  type VariantIdentity,
} from '../api/types';
import Modal from '../ui/Modal';
import Button from '../ui/Button';
import Spinner from '../ui/Spinner';
import { cn } from '../ui/cn';
import { toneSurface } from '../ui/tokens';

interface Props {
  action: BulkActionType;
  targets: VariantIdentity[];
  onClose: () => void;
  onDone: () => void;
}

type Phase = 'previewing' | 'preview' | 'executing' | 'done' | 'error';

function identityLabel(id: VariantIdentity): string {
  return `#${id.contentId}_${id.workId} · ${id.languageBranch} · ${id.variationKey ?? '(none)'}`;
}

// Safe, conflict-aware bulk flow: always dry-run (bulk-preview) first, surface per-item
// blockingReason/warning, then require explicit confirmation before bulk-exec.
export default function BulkActionModal({ action, targets, onClose, onDone }: Props) {
  const [phase, setPhase] = useState<Phase>('previewing');
  const [preview, setPreview] = useState<BulkPreviewResult | null>(null);
  const [result, setResult] = useState<BulkOperationResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [forceOverwriteStale, setForceOverwriteStale] = useState(false);
  // Merge is the only supported promote mode.
  const promoteMode = PromoteMode.Merge;

  const isPromote = action === BulkActionType.Promote;
  const command: BulkActionCommand = {
    action,
    targets,
    forceOverwriteStale,
    promoteMode: isPromote ? promoteMode : undefined,
  };

  // Dry-run on open and whenever the force flag changes (it changes what gets blocked).
  useEffect(() => {    let cancelled = false;
    setPhase('previewing');
    setError(null);
    auditorApi
      .bulkPreview(command)
      .then((r) => {
        if (!cancelled) {
          setPreview(r);
          setPhase('preview');
        }
      })
      .catch((e) => {
        if (!cancelled) {
          setError(e.message);
          setPhase('error');
        }
      });
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [action, forceOverwriteStale]);

  const blockedCount = preview?.items.filter((i) => i.blockingReason).length ?? 0;
  const actionableCount = (preview?.items.length ?? 0) - blockedCount;

  const exec = () => {
    setPhase('executing');
    auditorApi
      .bulkExec(command)
      .then((r) => {
        setResult(r);
        setPhase('done');
      })
      .catch((e) => {
        setError(e.message);
        setPhase('error');
      });
  };

  const isDestructive = action === BulkActionType.Delete;

  const footer =
    phase === 'done' ? (
      <Button
        variant="primary"
        size="md"
        onClick={() => {
          onDone();
          onClose();
        }}
      >
        Done
      </Button>
    ) : (
      <>
        <Button variant="secondary" size="md" onClick={onClose} disabled={phase === 'executing'}>
          Cancel
        </Button>
        <Button
          variant={isDestructive ? 'danger' : 'primary'}
          size="md"
          onClick={exec}
          disabled={phase !== 'preview' || actionableCount === 0}
        >
          {isDestructive ? 'Delete' : `Confirm ${BulkActionTypeLabel[action]}`} ({actionableCount})
        </Button>
      </>
    );

  return (
    <Modal
      size="lg"
      z={50}
      title={`Bulk ${BulkActionTypeLabel[action]} · ${targets.length} selected`}
      ariaLabel={`Bulk ${BulkActionTypeLabel[action]}`}
      dismissable={phase !== 'executing'}
      onClose={onClose}
      footer={footer}
    >
      {(phase === 'previewing' || phase === 'executing') && (
        <Spinner label={phase === 'previewing' ? 'Running safety preview…' : 'Executing…'} />
      )}

      {phase === 'error' && <div className="opti-auditor-text-sm opti-auditor-text-risk-600">{error}</div>}

      {phase === 'preview' && preview && (
        <>
          <div className="opti-auditor-mb-3 opti-auditor-text-sm opti-auditor-text-gray-600">
            <span className="opti-auditor-font-medium opti-auditor-text-gray-800">{actionableCount}</span> will be
            actioned, <span className="opti-auditor-font-medium opti-auditor-text-gray-800">{blockedCount}</span> blocked.
          </div>

          {isPromote && (
            <div className="opti-auditor-mb-3 opti-auditor-rounded-md opti-auditor-border opti-auditor-border-gray-200 opti-auditor-bg-gray-50 opti-auditor-p-2.5 opti-auditor-text-xs opti-auditor-text-gray-600">
              <span className="opti-auditor-font-medium opti-auditor-text-gray-800">Promote (Merge)</span>{' '}
              - copies only the properties this variant overrides onto master and preserves every other master
              property. This mirrors the platform&apos;s &ldquo;Copy changes to Original&rdquo; delta semantics.
            </div>
          )}

          <ul className="opti-auditor-space-y-1.5">
            {preview.items.map((item, idx) => {
              const blocked = !!item.blockingReason;
              const critical = item.isCriticalReversionRisk;
              return (
                <li
                  key={idx}
                  className={cn(
                    'opti-auditor-rounded-md opti-auditor-border opti-auditor-px-2.5 opti-auditor-py-2 opti-auditor-text-xs',
                    blocked
                      ? cn(toneSurface.neutral, 'opti-auditor-bg-gray-50 opti-auditor-text-gray-500')
                      : critical
                      ? toneSurface.risk
                      : toneSurface.neutral,
                  )}
                >
                  <div className="opti-auditor-font-mono opti-auditor-text-gray-700">{identityLabel(item.identity)}</div>
                  {item.blockingReason && (
                    <div className="opti-auditor-mt-0.5 opti-auditor-text-gray-500">⛔ {item.blockingReason}</div>
                  )}
                  {item.warning && (
                    <div className="opti-auditor-mt-0.5 opti-auditor-font-medium opti-auditor-text-risk-700">⚠ {item.warning}</div>
                  )}
                </li>
              );
            })}
          </ul>

          {preview.hasCriticalWarnings && action === BulkActionType.Promote && (
            <label className="opti-auditor-mt-3 opti-auditor-flex opti-auditor-items-center opti-auditor-gap-2 opti-auditor-text-xs opti-auditor-text-risk-700">
              <input
                type="checkbox"
                checked={forceOverwriteStale}
                onChange={(e) => setForceOverwriteStale(e.target.checked)}
              />
              I understand the reversion risk - force overwrite stale properties.
            </label>
          )}
        </>
      )}

      {phase === 'done' && result && (
        <>
          <div className="opti-auditor-mb-3 opti-auditor-text-sm">
            <span className="opti-auditor-font-medium opti-auditor-text-green-700">{result.successCount} succeeded</span>
            {result.failureCount > 0 && (
              <span className="opti-auditor-text-risk-700"> · {result.failureCount} failed</span>
            )}
          </div>
          <ul className="opti-auditor-space-y-1.5">
            {result.results.map((r, idx) => {
              const ok = r.outcome === BulkItemOutcome.Success;
              return (
                <li
                  key={idx}
                  className={cn(
                    'opti-auditor-rounded-md opti-auditor-border opti-auditor-px-2.5 opti-auditor-py-2 opti-auditor-text-xs',
                    ok ? toneSurface.success : toneSurface.risk,
                  )}
                >
                  <div className="opti-auditor-flex opti-auditor-items-center opti-auditor-justify-between opti-auditor-gap-2">
                    <span className="opti-auditor-font-mono opti-auditor-text-gray-700">{identityLabel(r.identity)}</span>
                    <span className={ok ? 'opti-auditor-text-green-700' : 'opti-auditor-text-risk-700'}>
                      {BulkItemOutcomeLabel[r.outcome]}
                    </span>
                  </div>
                  {r.message && <div className="opti-auditor-mt-0.5 opti-auditor-text-gray-500">{r.message}</div>}
                </li>
              );
            })}
          </ul>
        </>
      )}
    </Modal>
  );
}
