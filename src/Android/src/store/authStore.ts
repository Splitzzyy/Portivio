import { create } from 'zustand';
import { Auth } from '../types/dtos';
import { tokenStorage } from '../api/storage';

interface AuthState {
  user: Auth.UserDto | null;
  isAuthenticated: boolean;
  isHydrating: boolean;
  hydrate: () => Promise<void>;
  setSession: (auth: Auth.AuthResponse) => Promise<void>;
  clear: () => Promise<void>;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  isAuthenticated: false,
  isHydrating: true,
  hydrate: async () => {
    const { accessToken, accessExpiry, user } = await tokenStorage.read();
    const expired = accessExpiry
      ? new Date(accessExpiry).getTime() < Date.now() + 60_000
      : true;
    const valid = !!accessToken && !expired;
    set({
      user: user ? (JSON.parse(user) as Auth.UserDto) : null,
      isAuthenticated: valid,
      isHydrating: false,
    });
  },
  setSession: async (auth) => {
    await tokenStorage.write({
      accessToken: auth.accessToken ?? null,
      refreshToken: auth.refreshToken ?? null,
      accessExpiry: auth.accessTokenExpiry ?? null,
      user: auth.user ? JSON.stringify(auth.user) : null,
    });
    set({
      user: auth.user ?? null,
      isAuthenticated: !!auth.accessToken,
    });
  },
  clear: async () => {
    await tokenStorage.clear();
    set({ user: null, isAuthenticated: false });
  },
}));
