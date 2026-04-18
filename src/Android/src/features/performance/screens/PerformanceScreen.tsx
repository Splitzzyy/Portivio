import React, { Suspense, lazy, useState } from 'react';
import { RefreshControl, ScrollView, StyleSheet, View } from 'react-native';
import { Button, Card, Divider, SegmentedButtons, Text } from 'react-native-paper';
import Toast from 'react-native-toast-message';

import type { MoreScreenProps } from '../../../navigation/types';
import {
  usePerformanceHistory,
  useRecordSnapshot,
} from '../../../queries/performance';
import { LoadingOverlay } from '../../../components/feedback/LoadingOverlay';
import { ErrorView } from '../../../components/feedback/ErrorView';
import { MoneyText } from '../../../components/data/MoneyText';
import { fmtDate } from '../../../utils/dates';

const PerformanceChart = lazy(() => import('../components/PerformanceChart'));

export function PerformanceScreen({
  route,
}: MoreScreenProps<'PerformanceScreen'>): React.JSX.Element {
  const { profileId } = route.params;
  const [days, setDays] = useState('90');
  const { data, isLoading, isError, error, refetch, isRefetching } = usePerformanceHistory(
    profileId,
    Number(days),
  );
  const snap = useRecordSnapshot(profileId);

  if (isLoading) return <LoadingOverlay />;
  if (isError)
    return (
      <ErrorView
        message={error instanceof Error ? error.message : 'Failed'}
        onRetry={() => void refetch()}
      />
    );

  const latest = data?.latest ?? null;
  const history = data?.history ?? [];

  return (
    <ScrollView
      contentContainerStyle={styles.wrap}
      refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={() => void refetch()} />}
    >
      <SegmentedButtons
        value={days}
        onValueChange={setDays}
        buttons={[
          { value: '30', label: '30d' },
          { value: '90', label: '90d' },
          { value: '180', label: '180d' },
          { value: '365', label: '1y' },
        ]}
      />

      {latest ? (
        <Card style={styles.card}>
          <Card.Title title="Latest Snapshot" subtitle={fmtDate(latest.date)} />
          <Card.Content>
            <Row label="Total Investment" valueComp={<MoneyText value={latest.totalInvestment} />} />
            <Row label="Current Value" valueComp={<MoneyText value={latest.currentValue} />} />
            <Row label="Day Change" valueComp={<MoneyText value={latest.dayChange} pnl />} />
            <Row label="Total Return" valueComp={<MoneyText value={latest.totalReturn} pnl />} />
            <Row label="XIRR" value={`${(latest.xirr * 100).toFixed(2)}%`} />
          </Card.Content>
        </Card>
      ) : null}

      <Suspense fallback={<LoadingOverlay />}>
        {history.length ? (
          <Card style={styles.card}>
            <Card.Title title="Trend" />
            <Card.Content>
              <PerformanceChart history={history} />
            </Card.Content>
          </Card>
        ) : (
          <Text style={styles.muted}>No history yet — record a snapshot below.</Text>
        )}
      </Suspense>

      <Divider style={{ marginVertical: 12 }} />
      <Button
        mode="contained"
        loading={snap.isPending}
        onPress={async () => {
          try {
            await snap.mutateAsync();
            Toast.show({ type: 'success', text1: 'Snapshot recorded' });
            void refetch();
          } catch (e: unknown) {
            Toast.show({
              type: 'error',
              text1: 'Snapshot failed',
              text2: e instanceof Error ? e.message : '',
            });
          }
        }}
      >
        Record Snapshot Now
      </Button>
    </ScrollView>
  );
}

function Row({
  label,
  value,
  valueComp,
}: {
  label: string;
  value?: string;
  valueComp?: React.ReactNode;
}): React.JSX.Element {
  return (
    <View style={styles.row}>
      <Text variant="bodyMedium">{label}</Text>
      {valueComp ?? <Text variant="bodyMedium">{value}</Text>}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { padding: 16 },
  card: { marginVertical: 8 },
  row: { flexDirection: 'row', justifyContent: 'space-between', marginVertical: 2 },
  muted: { opacity: 0.7, textAlign: 'center', marginVertical: 24 },
});
