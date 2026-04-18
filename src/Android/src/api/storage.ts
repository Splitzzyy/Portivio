import * as SecureStore from 'expo-secure-store';

const KEYS = {
  access: 'portivio_access_token_v2',
  refresh: 'portivio_refresh_token_v2',
  expiry: 'portivio_access_expiry_v2',
  user: 'portivio_user_v2',
} as const;

export interface StoredTokens {
  accessToken: string | null;
  refreshToken: string | null;
  accessExpiry: string | null;
  user: string | null;
}

export const tokenStorage = {
  async read(): Promise<StoredTokens> {
    const [accessToken, refreshToken, accessExpiry, user] = await Promise.all([
      SecureStore.getItemAsync(KEYS.access),
      SecureStore.getItemAsync(KEYS.refresh),
      SecureStore.getItemAsync(KEYS.expiry),
      SecureStore.getItemAsync(KEYS.user),
    ]);
    return { accessToken, refreshToken, accessExpiry, user };
  },

  async write(t: Partial<StoredTokens>): Promise<void> {
    const ops: Promise<void>[] = [];
    if (t.accessToken !== undefined)
      ops.push(setOrDelete(KEYS.access, t.accessToken));
    if (t.refreshToken !== undefined)
      ops.push(setOrDelete(KEYS.refresh, t.refreshToken));
    if (t.accessExpiry !== undefined)
      ops.push(setOrDelete(KEYS.expiry, t.accessExpiry));
    if (t.user !== undefined) ops.push(setOrDelete(KEYS.user, t.user));
    await Promise.all(ops);
  },

  async clear(): Promise<void> {
    await Promise.all(
      Object.values(KEYS).map((k) => SecureStore.deleteItemAsync(k)),
    );
  },
};

async function setOrDelete(key: string, value: string | null): Promise<void> {
  if (value === null || value === '') {
    await SecureStore.deleteItemAsync(key);
  } else {
    await SecureStore.setItemAsync(key, value);
  }
}
