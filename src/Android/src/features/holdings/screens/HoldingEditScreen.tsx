import React, { useEffect, useState } from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';
import { Button, List, Menu, Text } from 'react-native-paper';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import Toast from 'react-native-toast-message';

import type { HoldingsScreenProps } from '../../../navigation/types';
import { ControlledTextInput } from '../../../components/forms/ControlledTextInput';
import { useHoldings, useUpsertHolding } from '../../../queries/holdings';
import { useInstruments } from '../../../queries/instruments';

const schema = z.object({
  instrumentId: z.string().min(1, 'Pick an instrument'),
  quantity: z.coerce.number().positive('Must be > 0'),
  avgPrice: z.coerce.number().nonnegative('Cannot be negative'),
  currentPrice: z.coerce.number().nonnegative('Cannot be negative'),
});
type Form = z.infer<typeof schema>;

export function HoldingEditScreen({
  route,
  navigation,
}: HoldingsScreenProps<'HoldingEdit'>): React.JSX.Element {
  const { profileId, holdingId } = route.params;
  const { data: holdings } = useHoldings(profileId);
  const existing = holdings?.find((h) => h.id === holdingId);
  const { data: instruments } = useInstruments();
  const upsert = useUpsertHolding(profileId);
  const [menuOpen, setMenuOpen] = useState(false);

  const { control, handleSubmit, reset, watch, setValue, formState: { isSubmitting } } =
    useForm<Form>({
      resolver: zodResolver(schema),
      defaultValues: { instrumentId: '', quantity: 0, avgPrice: 0, currentPrice: 0 },
    });

  useEffect(() => {
    if (existing) {
      reset({
        instrumentId: existing.instrumentId,
        quantity: existing.quantity,
        avgPrice: existing.avgPrice,
        currentPrice: existing.currentPrice,
      });
    }
  }, [existing, reset]);

  const selectedId = watch('instrumentId');
  const selected = instruments?.find((i) => i.id === selectedId);

  const onSubmit = handleSubmit(async (data) => {
    try {
      await upsert.mutateAsync(data);
      Toast.show({ type: 'success', text1: holdingId ? 'Updated' : 'Created' });
      navigation.goBack();
    } catch (e: unknown) {
      Toast.show({
        type: 'error',
        text1: 'Save failed',
        text2: e instanceof Error ? e.message : '',
      });
    }
  });

  return (
    <ScrollView contentContainerStyle={styles.wrap}>
      <Text variant="bodySmall" style={styles.label}>Instrument</Text>
      <Menu
        visible={menuOpen}
        onDismiss={() => setMenuOpen(false)}
        anchor={
          <Button mode="outlined" onPress={() => setMenuOpen(true)} style={styles.picker}>
            {selected ? `${selected.symbol} — ${selected.name}` : 'Select instrument'}
          </Button>
        }
      >
        {(instruments ?? []).map((i) => (
          <Menu.Item
            key={i.id}
            title={`${i.symbol} — ${i.name}`}
            onPress={() => {
              setValue('instrumentId', i.id, { shouldValidate: true });
              setMenuOpen(false);
            }}
          />
        ))}
        {!instruments?.length ? (
          <List.Item title="No instruments" description="Add one in More → Instruments" />
        ) : null}
      </Menu>

      <View style={{ height: 12 }} />
      <ControlledTextInput
        name="quantity"
        control={control}
        label="Quantity"
        keyboardType="decimal-pad"
      />
      <ControlledTextInput
        name="avgPrice"
        control={control}
        label="Avg Price"
        keyboardType="decimal-pad"
      />
      <ControlledTextInput
        name="currentPrice"
        control={control}
        label="Current Price"
        keyboardType="decimal-pad"
      />
      <Button
        mode="contained"
        onPress={onSubmit}
        loading={isSubmitting || upsert.isPending}
        disabled={isSubmitting || upsert.isPending}
      >
        {holdingId ? 'Save' : 'Create'}
      </Button>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  wrap: { padding: 16 },
  label: { opacity: 0.7, marginBottom: 4 },
  picker: { alignSelf: 'flex-start' },
});
