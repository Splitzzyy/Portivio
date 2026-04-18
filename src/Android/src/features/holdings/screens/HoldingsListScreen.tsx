import React from 'react';
import { FlatList, RefreshControl, StyleSheet, View } from 'react-native';
import { Card, FAB, IconButton, Text } from 'react-native-paper';
import Toast from 'react-native-toast-message';

import type { HoldingsScreenProps } from '../../../navigation/types';
import { useDeleteHolding, useHoldings } from '../../../queries/holdings';
import { LoadingOverlay } from '../../../components/feedback/LoadingOverlay';
import { ErrorView } from '../../../components/feedback/ErrorView';
import { EmptyState } from '../../../components/feedback/EmptyState';
import { MoneyText } from '../../../components/data/MoneyText';

export function HoldingsListScreen({
  route,
  navigation,
}: HoldingsScreenProps<'HoldingsList'>): React.JSX.Element {
  const { profileId } = route.params;
  const { data, isLoading, isError, error, refetch, isRefetching } = useHoldings(profileId);
  const del = useDeleteHolding(profileId);

  if (isLoading) return <LoadingOverlay />;
  if (isError)
    return (
      <ErrorView
        message={error instanceof Error ? error.message : 'Failed'}
        onRetry={() => void refetch()}
      />
    );

  return (
    <View style={{ flex: 1 }}>
      <FlatList
        data={data ?? []}
        keyExtractor={(h) => h.id}
        contentContainerStyle={styles.list}
        refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={() => void refetch()} />}
        ListEmptyComponent={<EmptyState title="No holdings" hint="Tap + to add one." />}
        renderItem={({ item }) => (
          <Card style={styles.card}>
            <Card.Title
              title={`${item.instrumentSymbol} — ${item.instrumentName}`}
              subtitle={`${item.assetTypeName} · ${item.currency}`}
              right={() => (
                <View style={{ flexDirection: 'row' }}>
                  <IconButton
                    icon="pencil"
                    onPress={() =>
                      navigation.navigate('HoldingEdit', {
                        profileId,
                        holdingId: item.id,
                      })
                    }
                  />
                  <IconButton
                    icon="delete"
                    onPress={async () => {
                      try {
                        await del.mutateAsync(item.id);
                        Toast.show({ type: 'success', text1: 'Deleted' });
                      } catch (e: unknown) {
                        Toast.show({
                          type: 'error',
                          text1: 'Delete failed',
                          text2: e instanceof Error ? e.message : '',
                        });
                      }
                    }}
                  />
                </View>
              )}
            />
            <Card.Content>
              <Row label="Qty" value={item.quantity.toString()} />
              <Row label="Avg Price" valueComp={<MoneyText value={item.avgPrice} currency={item.currency} />} />
              <Row label="Current" valueComp={<MoneyText value={item.currentPrice} currency={item.currency} />} />
              <Row label="Mkt Value" valueComp={<MoneyText value={item.marketValue} currency={item.currency} />} />
              <Row label="P&L" valueComp={<MoneyText value={item.unrealizedPnL} currency={item.currency} pnl />} />
            </Card.Content>
          </Card>
        )}
      />
      <FAB
        icon="plus"
        onPress={() => navigation.navigate('HoldingEdit', { profileId })}
        style={styles.fab}
      />
    </View>
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
  list: { padding: 16, paddingBottom: 96 },
  card: { marginBottom: 12 },
  fab: { position: 'absolute', right: 16, bottom: 16 },
  row: { flexDirection: 'row', justifyContent: 'space-between', marginVertical: 2 },
});
