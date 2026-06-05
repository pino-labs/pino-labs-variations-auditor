import { useMemo, useState } from 'react';
import {
  DivergenceState,
  DivergenceStateLabel,
  VersionStatusLabel,
  type VariantIdentity,
  type VariationAuditDto,
} from '../api/types';
import OpenInEditorButton from './OpenInEditorButton';
import Badge from '../ui/Badge';
import Spinner from '../ui/Spinner';
import type { Tone } from '../ui/tokens';

interface Props {
  rows: VariationAuditDto[];
  loading?: boolean;
  selectedKey: string | null;
  onSelect: (identity: VariantIdentity) => void;
  selectedKeys: Set<string>;
  onToggleSelect: (identity: VariantIdentity) => void;
  onToggleMany: (identities: VariantIdentity[], selected: boolean) => void;
  onOpenMatrix?: (contentId: number) => void;
}

export function identityKey(id: VariantIdentity): string {
  return `${id.contentId}_${id.workId}_${id.languageBranch}_${id.variationKey ?? ''}`;
}

function DivergenceBadge({ state }: { state: DivergenceState }) {
  const tone: Tone =
    state === DivergenceState.OutdatedOverride ? 'caution' : state === DivergenceState.Orphan ? 'warn' : 'success';
  return (
    <Badge tone={tone} dot>
      {DivergenceStateLabel[state]}
    </Badge>
  );
}

// Human-readable audience for a variant: resolved audience → glyph, unresolved key → muted, none → dash.
function AudienceCell({ row }: { row: VariationAuditDto }) {
  const a = row.audience;
  const hasName = !!a?.displayName;

  if (!hasName) {
    return (
      <span className="opti-auditor-text-gray-400" title="Language variant — no targeted audience.">
        &mdash;
      </span>
    );
  }

  return (
    <span className="opti-auditor-inline-flex opti-auditor-items-center opti-auditor-gap-1.5">
      <svg
        className={`opti-auditor-h-3.5 opti-auditor-w-3.5 opti-auditor-flex-none ${a.isResolved ? 'opti-auditor-text-brand-500' : 'opti-auditor-text-gray-300'}`}
        viewBox="0 0 20 20"
        fill="currentColor"
        aria-hidden="true"
      >
        <path d="M10 9a3 3 0 100-6 3 3 0 000 6zM6 8a2 2 0 11-4 0 2 2 0 014 0zM1.49 15.326a.78.78 0 01-.358-.442 3 3 0 014.308-3.516 6.484 6.484 0 00-1.905 3.959c-.023.222-.014.442.025.654a4.97 4.97 0 01-2.07-.655zM16.44 15.98a4.97 4.97 0 002.07-.654.78.78 0 00.357-.442 3 3 0 00-4.308-3.516 6.484 6.484 0 011.905 3.959c.023.222.014.442-.025.654zM18 8a2 2 0 11-4 0 2 2 0 014 0zM5.304 16.19a.844.844 0 01-.277-.71 5 5 0 019.947 0 .843.843 0 01-.277.71A6.975 6.975 0 0110 18a6.974 6.974 0 01-4.696-1.81z" />
      </svg>
      <span
        className={a.isResolved ? 'opti-auditor-font-medium opti-auditor-text-gray-800' : 'opti-auditor-text-gray-500 opti-auditor-italic'}
        title={
          a.isResolved
            ? `Audience "${a.displayName}"${a.visitorGroupId ? ` (visitor group ${a.visitorGroupId})` : ''}.`
            : `No matching audience found — name inferred from variation key "${row.variationKey ?? ''}".`
        }
      >
        {a.displayName}
      </span>
      {typeof a.estimatedSize === 'number' && (
        <span className="opti-auditor-rounded opti-auditor-bg-gray-100 opti-auditor-px-1.5 opti-auditor-py-0.5 opti-auditor-text-[10px] opti-auditor-tabular-nums opti-auditor-text-gray-500">
          {a.estimatedSize.toLocaleString()}
        </span>
      )}
    </span>
  );
}

