import type { ButtonHTMLAttributes, ReactNode } from 'react';
import { cn } from './cn';

export type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'success' | 'ghost';
export type ButtonSize = 'xs' | 'sm' | 'md';

const VARIANT: Record<ButtonVariant, string> = {
  primary: 'opti-auditor-bg-brand-600 opti-auditor-text-white opti-auditor-shadow-sm hover:opti-auditor-bg-brand-700',
  secondary:
    'opti-auditor-border opti-auditor-border-gray-300 opti-auditor-bg-white opti-auditor-text-gray-700 opti-auditor-shadow-sm hover:opti-auditor-bg-gray-50',
  danger: 'opti-auditor-bg-risk-600 opti-auditor-text-white opti-auditor-shadow-sm hover:opti-auditor-bg-risk-700',
  success: 'opti-auditor-bg-green-600 opti-auditor-text-white opti-auditor-shadow-sm hover:opti-auditor-bg-green-700',
  ghost: 'opti-auditor-text-brand-600 hover:opti-auditor-bg-brand-50',
};

const SIZE: Record<ButtonSize, string> = {
  xs: 'opti-auditor-px-2.5 opti-auditor-py-1 opti-auditor-text-xs',
  sm: 'opti-auditor-px-3 opti-auditor-py-1.5 opti-auditor-text-sm',
  md: 'opti-auditor-px-3.5 opti-auditor-py-1.5 opti-auditor-text-sm',
};

interface Props extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  leadingIcon?: ReactNode;
}

// Shared design-system button: call sites pick a semantic variant + size.
export default function Button({
  variant = 'secondary',
  size = 'sm',
  leadingIcon,
  className,
  children,
  type = 'button',
  ...rest
}: Props) {
  return (
    <button
      type={type}
      className={cn(
        'opti-auditor-inline-flex opti-auditor-items-center opti-auditor-justify-center opti-auditor-gap-1.5 opti-auditor-rounded-md opti-auditor-font-medium opti-auditor-outline-none disabled:opti-auditor-cursor-not-allowed disabled:opti-auditor-opacity-50',
        VARIANT[variant],
        SIZE[size],
        className,
      )}
      {...rest}
    >
      {leadingIcon}
      {children}
    </button>
  );
}

