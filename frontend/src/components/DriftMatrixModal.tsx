import { useEffect, useState } from 'react';
import { auditorApi } from '../api/client';
import { DivergenceState, type DriftCell, type DriftMatrix } from '../api/types';
import Modal from '../ui/Modal';
import Spinner from '../ui/Spinner';
import { cn } from '../ui/cn';
import { toneSoft, type Tone } from '../ui/tokens';

interface Props {
  contentId: number | null;
  onClose: () => void;
}

// Cell drift state → semantic tone (null = no variant, rendered as a grey hatch).
function cellTone(cell: DriftCell): Tone | null {
  if (!cell.exists) return null;
  if (cell.hasStaleProperties || cell.divergenceState === DivergenceState.OutdatedOverride) return 'caution';
  if (cell.divergenceState === DivergenceState.Orphan) return 'warn';
  return 'success';
}

const CELL_HOVER: Record<Tone, string> = {
  neutral: 'hover:opti-auditor-bg-gray-200',
  brand: 'hover:opti-auditor-bg-brand-200',
  risk: 'hover:opti-auditor-bg-risk-200',
  warn: 'hover:opti-auditor-bg-amber-200',
  caution: 'hover:opti-auditor-bg-orange-200',
  success: 'hover:opti-auditor-bg-green-200',
};

function cellClass(cell: DriftCell): string {
  const tone = cellTone(cell);
  if (!tone) return 'opti-auditor-bg-gray-50 opti-auditor-text-gray-300';
  return cn(toneSoft[tone], CELL_HOVER[tone]);
}

function cellGlyph(cell: DriftCell): string {
  if (!cell.exists) return '·';
  if (cell.hasStaleProperties) return '!';
  if (cell.divergenceState === DivergenceState.Orphan) return '~';
  return '✓';
}

// Language drift matrix: one content item as languages (rows) × variations (columns).
export default function DriftMatrixModal({ contentId, onClose }: Props) {
  const [matrix, setMatrix] = useState<DriftMatrix | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (contentId == null) {
      setMatrix(null);
      return;
    }
    let cancelled = false;
    setLoading(true);
    setError(null);
    auditorApi
      .driftMatrix(contentId)
      .then((m) => !cancelled && setMatrix(m))
      .catch((e) => !cancelled && setError(e.message))
      .finally(() => !cancelled && setLoading(false));
    return () => {
      cancelled = true;
    };
  }, [contentId]);

  if (contentId == null) return null;

  const cellAt = (lang: string, variationKey: string | null) =>
    matrix?.cells.find(
      (c) => c.languageBranch === lang && (c.variationKey ?? '') === (variationKey ?? ''),
    );

  return (
    <Modal
      size="xl"
      title="Language drift matrix"
      subtitle={`${matrix?.contentName ?? `#${contentId}`} - languages × variations`}
      onClose={onClose}
      ariaLabel="Language drift matrix"
      footer={
        <div className="opti-auditor-flex opti-auditor-w-full opti-auditor-items-center opti-auditor-gap-4 opti-auditor-text-xs opti-auditor-text-gray-500">
          <span className="opti-auditor-inline-flex opti-auditor-items-center opti-auditor-gap-1"><span className="opti-auditor-h-3 opti-auditor-w-3 opti-auditor-rounded opti-auditor-bg-green-100" /> Clean</span>
          <span className="opti-auditor-inline-flex opti-auditor-items-center opti-auditor-gap-1"><span className="opti-auditor-h-3 opti-auditor-w-3 opti-auditor-rounded opti-auditor-bg-orange-100" /> Stale</span>
          <span className="opti-auditor-inline-flex opti-auditor-items-center opti-auditor-gap-1"><span className="opti-auditor-h-3 opti-auditor-w-3 opti-auditor-rounded opti-auditor-bg-amber-100" /> Orphan</span>
          <span className="opti-auditor-inline-flex opti-auditor-items-center opti-auditor-gap-1"><span className="opti-auditor-h-3 opti-auditor-w-3 opti-auditor-rounded opti-auditor-bg-gray-50 opti-auditor-ring-1 opti-auditor-ring-inset opti-auditor-ring-gray-200" /> No variant</span>
        </div>
      }
    >
      {loading && <Spinner label="Loading…" />}
      {error && <div className="opti-auditor-text-sm opti-auditor-text-risk-600">{error}</div>}
      {matrix && (
        <table className="opti-auditor-border-collapse opti-auditor-text-xs">
          <thead>
            <tr>
              <th className="opti-auditor-sticky opti-auditor-left-0 opti-auditor-bg-white opti-auditor-px-2 opti-auditor-py-1.5 opti-auditor-text-left opti-auditor-font-semibold opti-auditor-text-gray-500">
                Language ＼ Variation
              </th>
              {matrix.columns.map((c) => (
                <th
                  key={(c.variationKey ?? 'default') + c.audience}
                  className="opti-auditor-px-2 opti-auditor-py-1.5 opti-auditor-text-center opti-auditor-font-semibold opti-auditor-text-gray-700"
                  title={c.variationKey ?? 'Default / language variant'}
                >
                  {c.audience}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {matrix.languages.map((lang) => (
              <tr key={lang}>
                <td className="opti-auditor-sticky opti-auditor-left-0 opti-auditor-bg-white opti-auditor-px-2 opti-auditor-py-1.5 opti-auditor-font-medium opti-auditor-text-gray-700">
                  {lang}
                </td>
                {matrix.columns.map((col) => {
                  const cell = cellAt(lang, col.variationKey);
                  return (
                    <td key={lang + (col.variationKey ?? 'default')} className="opti-auditor-p-1 opti-auditor-text-center">
                      <span
                        className={cn(
                          'opti-auditor-inline-flex opti-auditor-h-8 opti-auditor-w-8 opti-auditor-items-center opti-auditor-justify-center opti-auditor-rounded',
                          cell ? cellClass(cell) : 'opti-auditor-bg-gray-50 opti-auditor-text-gray-300',
                        )}
                        title={
                          cell?.exists
                            ? `${lang} · ${col.audience}: ${cell.overriddenPropertiesCount} overridden${cell.hasStaleProperties ? ', STALE' : ''}`
                            : `${lang} · ${col.audience}: no variant`
                        }
                      >
                        {cell ? cellGlyph(cell) : '·'}
                      </span>
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </Modal>
  );
}
