import { format, parseISO } from 'date-fns';

export function fmtDate(iso: string | null | undefined, pattern = 'dd MMM yyyy'): string {
  if (!iso) return '—';
  try {
    return format(parseISO(iso), pattern);
  } catch {
    return iso;
  }
}

export function todayIso(): string {
  return new Date().toISOString();
}
