import React from 'react';
import { ScrollView, StyleSheet } from 'react-native';
import { Button, Text } from 'react-native-paper';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import Toast from 'react-native-toast-message';

import type { AuthScreenProps } from '../../../navigation/types';
import { ControlledTextInput } from '../../../components/forms/ControlledTextInput';
import { VerifyForm, verifySchema } from '../schema';
import { useResendVerification, useVerifyEmail } from '../../../queries/auth';

export function VerifyEmailScreen({
  route,
  navigation,
}: AuthScreenProps<'VerifyEmail'>): React.JSX.Element {
  const { control, handleSubmit, getValues, formState: { isSubmitting } } = useForm<VerifyForm>({
    resolver: zodResolver(verifySchema),
    defaultValues: {
      email: route.params?.email ?? '',
      verificationToken: route.params?.token ?? '',
    },
  });
  const verify = useVerifyEmail();
  const resend = useResendVerification();

  const onSubmit = handleSubmit(async (data) => {
    try {
      const res = await verify.mutateAsync(data);
      Toast.show({
        type: res.success ? 'success' : 'error',
        text1: res.success ? 'Email verified' : 'Verification failed',
        text2: res.message ?? '',
      });
      if (res.success) navigation.navigate('Login');
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : 'Network error';
      Toast.show({ type: 'error', text1: 'Verification failed', text2: msg });
    }
  });

  return (
    <ScrollView contentContainerStyle={styles.wrap}>
      <Text variant="bodyMedium" style={styles.subtitle}>
        Paste the token from your verification email below.
      </Text>
      <ControlledTextInput
        name="email"
        control={control}
        label="Email"
        autoCapitalize="none"
        keyboardType="email-address"
      />
      <ControlledTextInput
        name="verificationToken"
        control={control}
        label="Verification Token"
      />
      <Button
        mode="contained"
        onPress={onSubmit}
        loading={isSubmitting || verify.isPending}
        disabled={isSubmitting || verify.isPending}
      >
        Verify
      </Button>
      <Button
        loading={resend.isPending}
        onPress={async () => {
          const email = getValues('email');
          if (!email) return;
          try {
            await resend.mutateAsync(email);
            Toast.show({ type: 'success', text1: 'Verification sent' });
          } catch (e: unknown) {
            const msg = e instanceof Error ? e.message : 'Network error';
            Toast.show({ type: 'error', text1: 'Resend failed', text2: msg });
          }
        }}
      >
        Resend
      </Button>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  wrap: { padding: 24 },
  subtitle: { marginBottom: 16, opacity: 0.7 },
});
