import { cn } from '../ui/cn';
import { ExternalLinkIcon } from '../ui/icons';

interface Props {
  editUrl: string;
  compact?: boolean;
  className?: string;
}

// "Open in editor": opens the CMS 13 editor deep-link in a new tab. stopPropagation prevents the row select.
export default function OpenInEditorButton({ editUrl, compact = false, className = '' }: Props) {
  if (!editUrl) return null;

  if (compact) {
    return (
      <a
        href={editUrl}
        target="_blank"
        rel="noopener noreferrer"
        onClick={(e) => e.stopPropagation()}
        title="Open in editor (new tab)"
        aria-label="Open in editor"
        className={cn(
          'opti-auditor-inline-flex opti-auditor-h-7 opti-auditor-w-7 opti-auditor-items-center opti-auditor-justify-center opti-auditor-rounded opti-auditor-text-gray-400 hover:opti-auditor-bg-brand-50 hover:opti-auditor-text-brand-600',
          className,
        )}
      >
        <ExternalLinkIcon />
      </a>
    );
  }

  return (
    <a
      href={editUrl}
      target="_blank"
      rel="noopener noreferrer"
      onClick={(e) => e.stopPropagation()}
      title="Open in editor (new tab)"
      className={cn(
        'opti-auditor-inline-flex opti-auditor-items-center opti-auditor-gap-1.5 opti-auditor-rounded-md opti-auditor-border opti-auditor-border-brand-200 opti-auditor-bg-brand-50 opti-auditor-px-2.5 opti-auditor-py-1 opti-auditor-text-xs opti-auditor-font-medium opti-auditor-text-brand-700 hover:opti-auditor-bg-brand-100',
        className,
      )}
    >
      <ExternalLinkIcon />
      Open in editor
    </a>
  );
}
