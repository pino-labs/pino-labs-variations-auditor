import { useEffect, type ReactNode } from 'react';
import { cn } from './cn';
import { CloseIcon } from './icons';
import IconButton from './IconButton';
import { SHELL_HEADER_OFFSET_PX } from './tokens';

export type ModalSize = 'md' | 'lg' | 'xl' | 'panel';

// `panel` is the right-docked drawer; the rest are centred dialogs.
const SIZE_CLASS: Record<ModalSize, string> = {
  md: 'opti-auditor-max-w-md',
  lg: 'opti-auditor-max-w-2xl',
  xl: 'opti-auditor-max-w-4xl',
  panel: 'opti-auditor-max-w-md',
};

interface Props {
  size?: ModalSize;
  z?: number;
  title?: ReactNode;
  subtitle?: ReactNode;
  headerActions?: ReactNode;
  footer?: ReactNode;
  // Disable backdrop-click / Esc close (e.g. while an action is executing).
  dismissable?: boolean;
  onClose: () => void;
  children: ReactNode;
  ariaLabel?: string;
}

// Unified overlay: backdrop, centred/right-docked surface, standard header and optional sticky footer.
// Owns the Escape-to-close behaviour.
export default function Modal({
  size = 'lg',
  z = 40,
  title,
  subtitle,
  headerActions,
  footer,
  dismissable = true,
  onClose,
  children,
  ariaLabel,
}: Props) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && dismissable) onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose, dismissable]);

  const isPanel = size === 'panel';

  return (
    <div
      className={cn(
        'opti-auditor-fixed opti-auditor-inset-0 opti-auditor-flex opti-auditor-bg-gray-900/40',
        isPanel ? 'opti-auditor-justify-end' : 'opti-auditor-items-center opti-auditor-justify-center opti-auditor-p-4',
      )}
      style={{ top: `${SHELL_HEADER_OFFSET_PX}px`, zIndex: z }}
      onClick={() => dismissable && onClose()}
      aria-hidden="true"
    >
      <div
        className={cn(
          'opti-auditor-flex opti-auditor-w-full opti-auditor-flex-col opti-auditor-overflow-hidden opti-auditor-border-gray-200 opti-auditor-bg-white opti-auditor-shadow-panel',
          SIZE_CLASS[size],
          isPanel
            ? 'opti-auditor-h-full opti-auditor-border-l'
            : 'opti-auditor-max-h-full opti-auditor-rounded-lg opti-auditor-border',
        )}
        role="dialog"
        aria-modal="true"
        aria-label={ariaLabel ?? (typeof title === 'string' ? title : undefined)}
        onClick={(e) => e.stopPropagation()}
      >
        <header className="opti-auditor-flex opti-auditor-items-start opti-auditor-justify-between opti-auditor-gap-3 opti-auditor-border-b opti-auditor-border-gray-200 opti-auditor-px-4 opti-auditor-py-3">
          <div className="opti-auditor-min-w-0">
            {title && <div className="opti-auditor-text-sm opti-auditor-font-semibold opti-auditor-text-gray-900">{title}</div>}
            {subtitle && <div className="opti-auditor-mt-0.5 opti-auditor-text-xs opti-auditor-text-gray-500">{subtitle}</div>}
          </div>
          <div className="opti-auditor-flex opti-auditor-flex-none opti-auditor-items-center opti-auditor-gap-2">
            {headerActions}
            <IconButton onClick={onClose} disabled={!dismissable} aria-label="Close" title="Close (Esc)">
              <CloseIcon />
            </IconButton>
          </div>
        </header>

        <div className="opti-auditor-min-h-0 opti-auditor-flex-1 opti-auditor-overflow-auto opti-auditor-px-4 opti-auditor-py-3">{children}</div>

        {footer && (
          <footer className="opti-auditor-flex opti-auditor-flex-none opti-auditor-items-center opti-auditor-justify-end opti-auditor-gap-2 opti-auditor-border-t opti-auditor-border-gray-200 opti-auditor-px-4 opti-auditor-py-3">
            {footer}
          </footer>
        )}
      </div>
    </div>
  );
}

