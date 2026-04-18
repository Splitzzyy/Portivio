export function formatMoney(value: number, currency = 'INR'): string {
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency,
      maximumFractionDigits: 2,
    }).format(value);
  } catch {
    return `${currency} ${value.toFixed(2)}`;
  }
}

export function formatPercent(value: number): string {
  return `${(value * 100).toFixed(2)}%`;
}

export function pnlColor(value: number, theme: { colors: { primary: string; error: string } }): string {
  if (value > 0) return '#16A34A';
  if (value < 0) return theme.colors.error;
  return theme.colors.primary;
}
