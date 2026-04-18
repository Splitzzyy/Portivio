import React, { useState } from 'react';
import { FlatList, RefreshControl, StyleSheet, View } from 'react-native';
import { Button, Dialog, IconButton, List, Portal, TextInput } from 'react-native-paper';
import Toast from 'react-native-toast-message';

import {
  useAssetTypes,
  useCreateAssetType,
  useDeleteAssetType,
} from '../../../queries/instruments';
import { LoadingOverlay } from '../../../components/feedback/LoadingOverlay';
import { ErrorView } from '../../../components/feedback/ErrorView';
import { EmptyState } from '../../../components/feedback/EmptyState';

export function AssetTypesListScreen(): React.JSX.Element {
  const { data, isLoading, isError, error, refetch, isRefetching } = useAssetTypes();
  const create = useCreateAssetType();
  const del = useDeleteAssetType();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [name, setName] = useState('');

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
        keyExtractor={(a) => a.id}
        refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={() => void refetch()} />}
        ListEmptyComponent={<EmptyState title="No asset types" hint="Add one below." />}
        renderItem={({ item }) => (
          <List.Item
            title={item.name}
            right={() => (
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
            )}
          />
        )}
      />
      <Button
        mode="contained"
        onPress={() => {
          setName('');
          setDialogOpen(true);
        }}
        style={styles.addBtn}
      >
        New Asset Type
      </Button>
      <Portal>
        <Dialog visible={dialogOpen} onDismiss={() => setDialogOpen(false)}>
          <Dialog.Title>New Asset Type</Dialog.Title>
          <Dialog.Content>
            <TextInput
              mode="outlined"
              label="Name"
              value={name}
              onChangeText={setName}
              autoFocus
            />
          </Dialog.Content>
          <Dialog.Actions>
            <Button onPress={() => setDialogOpen(false)}>Cancel</Button>
            <Button
              loading={create.isPending}
              onPress={async () => {
                if (!name.trim()) return;
                try {
                  await create.mutateAsync({ name: name.trim() });
                  setDialogOpen(false);
                  Toast.show({ type: 'success', text1: 'Created' });
                } catch (e: unknown) {
                  Toast.show({
                    type: 'error',
                    text1: 'Create failed',
                    text2: e instanceof Error ? e.message : '',
                  });
                }
              }}
            >
              Create
            </Button>
          </Dialog.Actions>
        </Dialog>
      </Portal>
    </View>
  );
}

const styles = StyleSheet.create({
  addBtn: { margin: 16 },
});
