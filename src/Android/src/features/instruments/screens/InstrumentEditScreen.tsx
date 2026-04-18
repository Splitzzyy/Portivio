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
  useAssetTypes,
  useCreateInstrument,
  useInstruments,
  useUpdateInstrument,
} from '../../../queries/instruments';

const schema = z.object({
  assetTypeId: z.string().min(1, 'Pick an asset type'),
  name: z.string().min(1, 'Name required'),
  symbol: z.string().min(1, 'Symbol required'),
  currency: z.string().min(1, 'Currency required'),
});
type Form = z.infer<typeof schema>;

export function InstrumentEditScreen({
  route,
  navigation,
}: MoreScreenProps<'InstrumentEdit'>): React.JSX.Element {
  const instrumentId = route.params?.instrumentId;
  const { data: instruments } = useInstruments();
  const existing = instruments?.find((i) => i.id === instrumentId);
  const { data: assetTypes } = useAssetTypes();
  const create = useCreateInstrument();
  const update = useUpdateInstrument();
  const [menuOpen, setMenuOpen] = useState(false);

  const { control, handleSubmit, reset, setValue, watch, formState: { isSubmitting } } =
    useForm<Form>({
      resolver: zodResolver(schema),
      defaultValues: { assetTypeId: '', name: '', symbol: '', currency: 'INR' },
    });

  useEffect(() => {
    if (existing) {
      reset({
        assetTypeId: existing.assetTypeId,
        name: existing.name,
        symbol: existing.symbol,
        currency: existing.currency,
      });
    }
  }, [existing, reset]);

  const selectedAtId = watch('assetTypeId');
  const selectedAt = assetTypes?.find((a) => a.id === selectedAtId);

  const onSubmit = handleSubmit(async (data) => {
    try {
      if (instrumentId) {
        await update.mutateAsync({
          id: instrumentId,
          req: { name: data.name, symbol: data.symbol, currency: data.currency },
        });
      } else {
        await create.mutateAsync(data);
      }
      Toast.show({ type: 'success', text1: instrumentId ? 'Updated' : 'Created' });
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
      <Text variant="bodySmall" style={styles.label}>Asset Type</Text>
      <Menu
        visible={menuOpen}
        onDismiss={() => setMenuOpen(false)}
        anchor={
          <Button
            mode="outlined"
            onPress={() => setMenuOpen(true)}
            disabled={!!instrumentId}
            style={styles.picker}
          >
            {selectedAt ? selectedAt.name : 'Select asset type'}
          </Button>
        }
      >
        {(assetTypes ?? []).map((a) => (
          <Menu.Item
            key={a.id}
            title={a.name}
            onPress={() => {
              setValue('assetTypeId', a.id, { shouldValidate: true });
              setMenuOpen(false);
            }}
          />
        ))}
      </Menu>

      <ControlledTextInput name="name" control={control} label="Name" />
      <ControlledTextInput name="symbol" control={control} label="Symbol" autoCapitalize="characters" />
      <ControlledTextInput name="currency" control={control} label="Currency" autoCapitalize="characters" />

      <Button
        mode="contained"
        onPress={onSubmit}
        loading={isSubmitting || create.isPending || update.isPending}
        disabled={isSubmitting || create.isPending || update.isPending}
      >
        {instrumentId ? 'Save' : 'Create'}
      </Button>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  wrap: { padding: 16 },
  label: { opacity: 0.7, marginBottom: 4 },
  picker: { alignSelf: 'flex-start', marginBottom: 12 },
});
