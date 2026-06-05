import Button from '../ui/Button';

interface Props {
  currentPage: number;
  totalPages: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
}


const PAGE_SIZES = [10, 20, 50, 100];

export default function PaginationControls({
  currentPage,
  totalPages,
  pageSize,
  totalCount,
  onPageChange,
  onPageSizeChange,
}: Props) {
  return (
    <div className="opti-auditor-flex opti-auditor-flex-none opti-auditor-items-center opti-auditor-justify-between opti-auditor-border-t opti-auditor-border-gray-200 opti-auditor-bg-gray-50/60 opti-auditor-px-4 opti-auditor-py-2.5 opti-auditor-text-sm">
      <div className="opti-auditor-text-gray-600">
        <span className="opti-auditor-font-medium opti-auditor-tabular-nums">{totalCount}</span> variant
        {totalCount === 1 ? '' : 's'} &middot; page{' '}
        <span className="opti-auditor-tabular-nums">{currentPage}</span> of{' '}
        <span className="opti-auditor-tabular-nums">{Math.max(totalPages, 1)}</span>
      </div>

      <div className="opti-auditor-flex opti-auditor-items-center opti-auditor-gap-2">
        <label className="opti-auditor-text-gray-600">Page size</label>
        <select
          className="opti-auditor-rounded-md opti-auditor-border opti-auditor-border-gray-300 opti-auditor-px-2 opti-auditor-py-1 opti-auditor-outline-none focus:opti-auditor-border-brand-500 focus:opti-auditor-ring-2 focus:opti-auditor-ring-brand-100"
          value={pageSize}
          onChange={(e) => onPageSizeChange(Number(e.target.value))}
        >
          {PAGE_SIZES.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </select>

        <Button
          variant="secondary"
          className="opti-auditor-px-3 opti-auditor-py-1"
          disabled={currentPage <= 1}
          onClick={() => onPageChange(currentPage - 1)}
        >
          Prev
        </Button>
        <Button
          variant="secondary"
          className="opti-auditor-px-3 opti-auditor-py-1"
          disabled={currentPage >= totalPages}
          onClick={() => onPageChange(currentPage + 1)}
        >
          Next
        </Button>
      </div>
    </div>
  );
}

