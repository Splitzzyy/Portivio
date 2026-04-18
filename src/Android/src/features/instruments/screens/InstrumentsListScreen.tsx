import React from 'react';
import { FlatList, RefreshControl, StyleSheet, View } from 'react-native';
import { Card, FAB, IconButton } from 'react-native-paper';
import Toast from 'react-native-toast-message';

import type { MoreScreenProps } from '../../../navigation/types';
import { useDeleteInstrument, useInstruments } from '../../../queries/instruments';
import { LoadingOverlay } from '../../../components/feedback/LoadingOverlay';
import { ErrorView } from '../../../components/feedback/ErrorView';
import { EmptyState } from '../../../components/feedback/EmptyState';

export function InstrumentsListScreen({
  navigation,
}: MoreScreenProps<'InstrumentsList'>): React.JSX.Element {
  const { data, isLoading, isError, error, refetch, isRefetching } = useInstruments();
  const del = useDeleteInstrument();

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
        keyExtractor={(i) => i.id}
        contentContainerStyle={styles.list}
        refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={() => void refetch()} />}
        ListEmptyComponent={<EmptyState title="No instruments" hint="Tap + to add one." />}
        renderItem={({ item }) => (
          <Card style={styles.card}>
            <Card.Title
              title={`${item.symbol} — ${item.name}`}
              subtitle={`${item.assetTypeName} · ${item.currency}`}
              right={() => (
                <View style={{ flexDirection: 'row' }}>
                  <IconButton
                    icon="pencil"
                    onPress={() =>
                      navigation.navigate('InstrumentEdit', { instrumentId: item.id })
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
          </Card>
        )}
      />
      <FAB
        icon="plus"
        onPress={() => navigation.navigate('InstrumentEdit', {})}
        style={styles.fab}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  list: { padding: 16, paddingBottom: 96 },
  card: { marginBottom: 12 },
  fab: { position: 'absolute', right: 16, bottom: 16 },
});
