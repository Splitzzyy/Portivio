import { useMutation } from '@tanstack/react-query';
import { apiClient } from '../api/client';
import { endpoints } from '../api/endpoints';
import { Auth } from '../types/dtos';
import { useAuthStore } from '../store/authStore';

export function useLogin() {
  const setSession = useAuthStore((s) => s.setSession);
  return useMutation({
    mutationFn: async (req: Auth.LoginRequest) => {
      const res = await apiClient.post<Auth.AuthResponse>(endpoints.auth.login, req);
      return res.data;
    },
    onSuccess: async (data) => {
      if (data.success) await setSession(data);
    },
  });
}

export function useSignup() {
  const setSession = useAuthStore((s) => s.setSession);
  return useMutation({
    mutationFn: async (req: Auth.SignupRequest) => {
      const res = await apiClient.post<Auth.AuthResponse>(endpoints.auth.signup, req);
      return res.data;
    },
    onSuccess: async (data) => {
      if (data.success && data.accessToken) await setSession(data);
    },
  });
}

export function useForgotPassword() {
  return useMutation({
    mutationFn: async (req: Auth.ForgotPasswordRequest) => {
      const res = await apiClient.post<Auth.AuthResponse>(
        endpoints.auth.forgotPassword,
        req,
      );
      return res.data;
    },
  });
}

export function useResetPassword() {
  return useMutation({
    mutationFn: async (req: Auth.ResetPasswordRequest) => {
      const res = await apiClient.post<Auth.AuthResponse>(
        endpoints.auth.resetPassword,
        req,
      );
      return res.data;
    },
  });
}

export function useVerifyEmail() {
  const setSession = useAuthStore((s) => s.setSession);
  return useMutation({
    mutationFn: async (req: Auth.VerifyEmailRequest) => {
      const res = await apiClient.post<Auth.AuthResponse>(endpoints.auth.verifyEmail, req);
      return res.data;
    },
    onSuccess: async (data) => {
      if (data.success && data.user) await setSession(data);
    },
  });
}

export function useResendVerification() {
  return useMutation({
    mutationFn: async (email: string) => {
      const res = await apiClient.post<Auth.AuthResponse>(
        `${endpoints.auth.resendVerification}?email=${encodeURIComponent(email)}`,
      );
      return res.data;
    },
  });
}

export function useLogout() {
  const clear = useAuthStore((s) => s.clear);
  return useMutation({
    mutationFn: async () => {
      try {
        await apiClient.post(endpoints.auth.logout);
      } catch {
        // ignore — clearing client state is what matters
      }
    },
    onSettled: async () => {
      await clear();
    },
  });
}