export default function InventoryGrid({ rows, loading, selectedKey, onSelect, selectedKeys, onToggleSelect, onToggleMany, onOpenMatrix }: Props) {
  // Group variants under their content item; each item is an expandable parent row.
  const groups = useMemo(() => {
    const map = new Map<string, { name: string; contentId: number; variants: VariationAuditDto[] }>();
    for (const r of rows) {
      const key = `${r.identity.contentId}`;
      if (!map.has(key)) {
        map.set(key, { name: r.contentName, contentId: r.identity.contentId, variants: [] });
      }
      map.get(key)!.variants.push(r);
    }
    return Array.from(map.values());
  }, [rows]);

  const allIdentities = useMemo(() => rows.map((r) => r.identity), [rows]);
  const allSelected = allIdentities.length > 0 && allIdentities.every((id) => selectedKeys.has(identityKey(id)));

  const [collapsed, setCollapsed] = useState<Set<number>>(new Set());

  const toggle = (id: number) =>
    setCollapsed((prev) => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });

  return (
    <table className="opti-auditor-w-full opti-auditor-border-collapse opti-auditor-text-sm">
      <thead className="opti-auditor-sticky opti-auditor-top-0 opti-auditor-z-10">
        <tr className="opti-auditor-bg-gray-50 opti-auditor-text-left opti-auditor-text-xs opti-auditor-uppercase opti-auditor-tracking-wide opti-auditor-text-gray-500 opti-auditor-shadow-[inset_0_-1px_0_0_rgb(229,231,235)]">
          <th className="opti-auditor-w-10 opti-auditor-px-3 opti-auditor-py-2.5">
            <input
              type="checkbox"
              aria-label="Select all visible variants"
              checked={allSelected}
              onChange={(e) => onToggleMany(allIdentities, e.target.checked)}
            />
          </th>
          <th className="opti-auditor-px-3 opti-auditor-py-2.5 opti-auditor-font-semibold">Audience</th>
          <th className="opti-auditor-px-3 opti-auditor-py-2.5 opti-auditor-font-semibold">Variant Key</th>
          <th className="opti-auditor-px-3 opti-auditor-py-2.5 opti-auditor-font-semibold">Language Branch</th>
          <th className="opti-auditor-px-3 opti-auditor-py-2.5 opti-auditor-font-semibold">Status</th>
          <th className="opti-auditor-px-3 opti-auditor-py-2.5 opti-auditor-font-semibold">Overridden Properties</th>
          <th className="opti-auditor-px-3 opti-auditor-py-2.5 opti-auditor-font-semibold">Divergence State</th>
          <th className="opti-auditor-px-3 opti-auditor-py-2.5 opti-auditor-font-semibold opti-auditor-text-right">Actions</th>
        </tr>
      </thead>
      <tbody>
        {groups.map((g) => {
          const isCollapsed = collapsed.has(g.contentId);
          return (
            <ContentGroup
              key={g.contentId}
              name={g.name}
              contentId={g.contentId}
              variants={g.variants}
              isCollapsed={isCollapsed}
              onToggle={() => toggle(g.contentId)}
              selectedKey={selectedKey}
              onSelect={onSelect}
              selectedKeys={selectedKeys}
              onToggleSelect={onToggleSelect}
              onToggleMany={onToggleMany}
              onOpenMatrix={onOpenMatrix}
            />
          );
        })}
        {groups.length === 0 && (
          <tr>
            <td colSpan={8} className="opti-auditor-px-3 opti-auditor-py-16 opti-auditor-text-center">
              {loading ? (
                <Spinner label="Loading variations..." />
              ) : (
                <div className="opti-auditor-mx-auto opti-auditor-flex opti-auditor-max-w-md opti-auditor-flex-col opti-auditor-items-center opti-auditor-gap-2 opti-auditor-text-gray-400">
                  <svg className="opti-auditor-h-10 opti-auditor-w-10 opti-auditor-text-gray-300" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5m-16.5 5.25h16.5" />
                  </svg>
                  <span className="opti-auditor-text-sm opti-auditor-font-medium opti-auditor-text-gray-600">No content variations found.</span>
                  <span className="opti-auditor-text-sm opti-auditor-text-gray-400">
                    Variations let you target content to specific audiences inside a single page. Create one
                    from a page&apos;s <em>Variations</em> tab in the editor, then return here to audit drift,
                    spot orphans, and clean up safely.
                  </span>
                </div>
              )}
            </td>
          </tr>
        )}
      </tbody>
    </table>
  );
}

