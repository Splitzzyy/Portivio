import React, { useCallback } from 'react';
import { RefreshControl, ScrollView, StyleSheet, View } from 'react-native';
import { Button, Card, Divider, Text } from 'react-native-paper';
import Constants from 'expo-constants';

import { useHome } from '../../../queries/home';
import { useLogout } from '../../../queries/auth';
import { LoadingOverlay } from '../../../components/feedback/LoadingOverlay';
import { ErrorView } from '../../../components/feedback/ErrorView';
import { EmptyState } from '../../../components/feedback/EmptyState';
import { MoneyText } from '../../../components/data/MoneyText';
import { fmtDate } from '../../../utils/dates';
import { useAuthStore } from '../../../store/authStore';

export function DashboardScreen(): React.JSX.Element {
  const { data, isLoading, isError, error, refetch, isRefetching } = useHome();
  const logout = useLogout();
  const user = useAuthStore((s) => s.user);
  const showSip = Constants.expoConfig?.extra?.showSip;

  const onRefresh = useCallback(() => {
    void refetch();
  }, [refetch]);

  if (isLoading) return <LoadingOverlay />;
  if (isError)
    return (
      <ErrorView
        message={error instanceof Error ? error.message : 'Failed to load'}
        onRetry={() => void refetch()}
      />
    );
  if (!data) return <EmptyState title="No data" />;

  const s = data.summary;

  return (
    <ScrollView
      contentContainerStyle={styles.wrap}
      refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={onRefresh} />}
    >
      <Text variant="headlineSmall">Hi, {user?.name ?? data.user.name}</Text>
      <Text variant="bodySmall" style={styles.muted}>
        Last login {fmtDate(data.user.lastLoginAt ?? null)}
      </Text>

      <Card style={styles.card}>
        <Card.Title title="Portfolio Summary" />
        <Card.Content>
          <Row label="Profiles" value={s.profileCount} />
          <Row label="Holdings" value={s.holdingCount} />
          <Row label="Transactions" value={s.transactionCount} />
          {showSip && <Row label="Active SIPs" value={s.activeSIPCount} />}
          <Divider style={styles.divider} />
          <RowMoney label="Total Investment" value={s.totalInvestment} />
          <RowMoney label="Market Value" value={s.totalMarketValue} />
          <RowMoney label="Unrealized P&L" value={s.totalUnrealizedPnL} pnl />
        </Card.Content>
      </Card>

      <Text variant="titleMedium" style={styles.section}>
        Profiles
      </Text>
      {data.profiles.length === 0 ? (
        <EmptyState title="No profiles yet" hint="Create one in the Profiles tab." />
      ) : (
        data.profiles.map((p) => (
          <Card key={p.id} style={styles.card}>
            <Card.Title title={p.name} subtitle={p.baseCurrency} />
            <Card.Content>
              <Text variant="bodySmall" style={styles.muted}>
                {p.description || 'No description'}
              </Text>
              <Divider style={styles.divider} />
              <Row label="Holdings" value={p.holdings.length} />
              <Row label="Transactions" value={p.transactions.length} />
              {showSip && <Row label="SIP Plans" value={p.sipPlans.length} />}
              {p.latestPerformance ? (
                <>
                  <Divider style={styles.divider} />
                  <RowMoney
                    label="Current Value"
                    value={p.latestPerformance.currentValue}
                    currency={p.baseCurrency}
                  />
                  <RowMoney
                    label="Total Return"
                    value={p.latestPerformance.totalReturn}
                    currency={p.baseCurrency}
                    pnl
                  />
                </>
              ) : null}
            </Card.Content>
          </Card>
        ))
      )}

      <Button
        mode="outlined"
        style={styles.logout}
        loading={logout.isPending}
        onPress={() => logout.mutate()}
      >
        Sign out
      </Button>
    </ScrollView>
  );
}

function Row({ label, value }: { label: string; value: number | string }): React.JSX.Element {
  return (
    <View style={styles.row}>
      <Text variant="bodyMedium">{label}</Text>
      <Text variant="bodyMedium">{value}</Text>
    </View>
  );
}

function RowMoney({
  label,
  value,
  currency,
  pnl,
}: {
  label: string;
  value: number;
  currency?: string;
  pnl?: boolean;
}): React.JSX.Element {
  return (
    <View style={styles.row}>
      <Text variant="bodyMedium">{label}</Text>
      <MoneyText value={value} currency={currency} pnl={pnl} />
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { padding: 16 },
  muted: { opacity: 0.7 },
  card: { marginVertical: 8 },
  section: { marginTop: 16, marginBottom: 4 },
  row: { flexDirection: 'row', justifyContent: 'space-between', marginVertical: 4 },
  divider: { marginVertical: 8 },
  logout: { marginTop: 24 },
});
