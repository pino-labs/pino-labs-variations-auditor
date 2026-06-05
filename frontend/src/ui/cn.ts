// Tiny class-name combiner: filters out falsy values for inline conditional classes.
export function cn(...parts: Array<string | false | null | undefined>): string {
  return parts.filter(Boolean).join(' ');
}

