import React from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';
import { Button, Text } from 'react-native-paper';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import Toast from 'react-native-toast-message';

import type { AuthScreenProps } from '../../../navigation/types';
import { ControlledTextInput } from '../../../components/forms/ControlledTextInput';
import { LoginForm, loginSchema } from '../schema';
import { useLogin } from '../../../queries/auth';

export function LoginScreen({ navigation }: AuthScreenProps<'Login'>): React.JSX.Element {
  const { control, handleSubmit, formState: { isSubmitting } } = useForm<LoginForm>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });
  const login = useLogin();

  const onSubmit = handleSubmit(async (data) => {
    try {
      const res = await login.mutateAsync(data);
      if (!res.success) {
        Toast.show({ type: 'error', text1: 'Login failed', text2: res.message ?? '' });
      }
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : 'Network error';
      Toast.show({ type: 'error', text1: 'Login failed', text2: msg });
    }
  });

  return (
    <ScrollView contentContainerStyle={styles.wrap}>
      <Text variant="headlineMedium" style={styles.title}>Portivio</Text>
      <Text variant="bodyMedium" style={styles.subtitle}>Welcome back. Sign in to continue.</Text>

      <ControlledTextInput
        name="email"
        control={control}
        label="Email"
        autoCapitalize="none"
        keyboardType="email-address"
      />
      <ControlledTextInput name="password" control={control} label="Password" secureTextEntry />

      <Button
        mode="contained"
        onPress={onSubmit}
        loading={isSubmitting || login.isPending}
        disabled={isSubmitting || login.isPending}
        style={styles.btn}
      >
        Sign In
      </Button>

      <View style={styles.row}>
        <Button onPress={() => navigation.navigate('ForgotPassword')}>Forgot?</Button>
        <Button onPress={() => navigation.navigate('Signup')}>Create account</Button>
      </View>

      <Button
        onPress={() =>
          Toast.show({ type: 'info', text1: 'Google Sign-In', text2: 'Coming soon' })
        }
        style={styles.google}
      >
        Continue with Google
      </Button>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  wrap: { padding: 24, flexGrow: 1, justifyContent: 'center' },
  title: { textAlign: 'center', marginBottom: 4 },
  subtitle: { textAlign: 'center', marginBottom: 24, opacity: 0.7 },
  btn: { marginTop: 8 },
  row: { flexDirection: 'row', justifyContent: 'space-between', marginTop: 8 },
  google: { marginTop: 8 },
});
