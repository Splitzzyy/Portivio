import React from 'react';
import { FlatList, RefreshControl, StyleSheet, View } from 'react-native';
import { Card, FAB, IconButton, Switch, Text } from 'react-native-paper';
import Toast from 'react-native-toast-message';

import type { MoreScreenProps } from '../../../navigation/types';
import {
  useDeleteSipPlan,
  useSipPlans,
  useToggleSipPlan,
} from '../../../queries/sipPlans';
import { LoadingOverlay } from '../../../components/feedback/LoadingOverlay';
import { ErrorView } from '../../../components/feedback/ErrorView';
import { EmptyState } from '../../../components/feedback/EmptyState';
import { MoneyText } from '../../../components/data/MoneyText';
import { fmtDate } from '../../../utils/dates';

export function SipPlansListScreen({
  route,
  navigation,
}: MoreScreenProps<'SipPlansList'>): React.JSX.Element {
  const { profileId } = route.params;
  const { data, isLoading, isError, error, refetch, isRefetching } = useSipPlans(profileId);
  const toggle = useToggleSipPlan(profileId);
  const del = useDeleteSipPlan(profileId);

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
        keyExtractor={(s) => s.id}
        contentContainerStyle={styles.list}
        refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={() => void refetch()} />}
        ListEmptyComponent={<EmptyState title="No SIP plans" hint="Tap + to create one." />}
        renderItem={({ item }) => (
          <Card style={styles.card}>
            <Card.Title
              title={`${item.instrumentSymbol} — ${item.instrumentName}`}
              subtitle={`Day ${item.sipDay} · ${fmtDate(item.startDate)} → ${fmtDate(item.endDate)}`}
              right={() => (
                <View style={{ flexDirection: 'row', alignItems: 'center' }}>
                  <Switch
                    value={item.isActive}
                    onValueChange={async (v) => {
                      try {
                        await toggle.mutateAsync({ id: item.id, active: v });
                      } catch (e: unknown) {
                        Toast.show({
                          type: 'error',
                          text1: 'Toggle failed',
                          text2: e instanceof Error ? e.message : '',
                        });
                      }
                    }}
                  />
                  <IconButton
                    icon="pencil"
                    onPress={() =>
                      navigation.navigate('SipPlanEdit', { profileId, sipId: item.id })
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
              <Row label="Amount" valueComp={<MoneyText value={item.amount} />} />
              <Row label="Status" value={item.isActive ? 'Active' : 'Paused'} />
            </Card.Content>
          </Card>
        )}
      />
      <FAB
        icon="plus"
        onPress={() => navigation.navigate('SipPlanEdit', { profileId })}
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
