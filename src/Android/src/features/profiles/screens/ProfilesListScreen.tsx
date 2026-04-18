import React from 'react';
import { FlatList, RefreshControl, StyleSheet, View } from 'react-native';
import { Card, FAB, IconButton, Text } from 'react-native-paper';
import Toast from 'react-native-toast-message';

import type { ProfilesScreenProps } from '../../../navigation/types';
import { useDeleteProfile, useProfiles } from '../../../queries/profiles';
import { LoadingOverlay } from '../../../components/feedback/LoadingOverlay';
import { ErrorView } from '../../../components/feedback/ErrorView';
import { EmptyState } from '../../../components/feedback/EmptyState';
import { fmtDate } from '../../../utils/dates';

export function ProfilesListScreen({
  navigation,
}: ProfilesScreenProps<'ProfilesList'>): React.JSX.Element {
  const { data, isLoading, isError, error, refetch, isRefetching } = useProfiles();
  const del = useDeleteProfile();

  if (isLoading) return <LoadingOverlay />;
  if (isError)
    return (
      <ErrorView
        message={error instanceof Error ? error.message : 'Failed'}
        onRetry={() => void refetch()}
      />
    );

  const profiles = data ?? [];

  return (
    <View style={{ flex: 1 }}>
      <FlatList
        data={profiles}
        keyExtractor={(p) => p.id}
        contentContainerStyle={styles.list}
        refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={() => void refetch()} />}
        ListEmptyComponent={<EmptyState title="No profiles" hint="Tap + to create one." />}
        renderItem={({ item }) => (
          <Card style={styles.card}>
            <Card.Title
              title={item.name}
              subtitle={`${item.baseCurrency} · ${fmtDate(item.createdAt)}`}
              right={() => (
                <View style={{ flexDirection: 'row' }}>
                  <IconButton
                    icon="pencil"
                    onPress={() =>
                      navigation.navigate('ProfileEdit', { profileId: item.id })
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
            {item.description ? (
              <Card.Content>
                <Text variant="bodyMedium">{item.description}</Text>
              </Card.Content>
            ) : null}
          </Card>
        )}
      />
      <FAB
        icon="plus"
        onPress={() => navigation.navigate('ProfileEdit', {})}
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
