import React from 'react';
import { ScrollView, StyleSheet } from 'react-native';
import { Button, Text } from 'react-native-paper';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import Toast from 'react-native-toast-message';

import type { AuthScreenProps } from '../../../navigation/types';
import { ControlledTextInput } from '../../../components/forms/ControlledTextInput';
import { SignupForm, signupSchema } from '../schema';
import { useSignup } from '../../../queries/auth';

export function SignupScreen({ navigation }: AuthScreenProps<'Signup'>): React.JSX.Element {
  const { control, handleSubmit, formState: { isSubmitting } } = useForm<SignupForm>({
    resolver: zodResolver(signupSchema),
    defaultValues: { name: '', email: '', password: '', confirmPassword: '' },
  });
  const signup = useSignup();

  const onSubmit = handleSubmit(async (data) => {
    try {
      const res = await signup.mutateAsync(data);
      if (!res.success) {
        Toast.show({ type: 'error', text1: 'Signup failed', text2: res.message ?? '' });
        return;
      }
      Toast.show({ type: 'success', text1: 'Account created', text2: 'Check your email to verify' });
      if (!res.accessToken) {
        navigation.navigate('VerifyEmail', { email: data.email });
      }
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : 'Network error';
      Toast.show({ type: 'error', text1: 'Signup failed', text2: msg });
    }
  });

  return (
    <ScrollView contentContainerStyle={styles.wrap}>
      <Text variant="headlineSmall" style={styles.title}>Create your account</Text>
      <ControlledTextInput name="name" control={control} label="Name" />
      <ControlledTextInput
        name="email"
        control={control}
        label="Email"
        autoCapitalize="none"
        keyboardType="email-address"
      />
      <ControlledTextInput name="password" control={control} label="Password" secureTextEntry />
      <ControlledTextInput
        name="confirmPassword"
        control={control}
        label="Confirm Password"
        secureTextEntry
      />
      <Button
        mode="contained"
        onPress={onSubmit}
        loading={isSubmitting || signup.isPending}
        disabled={isSubmitting || signup.isPending}
        style={styles.btn}
      >
        Sign Up
      </Button>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  wrap: { padding: 24 },
  title: { marginBottom: 16 },
  btn: { marginTop: 8 },
});
