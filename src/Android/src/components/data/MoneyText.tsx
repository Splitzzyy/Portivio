import React from 'react';
import { Text, useTheme } from 'react-native-paper';
import { formatMoney } from '../../utils/money';

export function MoneyText({
  value,
  currency,
  variant = 'bodyMedium',
  pnl = false,
}: {
  value: number;
  currency?: string;
  variant?: React.ComponentProps<typeof Text>['variant'];
  pnl?: boolean;
}): React.JSX.Element {
  const theme = useTheme();
  let color: string | undefined;
  if (pnl) {
    if (value > 0) color = '#16A34A';
    else if (value < 0) color = theme.colors.error;
  }
  return (
    <Text variant={variant} style={color ? { color } : undefined}>
      {formatMoney(value, currency ?? 'INR')}
    </Text>
  );
}
