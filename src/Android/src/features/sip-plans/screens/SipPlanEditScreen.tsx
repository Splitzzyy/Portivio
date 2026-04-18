import React, { useEffect, useState } from 'react';
import { ScrollView, StyleSheet } from 'react-native';
import { Button, Menu, Text } from 'react-native-paper';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import Toast from 'react-native-toast-message';

import type { MoreScreenProps } from '../../../navigation/types';
import { ControlledTextInput } from '../../../components/forms/ControlledTextInput';
import {
  useCreateSipPlan,
  useSipPlans,
  useUpdateSipPlan,
} from '../../../queries/sipPlans';
import { useInstruments } from '../../../queries/instruments';

const schema = z.object({
  instrumentId: z.string().min(1, 'Pick an instrument'),
  amount: z.coerce.number().positive(),
  sipDay: z.coerce.number().int().min(1).max(28),
  startDate: z.string().min(1),
  endDate: z.string().min(1),
});
type Form = z.infer<typeof schema>;

export function SipPlanEditScreen({
  route,
  navigation,
}: MoreScreenProps<'SipPlanEdit'>): React.JSX.Element {
  const { profileId, sipId } = route.params;
  const { data: sips } = useSipPlans(profileId);
  const existing = sips?.find((s) => s.id === sipId);
  const { data: instruments } = useInstruments();
  const create = useCreateSipPlan(profileId);
  const update = useUpdateSipPlan(profileId);
  const [menuOpen, setMenuOpen] = useState(false);

  const { control, handleSubmit, reset, setValue, watch, formState: { isSubmitting } } =
    useForm<Form>({
      resolver: zodResolver(schema),
      defaultValues: {
        instrumentId: '',
        amount: 0,
        sipDay: 1,
        startDate: new Date().toISOString().slice(0, 10),
        endDate: new Date(Date.now() + 365 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10),
      },
    });

  useEffect(() => {
    if (existing) {
      reset({
        instrumentId: existing.instrumentId,
        amount: existing.amount,
        sipDay: existing.sipDay,
        startDate: existing.startDate.slice(0, 10),
        endDate: existing.endDate.slice(0, 10),
      });
    }
  }, [existing, reset]);

  const selectedId = watch('instrumentId');
  const selected = instruments?.find((i) => i.id === selectedId);

  const onSubmit = handleSubmit(async (data) => {
    const startIso = new Date(`${data.startDate}T00:00:00Z`).toISOString();
    const endIso = new Date(`${data.endDate}T00:00:00Z`).toISOString();
    try {
      if (sipId) {
        await update.mutateAsync({
          id: sipId,
          req: { amount: data.amount, sipDay: data.sipDay, startDate: startIso, endDate: endIso },
        });
      } else {
        await create.mutateAsync({
          instrumentId: data.instrumentId,
          amount: data.amount,
          sipDay: data.sipDay,
          startDate: startIso,
          endDate: endIso,
        });
      }
      Toast.show({ type: 'success', text1: sipId ? 'Updated' : 'Created' });
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
            disabled={!!sipId}
            onPress={() => setMenuOpen(true)}
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

      <ControlledTextInput
        name="amount"
        control={control}
        label="Monthly Amount"
        keyboardType="decimal-pad"
      />
      <ControlledTextInput
        name="sipDay"
        control={control}
        label="SIP Day (1–28)"
        keyboardType="number-pad"
      />
      <ControlledTextInput
        name="startDate"
        control={control}
        label="Start Date (YYYY-MM-DD)"
      />
      <ControlledTextInput
        name="endDate"
        control={control}
        label="End Date (YYYY-MM-DD)"
      />
      <Button
        mode="contained"
        onPress={onSubmit}
        loading={isSubmitting || create.isPending || update.isPending}
        disabled={isSubmitting || create.isPending || update.isPending}
      >
        {sipId ? 'Save' : 'Create'}
      </Button>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  wrap: { padding: 16 },
  label: { opacity: 0.7, marginBottom: 4 },
  picker: { alignSelf: 'flex-start', marginBottom: 12 },
});
