import { cn } from './cn';
import { SpinnerIcon } from './icons';

// Inline "loading…" row used by the grid, panels and modals. Consistent spinner + muted label.
export default function Spinner({ label, className }: { label?: string; className?: string }) {
  return (
    <span className={cn('opti-auditor-inline-flex opti-auditor-items-center opti-auditor-gap-2 opti-auditor-text-sm opti-auditor-text-gray-500', className)}>
      <SpinnerIcon />
      {label}
    </span>
  );
}

