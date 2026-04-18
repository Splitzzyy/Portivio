import React from 'react';
import { ScrollView, StyleSheet } from 'react-native';
import { Button, Text } from 'react-native-paper';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import Toast from 'react-native-toast-message';

import type { AuthScreenProps } from '../../../navigation/types';
import { ControlledTextInput } from '../../../components/forms/ControlledTextInput';
import { ForgotForm, forgotSchema } from '../schema';
import { useForgotPassword } from '../../../queries/auth';

export function ForgotPasswordScreen({
  navigation,
}: AuthScreenProps<'ForgotPassword'>): React.JSX.Element {
  const { control, handleSubmit, getValues, formState: { isSubmitting } } = useForm<ForgotForm>({
    resolver: zodResolver(forgotSchema),
    defaultValues: { email: '' },
  });
  const forgot = useForgotPassword();

  const onSubmit = handleSubmit(async (data) => {
    try {
      const res = await forgot.mutateAsync(data);
      Toast.show({
        type: res.success ? 'success' : 'error',
        text1: res.success ? 'Email sent' : 'Failed',
        text2: res.message ?? '',
      });
      if (res.success) navigation.navigate('ResetPassword', { email: data.email });
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : 'Network error';
      Toast.show({ type: 'error', text1: 'Failed', text2: msg });
    }
  });

  return (
    <ScrollView contentContainerStyle={styles.wrap}>
      <Text variant="bodyMedium" style={styles.subtitle}>
        Enter the email tied to your account. We'll send a reset token.
      </Text>
      <ControlledTextInput
        name="email"
        control={control}
        label="Email"
        autoCapitalize="none"
        keyboardType="email-address"
      />
      <Button
        mode="contained"
        onPress={onSubmit}
        loading={isSubmitting || forgot.isPending}
        disabled={isSubmitting || forgot.isPending}
      >
        Send reset link
      </Button>
      <Button onPress={() => navigation.navigate('ResetPassword', { email: getValues('email') })}>
        I already have a token
      </Button>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  wrap: { padding: 24 },
  subtitle: { marginBottom: 16, opacity: 0.7 },
});
