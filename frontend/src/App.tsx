import { useCallback, useEffect, useMemo, useState } from 'react';
import { auditorApi } from './api/client';
import {
  BulkActionType,
  DivergenceState,
  type PagedResult,
  type VariantIdentity,
  type VariationAuditDto,
} from './api/types';
import InventoryGrid, { identityKey } from './components/InventoryGrid';
import PaginationControls from './components/PaginationControls';
import DivergencePanel from './components/DivergencePanel';
import BulkActionBar from './components/BulkActionBar';
import BulkActionModal from './components/BulkActionModal';
import FilterBar from './components/FilterBar';
import DriftMatrixModal from './components/DriftMatrixModal';
import Button from './ui/Button';
import Badge from './ui/Badge';
import { RefreshIcon, SearchIcon } from './ui/icons';

export default function App() {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [term, setTerm] = useState('');
  const [data, setData] = useState<PagedResult<VariationAuditDto> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<VariantIdentity | null>(null);
  const [selectedKeys, setSelectedKeys] = useState<Set<string>>(new Set());
  const [bulkAction, setBulkAction] = useState<BulkActionType | null>(null);
  const [needsAttention, setNeedsAttention] = useState(false);
  const [divergenceStates, setDivergenceStates] = useState<DivergenceState[]>([]);
  const [language, setLanguage] = useState('');
  const [matrixContentId, setMatrixContentId] = useState<number | null>(null);

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    setSelectedKeys(new Set());
    auditorApi
      .list({
        page,
        pageSize,
        term: term || null,
        filter: { includeTotalCount: true },
        language: language || null,
        divergenceStates: divergenceStates.length ? divergenceStates : null,
        needsAttentionOnly: needsAttention,
      })
      .then(setData)
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  }, [page, pageSize, term, language, divergenceStates, needsAttention]);

  useEffect(() => {
    load();
  }, [load]);

  const rows = data?.items ?? [];
  const riskCount = rows.filter((r) => r.hasStaleProperties).length;

  const languageOptions = useMemo(() => {
    const set = new Set<string>();
    for (const r of rows) set.add(r.languageBranch);
    if (language) set.add(language);
    return Array.from(set).sort();
  }, [rows, language]);

  const activeFilterCount =
    (needsAttention ? 1 : 0) + (divergenceStates.length ? 1 : 0) + (language ? 1 : 0);

  const toggleDivergence = (s: DivergenceState) => {
    setPage(1);
    setDivergenceStates((prev) => (prev.includes(s) ? prev.filter((x) => x !== s) : [...prev, s]));
  };

  const resetFilters = () => {
    setPage(1);
    setNeedsAttention(false);
    setDivergenceStates([]);
    setLanguage('');
  };

  const toggleSelect = useCallback((identity: VariantIdentity) => {
    setSelectedKeys((prev) => {
      const next = new Set(prev);
      const k = identityKey(identity);
      next.has(k) ? next.delete(k) : next.add(k);
      return next;
    });
  }, []);

  const toggleMany = useCallback((identities: VariantIdentity[], on: boolean) => {
    setSelectedKeys((prev) => {
      const next = new Set(prev);
      for (const id of identities) {
        const k = identityKey(id);
        on ? next.add(k) : next.delete(k);
      }
      return next;
    });
  }, []);

  const selectedIdentities = rows.filter((r) => selectedKeys.has(identityKey(r.identity))).map((r) => r.identity);

  return (
    <div className="opti-auditor-flex opti-auditor-w-full opti-auditor-min-h-screen opti-auditor-flex-col opti-auditor-bg-gray-100">
      <header className="opti-auditor-border-b opti-auditor-border-gray-200 opti-auditor-bg-white opti-auditor-px-6 opti-auditor-py-4">
        <div className="opti-auditor-flex opti-auditor-items-start opti-auditor-justify-between opti-auditor-gap-4">
          <div>
            <h1 className="opti-auditor-text-xl opti-auditor-font-semibold opti-auditor-text-gray-900">
              Variations Auditor
            </h1>
            <p className="opti-auditor-mt-0.5 opti-auditor-text-sm opti-auditor-text-gray-500">
              Site-wide audit of content variations, divergence and reversion risk.
            </p>
          </div>
          <div className="opti-auditor-flex opti-auditor-flex-none opti-auditor-items-center opti-auditor-gap-3">
            {riskCount > 0 && (
              <Badge tone="caution" dot className="opti-auditor-px-3 opti-auditor-py-1 opti-auditor-text-sm opti-auditor-font-medium">
                {riskCount} stale regression risk{riskCount === 1 ? '' : 's'} on this page
              </Badge>
            )}
          </div>
        </div>
      </header>

      <div className="opti-auditor-flex opti-auditor-flex-1 opti-auditor-flex-col opti-auditor-p-6">
        <div className="opti-auditor-mb-3 opti-auditor-flex opti-auditor-items-center opti-auditor-gap-2">
          <div className="opti-auditor-relative opti-auditor-w-80">
            <span className="opti-auditor-pointer-events-none opti-auditor-absolute opti-auditor-left-3 opti-auditor-top-1/2 opti-auditor--translate-y-1/2 opti-auditor-text-gray-400">
              <SearchIcon />
            </span>
            <input
              className="opti-auditor-w-full opti-auditor-rounded-md opti-auditor-border opti-auditor-border-gray-300 opti-auditor-py-1.5 opti-auditor-pl-9 opti-auditor-pr-3 opti-auditor-text-sm opti-auditor-outline-none focus:opti-auditor-border-brand-500 focus:opti-auditor-ring-2 focus:opti-auditor-ring-brand-100"
              placeholder="Search variant key or language..."
              value={term}
              onChange={(e) => {
                setPage(1);
                setTerm(e.target.value);
              }}
            />
          </div>
          <Button
            variant="primary"
            onClick={load}
            disabled={loading}
            leadingIcon={<RefreshIcon className={loading ? 'opti-auditor-animate-spin' : ''} />}
          >
            Refresh
          </Button>
          {error && (
            <span className="opti-auditor-ml-1 opti-auditor-text-sm opti-auditor-text-risk-600">{error}</span>
          )}
        </div>

        <div className="opti-auditor-mb-3">
          <FilterBar
            needsAttentionOnly={needsAttention}
            onNeedsAttentionChange={(v) => {
              setPage(1);
              setNeedsAttention(v);
            }}
            divergenceStates={divergenceStates}
            onToggleDivergence={toggleDivergence}
            language={language}
            languageOptions={languageOptions}
            onLanguageChange={(l) => {
              setPage(1);
              setLanguage(l);
            }}
            onReset={resetFilters}
            activeCount={activeFilterCount}
          />
        </div>

        <div className="opti-auditor-flex opti-auditor-min-h-0 opti-auditor-flex-1 opti-auditor-w-full opti-auditor-flex-col opti-auditor-overflow-hidden opti-auditor-rounded-lg opti-auditor-border opti-auditor-border-gray-200 opti-auditor-bg-white opti-auditor-shadow-sm">
          <div className="opti-auditor-min-h-0 opti-auditor-flex-1 opti-auditor-overflow-auto">
            <InventoryGrid
              rows={rows}
              loading={loading}
              selectedKey={selected ? identityKey(selected) : null}
              onSelect={setSelected}
              selectedKeys={selectedKeys}
              onToggleSelect={toggleSelect}
              onToggleMany={toggleMany}
              onOpenMatrix={setMatrixContentId}
            />
          </div>
          <BulkActionBar
            count={selectedKeys.size}
            onAction={setBulkAction}
            onClear={() => setSelectedKeys(new Set())}
          />
          <PaginationControls
            currentPage={data?.currentPage ?? page}
            totalPages={data?.totalPages ?? 1}
            pageSize={pageSize}
            totalCount={data?.totalCount ?? 0}
            onPageChange={setPage}
            onPageSizeChange={(s) => {
              setPage(1);
              setPageSize(s);
            }}
          />
        </div>
      </div>

      <DivergencePanel identity={selected} onClose={() => setSelected(null)} onResolved={load} />

      {bulkAction !== null && selectedIdentities.length > 0 && (
        <BulkActionModal
          action={bulkAction}
          targets={selectedIdentities}
          onClose={() => setBulkAction(null)}
          onDone={load}
        />
      )}

      <DriftMatrixModal contentId={matrixContentId} onClose={() => setMatrixContentId(null)} />
    </div>
  );
}
