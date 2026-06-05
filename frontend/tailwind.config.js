/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  // CRITICAL UI ISOLATION - Tailwind's global reset (preflight) would destroy the EPiServer host shell
  // (Dojo / On-Page Editing). Disable preflight and namespace every utility with a prefix so nothing
  // leaks into the host UI.
  corePlugins: {
    preflight: false,
  },
  prefix: 'opti-auditor-',
  theme: {
    extend: {
      colors: {
        // Optimizely-aligned action blue (matches the CMS shell accent).
        brand: {
          50: '#eef2ff',
          100: '#e0e7ff',
          200: '#c7d2fe',
          500: '#4f6bed',
          600: '#3b54d6',
          700: '#2f43b0',
        },
        // soft enterprise red - reserved for genuine errors & destructive actions only
        // (failed operations, the delete/danger button, removed blocks). Routine divergence
        // (Outdated Override / stale / Orphan) uses the warning family (amber/orange) instead.
        risk: {
          50: '#fdf2f2',
          100: '#fce4e4',
          200: '#f9cccc',
          600: '#c0392b',
          700: '#a93226',
        },
      },
      boxShadow: {
        // subtle elevation that matches the Optimizely flyout panels
        panel: '0 0 0 1px rgba(15,23,42,0.05), -8px 0 24px -12px rgba(15,23,42,0.25)',
      },
    },
  },
  plugins: [],
};

