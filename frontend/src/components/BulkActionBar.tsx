import { BulkActionType } from '../api/types';
import Button from '../ui/Button';

interface Props {
  count: number;
  onAction: (action: BulkActionType) => void;
  onClear: () => void;
}

// Sticky action bar shown when one or more variants are selected. Every action funnels through the
// mandatory conflict-aware dry-run in BulkActionModal.
export default function BulkActionBar({ count, onAction, onClear }: Props) {
  if (count === 0) return null;

  return (
    <div className="opti-auditor-flex opti-auditor-flex-none opti-auditor-items-center opti-auditor-justify-between opti-auditor-gap-3 opti-auditor-border-t opti-auditor-border-brand-200 opti-auditor-bg-brand-50 opti-auditor-px-4 opti-auditor-py-2.5">
      <div className="opti-auditor-flex opti-auditor-items-center opti-auditor-gap-3 opti-auditor-text-sm">
        <span className="opti-auditor-font-medium opti-auditor-text-brand-800">
          {count} selected
        </span>
        <Button variant="ghost" size="xs" className="hover:opti-auditor-underline" onClick={onClear}>
          Clear
        </Button>
      </div>

      <div className="opti-auditor-flex opti-auditor-items-center opti-auditor-gap-2">
        <Button
          variant="primary"
          onClick={() => onAction(BulkActionType.Promote)}
          title="Promote the selected variants onto master."
        >
          Promote
        </Button>
        <Button variant="secondary" onClick={() => onAction(BulkActionType.Unpublish)}>
          Unpublish
        </Button>
        <Button variant="danger" onClick={() => onAction(BulkActionType.Delete)}>
          Delete
        </Button>
      </div>
    </div>
  );
}
