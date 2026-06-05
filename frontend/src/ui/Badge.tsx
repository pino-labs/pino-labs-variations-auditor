import type { ReactNode } from 'react';
import { cn } from './cn';
import { toneDot, toneSoft, type Tone } from './tokens';

interface Props {
  tone?: Tone;
  dot?: boolean;
  shape?: 'pill' | 'tag';
  className?: string;
  title?: string;
  children: ReactNode;
}

// Unified status pill.
export default function Badge({ tone = 'neutral', dot = false, shape = 'pill', className, title, children }: Props) {
  return (
    <span
      title={title}
      className={cn(
        'opti-auditor-inline-flex opti-auditor-items-center opti-auditor-gap-1.5 opti-auditor-px-2 opti-auditor-py-0.5 opti-auditor-text-xs opti-auditor-font-medium opti-auditor-ring-1 opti-auditor-ring-inset',
        shape === 'pill' ? 'opti-auditor-rounded-full' : 'opti-auditor-rounded',
        toneSoft[tone],
        className,
      )}
    >
      {dot && <span className={cn('opti-auditor-h-1.5 opti-auditor-w-1.5 opti-auditor-rounded-full', toneDot[tone])} />}
      {children}
    </span>
  );
}

