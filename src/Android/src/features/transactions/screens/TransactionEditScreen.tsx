import React, { useEffect, useState } from 'react';
import { ScrollView, StyleSheet } from 'react-native';
import { Button, Menu, SegmentedButtons, Text } from 'react-native-paper';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import Toast from 'react-native-toast-message';

import type { TransactionsScreenProps } from '../../../navigation/types';
import { ControlledTextInput } from '../../../components/forms/ControlledTextInput';
import {
  useCreateTransaction,
  useTransactions,
  useUpdateTransaction,
} from '../../../queries/transactions';
import { useInstruments } from '../../../queries/instruments';
import { todayIso } from '../../../utils/dates';

const schema = z.object({
  instrumentId: z.string().min(1, 'Pick an instrument'),
  type: z.string().min(1),
  quantity: z.coerce.number().positive(),
  price: z.coerce.number().nonnegative(),
  amount: z.coerce.number().nonnegative(),
  transactionDate: z.string().min(1),
  notes: z.string().optional().default(''),
});
type Form = z.infer<typeof schema>;

export function TransactionEditScreen({
  route,
  navigation,
}: TransactionsScreenProps<'TransactionEdit'>): React.JSX.Element {
  const { profileId, transactionId } = route.params;
  const { data: pages } = useTransactions(profileId);
  const existing = pages?.pages.flatMap((p) => p.items).find((t) => t.id === transactionId);
  const { data: instruments } = useInstruments();
  const create = useCreateTransaction(profileId);
  const update = useUpdateTransaction(profileId);
  const [menuOpen, setMenuOpen] = useState(false);

  const { control, handleSubmit, reset, setValue, watch, formState: { isSubmitting } } =
    useForm<Form>({
      resolver: zodResolver(schema),
      defaultValues: {
        instrumentId: '',
        type: 'Buy',
        quantity: 0,
        price: 0,
        amount: 0,
        transactionDate: todayIso().slice(0, 10),
        notes: '',
      },
    });

  useEffect(() => {
    if (existing) {
      reset({
        instrumentId: existing.instrumentId,
        type: existing.type,
        quantity: existing.quantity,
        price: existing.price,
        amount: existing.amount,
        transactionDate: existing.transactionDate.slice(0, 10),
        notes: existing.notes,
      });
    }
  }, [existing, reset]);

  const selectedId = watch('instrumentId');
  const selected = instruments?.find((i) => i.id === selectedId);
  const txType = watch('type');

  const onSubmit = handleSubmit(async (data) => {
    const dateIso = new Date(`${data.transactionDate}T00:00:00Z`).toISOString();
    try {
      if (transactionId) {
        await update.mutateAsync({
          id: transactionId,
          req: {
            quantity: data.quantity,
            price: data.price,
            amount: data.amount,
            transactionDate: dateIso,
            notes: data.notes ?? '',
          },
        });
      } else {
        await create.mutateAsync({
          instrumentId: data.instrumentId,
          type: data.type,
          quantity: data.quantity,
          price: data.price,
          amount: data.amount,
          transactionDate: dateIso,
          notes: data.notes ?? '',
        });
      }
      Toast.show({ type: 'success', text1: transactionId ? 'Updated' : 'Created' });
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
          <Button
            mode="outlined"
            onPress={() => setMenuOpen(true)}
            disabled={!!transactionId}
            style={styles.picker}
          >
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
      </Menu>

      <Text variant="bodySmall" style={[styles.label, { marginTop: 12 }]}>Type</Text>
      <SegmentedButtons
        value={txType}
        onValueChange={(v) => setValue('type', v, { shouldValidate: true })}
        buttons={[
          { value: 'Buy', label: 'Buy' },
          { value: 'Sell', label: 'Sell' },
        ]}
        style={{ marginBottom: 12 }}
      />

      <ControlledTextInput
        name="quantity"
        control={control}
        label="Quantity"
        keyboardType="decimal-pad"
      />
      <ControlledTextInput
        name="price"
        control={control}
        label="Price"
        keyboardType="decimal-pad"
      />
      <ControlledTextInput
        name="amount"
        control={control}
        label="Amount"
        keyboardType="decimal-pad"
      />
      <ControlledTextInput
        name="transactionDate"
        control={control}
        label="Date (YYYY-MM-DD)"
      />
      <ControlledTextInput name="notes" control={control} label="Notes" multiline numberOfLines={2} />
      <Button
        mode="contained"
        onPress={onSubmit}
        loading={isSubmitting || create.isPending || update.isPending}
        disabled={isSubmitting || create.isPending || update.isPending}
      >
        {transactionId ? 'Save' : 'Create'}
      </Button>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  wrap: { padding: 16 },
  label: { opacity: 0.7, marginBottom: 4 },
  picker: { alignSelf: 'flex-start' },
});
