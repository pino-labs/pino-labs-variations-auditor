import { DivergenceState, DivergenceStateLabel } from '../api/types';
import Button from '../ui/Button';

interface Props {
  needsAttentionOnly: boolean;
  onNeedsAttentionChange: (v: boolean) => void;
  divergenceStates: DivergenceState[];
  onToggleDivergence: (state: DivergenceState) => void;
  language: string;
  languageOptions: string[];
  onLanguageChange: (lang: string) => void;
  onReset: () => void;
  activeCount: number;
}

const DIVERGENCE_CHIPS: DivergenceState[] = [
  DivergenceState.OutdatedOverride,
  DivergenceState.Orphan,
  DivergenceState.Clear,
];

// Smart-filter toolbar. Filters are applied server-side before paging.
export default function FilterBar({
  needsAttentionOnly,
  onNeedsAttentionChange,
  divergenceStates,
  onToggleDivergence,
  language,
  languageOptions,
  onLanguageChange,
  onReset,
  activeCount,
}: Props) {
  return (
    <div className="opti-auditor-flex opti-auditor-flex-wrap opti-auditor-items-center opti-auditor-gap-2 opti-auditor-text-sm">
      <label className="opti-auditor-inline-flex opti-auditor-cursor-pointer opti-auditor-items-center opti-auditor-gap-1.5 opti-auditor-rounded-md opti-auditor-border opti-auditor-border-gray-300 opti-auditor-bg-white opti-auditor-px-2.5 opti-auditor-py-1.5">
        <input
          type="checkbox"
          checked={needsAttentionOnly}
          onChange={(e) => onNeedsAttentionChange(e.target.checked)}
        />
        <span className="opti-auditor-font-medium opti-auditor-text-gray-700">Needs attention</span>
      </label>

      <div className="opti-auditor-flex opti-auditor-items-center opti-auditor-gap-1">
        {DIVERGENCE_CHIPS.map((s) => {
          const active = divergenceStates.includes(s);
          return (
            <button
              key={s}
              onClick={() => onToggleDivergence(s)}
              className={[
                'opti-auditor-rounded-full opti-auditor-border opti-auditor-px-2.5 opti-auditor-py-1 opti-auditor-text-xs opti-auditor-font-medium',
                active
                  ? 'opti-auditor-border-brand-300 opti-auditor-bg-brand-50 opti-auditor-text-brand-700'
                  : 'opti-auditor-border-gray-300 opti-auditor-bg-white opti-auditor-text-gray-600 hover:opti-auditor-bg-gray-50',
              ].join(' ')}
              aria-pressed={active}
            >
              {DivergenceStateLabel[s]}
            </button>
          );
        })}
      </div>

      <select
        className="opti-auditor-rounded-md opti-auditor-border opti-auditor-border-gray-300 opti-auditor-bg-white opti-auditor-px-2 opti-auditor-py-1.5 opti-auditor-text-sm opti-auditor-text-gray-700 opti-auditor-outline-none focus:opti-auditor-border-brand-500 focus:opti-auditor-ring-2 focus:opti-auditor-ring-brand-100"
        value={language}
        onChange={(e) => onLanguageChange(e.target.value)}
        aria-label="Filter by language branch"
      >
        <option value="">All languages</option>
        {languageOptions.map((l) => (
          <option key={l} value={l}>
            {l}
          </option>
        ))}
      </select>

      {activeCount > 0 && (
        <Button variant="ghost" size="xs" className="hover:opti-auditor-underline" onClick={onReset}>
          Reset filters ({activeCount})
        </Button>
      )}
    </div>
  );
}

