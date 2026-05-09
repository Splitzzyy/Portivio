import React, { useMemo } from 'react';
import { FlatList, RefreshControl, StyleSheet, View } from 'react-native';
import { Card, FAB, IconButton, Text } from 'react-native-paper';
import Toast from 'react-native-toast-message';

import type { TransactionsScreenProps } from '../../../navigation/types';
import {
  useDeleteTransaction,
  useTransactions,
} from '../../../queries/transactions';
import { LoadingOverlay } from '../../../components/feedback/LoadingOverlay';
import { ErrorView } from '../../../components/feedback/ErrorView';
import { EmptyState } from '../../../components/feedback/EmptyState';
import { MoneyText } from '../../../components/data/MoneyText';
import { fmtDate } from '../../../utils/dates';

export function TransactionsListScreen({
  route,
  navigation,
}: TransactionsScreenProps<'TransactionsList'>): React.JSX.Element {
  const { profileId } = route.params;
  const {
    data,
    isLoading,
    isError,
    error,
    refetch,
    isRefetching,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
  } = useTransactions(profileId);
  const del = useDeleteTransaction(profileId);

  const items = useMemo(() => data?.pages.flatMap((p) => p.items) ?? [], [data]);

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
        data={items}
        keyExtractor={(t) => t.id}
        contentContainerStyle={styles.list}
        refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={() => void refetch()} />}
        onEndReachedThreshold={0.5}
        onEndReached={() => {
          if (hasNextPage && !isFetchingNextPage) void fetchNextPage();
        }}
        ListEmptyComponent={<EmptyState title="No transactions" hint="Tap + to record one." />}
        renderItem={({ item }) => (
          <Card style={styles.card}>
            <Card.Title
              title={`${item.type} · ${item.instrumentSymbol}`}
              subtitle={fmtDate(item.transactionDate)}
              right={() => (
                <View style={{ flexDirection: 'row' }}>
                  <IconButton
                    icon="pencil"
                    onPress={() =>
                      navigation.navigate('TransactionEdit', {
                        profileId,
                        transactionId: item.id,
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
              <Row label="Qty" value={String(item.quantity)} />
              <Row label="Price" valueComp={<MoneyText value={item.price} />} />
              <Row label="Amount" valueComp={<MoneyText value={item.amount} />} />
              <Row label="Logged on" value={fmtDate(item.createdAtUtc)} />
              {item.notes ? <Text variant="bodySmall" style={styles.notes}>{item.notes}</Text> : null}
            </Card.Content>
          </Card>
        )}
        ListFooterComponent={
          isFetchingNextPage ? (
            <Text style={styles.loading}>Loading more…</Text>
          ) : null
        }
      />
      <FAB
        icon="plus"
        onPress={() => navigation.navigate('TransactionEdit', { profileId })}
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
  notes: { opacity: 0.7, marginTop: 4 },
  loading: { textAlign: 'center', padding: 12, opacity: 0.6 },
});
