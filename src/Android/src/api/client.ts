import axios, { AxiosError, AxiosRequestConfig, InternalAxiosRequestConfig } from 'axios';
import Constants from 'expo-constants';
import { Auth } from '../types/dtos';
import { AUTH_ALLOWLIST, endpoints } from './endpoints';
import { tokenStorage } from './storage';

const apiUrl =
  (Constants.expoConfig?.extra as { apiUrl?: string } | undefined)?.apiUrl ??
  'http://10.0.2.2:5274/api';

export const apiClient = axios.create({
  baseURL: apiUrl,
  timeout: 20000,
  headers: { 'Content-Type': 'application/json' },
});

let isRefreshing = false;
type QueueItem = {
  resolve: (token: string | null) => void;
  reject: (err: unknown) => void;
};
const queue: QueueItem[] = [];

function drain(token: string | null, err?: unknown): void {
  while (queue.length) {
    const { resolve, reject } = queue.shift()!;
    if (err) reject(err);
    else resolve(token);
  }
}

let onAuthFailure: (() => void) | null = null;
export function setOnAuthFailure(fn: () => void): void {
  onAuthFailure = fn;
}

function isAuthRoute(url?: string): boolean {
  if (!url) return false;
  return AUTH_ALLOWLIST.some((p) => url.endsWith(p));
}

apiClient.interceptors.request.use(async (config: InternalAxiosRequestConfig) => {
  if (isAuthRoute(config.url)) return config;
  const { accessToken } = await tokenStorage.read();
  if (accessToken) {
    config.headers.set('Authorization', `Bearer ${accessToken}`);
  }
  return config;
});

apiClient.interceptors.response.use(
  (r) => r,
  async (error: AxiosError) => {
    const original = error.config as (AxiosRequestConfig & { _retry?: boolean }) | undefined;
    const status = error.response?.status;
    if (!original || status !== 401 || isAuthRoute(original.url) || original._retry) {
      return Promise.reject(error);
    }

    original._retry = true;

    if (isRefreshing) {
      return new Promise((resolve, reject) => {
        queue.push({
          resolve: (token) => {
            if (!token) return reject(error);
            (original.headers as Record<string, string>) = {
              ...(original.headers as Record<string, string>),
              Authorization: `Bearer ${token}`,
            };
            resolve(apiClient(original));
          },
          reject,
        });
      });
    }

    isRefreshing = true;

    try {
      const { refreshToken } = await tokenStorage.read();
      if (!refreshToken) throw error;

      const refresh = await axios.post<Auth.AuthResponse>(
        `${apiUrl}${endpoints.auth.refresh}`,
        { refreshToken } satisfies Auth.RefreshTokenRequest,
        { headers: { 'Content-Type': 'application/json' } },
      );

      const next = refresh.data;
      if (!next.success || !next.accessToken) throw error;

      await tokenStorage.write({
        accessToken: next.accessToken,
        refreshToken: next.refreshToken ?? refreshToken,
        accessExpiry: next.accessTokenExpiry ?? null,
        user: next.user ? JSON.stringify(next.user) : undefined,
      });

      drain(next.accessToken);

      (original.headers as Record<string, string>) = {
        ...(original.headers as Record<string, string>),
        Authorization: `Bearer ${next.accessToken}`,
      };
      return apiClient(original);
    } catch (refreshErr) {
      drain(null, refreshErr);
      await tokenStorage.clear();
      onAuthFailure?.();
      return Promise.reject(refreshErr);
    } finally {
      isRefreshing = false;
    }
  },
);
