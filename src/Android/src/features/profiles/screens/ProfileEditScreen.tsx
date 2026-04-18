import React, { useEffect } from 'react';
import { ScrollView, StyleSheet } from 'react-native';
import { Button } from 'react-native-paper';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import Toast from 'react-native-toast-message';

import type { ProfilesScreenProps } from '../../../navigation/types';
import { ControlledTextInput } from '../../../components/forms/ControlledTextInput';
import { useCreateProfile, useProfiles, useUpdateProfile } from '../../../queries/profiles';

const schema = z.object({
  name: z.string().min(1, 'Name required'),
  baseCurrency: z.string().min(1, 'Currency required').max(8),
  description: z.string().max(500).optional().default(''),
});
type Form = z.infer<typeof schema>;

export function ProfileEditScreen({
  route,
  navigation,
}: ProfilesScreenProps<'ProfileEdit'>): React.JSX.Element {
  const profileId = route.params?.profileId;
  const { data: profiles } = useProfiles();
  const existing = profiles?.find((p) => p.id === profileId);

  const create = useCreateProfile();
  const update = useUpdateProfile();

  const { control, handleSubmit, reset, formState: { isSubmitting } } = useForm<Form>({
    resolver: zodResolver(schema),
    defaultValues: { name: '', baseCurrency: 'INR', description: '' },
  });

  useEffect(() => {
    if (existing) {
      reset({
        name: existing.name,
        baseCurrency: existing.baseCurrency,
        description: existing.description,
      });
    }
  }, [existing, reset]);

  const onSubmit = handleSubmit(async (data) => {
    try {
      if (profileId) await update.mutateAsync({ id: profileId, req: { ...data, description: data.description ?? '' } });
      else await create.mutateAsync({ ...data, description: data.description ?? '' });
      Toast.show({ type: 'success', text1: profileId ? 'Updated' : 'Created' });
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
      <ControlledTextInput name="name" control={control} label="Name" />
      <ControlledTextInput
        name="baseCurrency"
        control={control}
        label="Base Currency (e.g. INR, USD)"
        autoCapitalize="characters"
      />
      <ControlledTextInput
        name="description"
        control={control}
        label="Description"
        multiline
        numberOfLines={3}
      />
      <Button
        mode="contained"
        onPress={onSubmit}
        loading={isSubmitting || create.isPending || update.isPending}
        disabled={isSubmitting || create.isPending || update.isPending}
      >
        {profileId ? 'Save' : 'Create'}
      </Button>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  wrap: { padding: 16 },
});
