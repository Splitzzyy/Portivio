import React from 'react';
import { FlatList, RefreshControl, StyleSheet } from 'react-native';
import { List } from 'react-native-paper';

import type { TransactionsScreenProps } from '../../../navigation/types';
import { useProfiles } from '../../../queries/profiles';
import { LoadingOverlay } from '../../../components/feedback/LoadingOverlay';
import { ErrorView } from '../../../components/feedback/ErrorView';
import { EmptyState } from '../../../components/feedback/EmptyState';

export function TransactionsProfilePicker({
  navigation,
}: TransactionsScreenProps<'ProfilePicker'>): React.JSX.Element {
  const { data, isLoading, isError, error, refetch, isRefetching } = useProfiles();

  if (isLoading) return <LoadingOverlay />;
  if (isError)
    return (
      <ErrorView
        message={error instanceof Error ? error.message : 'Failed'}
        onRetry={() => void refetch()}
      />
    );

  return (
    <FlatList
      data={data ?? []}
      keyExtractor={(p) => p.id}
      contentContainerStyle={styles.list}
      refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={() => void refetch()} />}
      ListEmptyComponent={<EmptyState title="No profiles" hint="Create one in the Profiles tab." />}
      renderItem={({ item }) => (
        <List.Item
          title={item.name}
          description={item.baseCurrency}
          left={(p) => <List.Icon {...p} icon="cash-multiple" />}
          onPress={() => navigation.navigate('TransactionsList', { profileId: item.id })}
        />
      )}
    />
  );
}

const styles = StyleSheet.create({
  list: { paddingVertical: 8 },
});