function ContentGroup({
  name,
  contentId,
  variants,
  isCollapsed,
  onToggle,
  selectedKey,
  onSelect,
  selectedKeys,
  onToggleSelect,
  onToggleMany,
  onOpenMatrix,
}: {
  name: string;
  contentId: number;
  variants: VariationAuditDto[];
  isCollapsed: boolean;
  onToggle: () => void;
  selectedKey: string | null;
  onSelect: (identity: VariantIdentity) => void;
  selectedKeys: Set<string>;
  onToggleSelect: (identity: VariantIdentity) => void;
  onToggleMany: (identities: VariantIdentity[], selected: boolean) => void;
  onOpenMatrix?: (contentId: number) => void;
}) {
  const groupIdentities = variants.map((v) => v.identity);
  const groupAllSelected = groupIdentities.every((id) => selectedKeys.has(identityKey(id)));
  const languages = new Set(variants.map((v) => v.languageBranch));
  return (
    <>
      <tr
        className="opti-auditor-cursor-pointer opti-auditor-border-t opti-auditor-border-gray-200 opti-auditor-bg-gray-50/60 hover:opti-auditor-bg-gray-100"
        onClick={onToggle}
      >
        <td className="opti-auditor-px-3 opti-auditor-py-2" onClick={(e) => e.stopPropagation()}>
          <input
            type="checkbox"
            aria-label={`Select all variants of ${name}`}
            checked={groupAllSelected}
            onChange={(e) => onToggleMany(groupIdentities, e.target.checked)}
          />
        </td>
        <td colSpan={7} className="opti-auditor-px-3 opti-auditor-py-2 opti-auditor-font-semibold opti-auditor-text-gray-800">
          <span className="opti-auditor-inline-flex opti-auditor-items-center opti-auditor-gap-2">
            <span className="opti-auditor-mr-1 opti-auditor-inline-block opti-auditor-w-3 opti-auditor-text-gray-400 opti-auditor-transition-transform">
              {isCollapsed ? '\u25B6' : '\u25BC'}
            </span>
            {name}
            <span className="opti-auditor-text-xs opti-auditor-font-normal opti-auditor-text-gray-400">
              #{contentId} &middot; {variants.length} variant{variants.length === 1 ? '' : 's'}
            </span>
            {languages.size > 1 && onOpenMatrix && (
              <button
                className="opti-auditor-rounded opti-auditor-border opti-auditor-border-gray-300 opti-auditor-bg-white opti-auditor-px-1.5 opti-auditor-py-0.5 opti-auditor-text-[11px] opti-auditor-font-medium opti-auditor-text-gray-600 hover:opti-auditor-bg-gray-50"
                onClick={(e) => {
                  e.stopPropagation();
                  onOpenMatrix(contentId);
                }}
                title="Open the language × variation drift matrix for this content item."
              >
                Drift matrix
              </button>
            )}
          </span>
        </td>
      </tr>

      {!isCollapsed &&
        variants.map((v) => {
          const key = identityKey(v.identity);
          const isSelected = key === selectedKey;
          const staleRow = v.hasStaleProperties;
          return (
            <tr
              key={key}
              className={[
                'opti-auditor-cursor-pointer opti-auditor-border-t opti-auditor-border-gray-100 opti-auditor-bg-white',
                // Stale variants get a subtle left accent marker instead of a filled row tint.
                staleRow ? 'opti-auditor-border-l-2 opti-auditor-border-l-orange-400' : '',
                isSelected ? 'opti-auditor-ring-2 opti-auditor-ring-inset opti-auditor-ring-brand-500' : '',
                'hover:opti-auditor-bg-brand-50/40',
              ].join(' ')}
              onClick={() => onSelect(v.identity)}
            >
              <td className="opti-auditor-px-3 opti-auditor-py-2" onClick={(e) => e.stopPropagation()}>
                <input
                  type="checkbox"
                  aria-label={`Select variant ${v.variationKey ?? '(none)'}`}
                  checked={selectedKeys.has(key)}
                  onChange={() => onToggleSelect(v.identity)}
                />
              </td>
              <td className="opti-auditor-px-3 opti-auditor-py-2 opti-auditor-pl-8">
                <AudienceCell row={v} />
              </td>
              <td className="opti-auditor-px-3 opti-auditor-py-2 opti-auditor-font-mono opti-auditor-text-xs">
                {v.variationKey ?? <span className="opti-auditor-text-gray-400">(none)</span>}
              </td>
              <td className="opti-auditor-px-3 opti-auditor-py-2">{v.languageBranch}</td>
              <td className="opti-auditor-px-3 opti-auditor-py-2 opti-auditor-text-gray-600">{VersionStatusLabel[v.status]}</td>
              <td className="opti-auditor-px-3 opti-auditor-py-2">
                <span className="opti-auditor-tabular-nums">{v.overriddenPropertiesCount}</span>
                {staleRow && (
                  <span className="opti-auditor-ml-2 opti-auditor-inline-flex opti-auditor-items-center opti-auditor-gap-1 opti-auditor-rounded opti-auditor-bg-orange-100 opti-auditor-px-1.5 opti-auditor-py-0.5 opti-auditor-text-xs opti-auditor-font-semibold opti-auditor-text-orange-700">
                    stale
                  </span>
                )}
              </td>
              <td className="opti-auditor-px-3 opti-auditor-py-2">
                <DivergenceBadge state={v.divergenceState} />
              </td>
              <td className="opti-auditor-px-3 opti-auditor-py-2 opti-auditor-text-right">
                <OpenInEditorButton editUrl={v.editUrl} compact />
              </td>
            </tr>
          );
        })}
    </>
  );
}

