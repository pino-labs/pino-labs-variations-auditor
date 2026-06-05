// Design tokens. Every colour maps onto a semantic Tone; components reference a Tone, never a raw colour.
// Divergence/drift surfaces stay in the warning family (amber → orange); `risk` (red) is errors/destructive
// actions only.
export type Tone = 'neutral' | 'brand' | 'risk' | 'warn' | 'caution' | 'success';

export const toneSoft: Record<Tone, string> = {
  neutral: 'opti-auditor-bg-gray-100 opti-auditor-text-gray-700 opti-auditor-ring-gray-200',
  brand: 'opti-auditor-bg-brand-100 opti-auditor-text-brand-700 opti-auditor-ring-brand-200',
  risk: 'opti-auditor-bg-risk-100 opti-auditor-text-risk-700 opti-auditor-ring-risk-200',
  warn: 'opti-auditor-bg-amber-100 opti-auditor-text-amber-700 opti-auditor-ring-amber-200',
  caution: 'opti-auditor-bg-orange-100 opti-auditor-text-orange-700 opti-auditor-ring-orange-200',
  success: 'opti-auditor-bg-green-100 opti-auditor-text-green-700 opti-auditor-ring-green-200',
};

/** Even lighter "surface" treatment (50-weight bg + 200 border) for cards/rows/banners. */
export const toneSurface: Record<Tone, string> = {
  neutral: 'opti-auditor-border-gray-200 opti-auditor-bg-white',
  brand: 'opti-auditor-border-brand-200 opti-auditor-bg-brand-50',
  risk: 'opti-auditor-border-risk-200 opti-auditor-bg-risk-50',
  warn: 'opti-auditor-border-amber-200 opti-auditor-bg-amber-50',
  caution: 'opti-auditor-border-orange-200 opti-auditor-bg-orange-50',
  success: 'opti-auditor-border-green-200 opti-auditor-bg-green-50',
};

export const toneDot: Record<Tone, string> = {
  neutral: 'opti-auditor-bg-gray-400',
  brand: 'opti-auditor-bg-brand-600',
  risk: 'opti-auditor-bg-risk-600',
  warn: 'opti-auditor-bg-amber-500',
  caution: 'opti-auditor-bg-orange-500',
  success: 'opti-auditor-bg-green-600',
};

export const toneText: Record<Tone, string> = {
  neutral: 'opti-auditor-text-gray-700',
  brand: 'opti-auditor-text-brand-700',
  risk: 'opti-auditor-text-risk-700',
  warn: 'opti-auditor-text-amber-700',
  caution: 'opti-auditor-text-orange-700',
  success: 'opti-auditor-text-green-700',
};

// The CMS shell renders a fixed top chrome bar; overlays must start below it.
export const SHELL_HEADER_OFFSET_PX = 56;

