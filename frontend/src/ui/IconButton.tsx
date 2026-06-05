import type { ButtonHTMLAttributes } from 'react';
import { cn } from './cn';

// Square, icon-only button (close buttons, row affordances).
export default function IconButton({
  className,
  children,
  type = 'button',
  ...rest
}: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      type={type}
      className={cn(
        'opti-auditor-flex opti-auditor-h-7 opti-auditor-w-7 opti-auditor-flex-none opti-auditor-items-center opti-auditor-justify-center opti-auditor-rounded opti-auditor-text-gray-500 opti-auditor-outline-none hover:opti-auditor-bg-gray-100 hover:opti-auditor-text-gray-700 disabled:opti-auditor-opacity-40',
        className,
      )}
      {...rest}
    >
      {children}
    </button>
  );
}

