import React from 'react';
import { ScrollView, StyleSheet } from 'react-native';
import { Button } from 'react-native-paper';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import Toast from 'react-native-toast-message';

import type { AuthScreenProps } from '../../../navigation/types';
import { ControlledTextInput } from '../../../components/forms/ControlledTextInput';
import { ResetForm, resetSchema } from '../schema';
import { useResetPassword } from '../../../queries/auth';

export function ResetPasswordScreen({
  route,
  navigation,
}: AuthScreenProps<'ResetPassword'>): React.JSX.Element {
  const { control, handleSubmit, formState: { isSubmitting } } = useForm<ResetForm>({
    resolver: zodResolver(resetSchema),
    defaultValues: {
      email: route.params?.email ?? '',
      resetToken: route.params?.token ?? '',
      newPassword: '',
      confirmPassword: '',
    },
  });
  const reset = useResetPassword();

  const onSubmit = handleSubmit(async (data) => {
    try {
      const res = await reset.mutateAsync(data);
      Toast.show({
        type: res.success ? 'success' : 'error',
        text1: res.success ? 'Password reset' : 'Failed',
        text2: res.message ?? '',
      });
      if (res.success) navigation.navigate('Login');
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : 'Network error';
      Toast.show({ type: 'error', text1: 'Failed', text2: msg });
    }
  });

  return (
    <ScrollView contentContainerStyle={styles.wrap}>
      <ControlledTextInput
        name="email"
        control={control}
        label="Email"
        autoCapitalize="none"
        keyboardType="email-address"
      />
      <ControlledTextInput name="resetToken" control={control} label="Reset Token" />
      <ControlledTextInput
        name="newPassword"
        control={control}
        label="New Password"
        secureTextEntry
      />
      <ControlledTextInput
        name="confirmPassword"
        control={control}
        label="Confirm Password"
        secureTextEntry
      />
      <Button
        mode="contained"
        onPress={onSubmit}
        loading={isSubmitting || reset.isPending}
        disabled={isSubmitting || reset.isPending}
      >
        Reset Password
      </Button>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  wrap: { padding: 24 },
});
