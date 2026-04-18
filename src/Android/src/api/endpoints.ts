import type { Guid } from '../types/dtos';

export const endpoints = {
  auth: {
    login: '/auth/login',
    signup: '/auth/signup',
    refresh: '/auth/refresh-token',
    verifyEmail: '/auth/verify-email',
    resendVerification: '/auth/resend-verification',
    forgotPassword: '/auth/forgot-password',
    resetPassword: '/auth/reset-password',
    googleLogin: '/auth/google-login',
    logout: '/auth/logout',
  },
  home: '/home',
  profiles: {
    list: '/profiles',
    create: '/profiles',
    byId: (id: Guid) => `/profiles/${id}`,
  },
  holdings: {
    list: (profileId: Guid) => `/profiles/${profileId}/holdings`,
    create: (profileId: Guid) => `/profiles/${profileId}/holdings`,
    byId: (profileId: Guid, id: Guid) => `/profiles/${profileId}/holdings/${id}`,
  },
  transactions: {
    list: (profileId: Guid) => `/profiles/${profileId}/transactions`,
    create: (profileId: Guid) => `/profiles/${profileId}/transactions`,
    byId: (profileId: Guid, id: Guid) => `/profiles/${profileId}/transactions/${id}`,
  },
  instruments: {
    list: '/instruments',
    create: '/instruments',
    byId: (id: Guid) => `/instruments/${id}`,
  },
  assetTypes: {
    list: '/asset-types',
    create: '/asset-types',
    byId: (id: Guid) => `/asset-types/${id}`,
  },
  sipPlans: {
    list: (profileId: Guid) => `/profiles/${profileId}/sip-plans`,
    create: (profileId: Guid) => `/profiles/${profileId}/sip-plans`,
    byId: (profileId: Guid, id: Guid) => `/profiles/${profileId}/sip-plans/${id}`,
    activate: (profileId: Guid, id: Guid) => `/profiles/${profileId}/sip-plans/${id}/activate`,
    deactivate: (profileId: Guid, id: Guid) => `/profiles/${profileId}/sip-plans/${id}/deactivate`,
  },
  performance: {
    history: (profileId: Guid) => `/profiles/${profileId}/performance`,
    latest: (profileId: Guid) => `/profiles/${profileId}/performance/latest`,
    snapshot: (profileId: Guid) => `/profiles/${profileId}/performance/snapshot`,
  },
  prices: {
    list: (instrumentId: Guid) => `/instruments/${instrumentId}/prices`,
    latest: (instrumentId: Guid) => `/instruments/${instrumentId}/prices/latest`,
    create: (instrumentId: Guid) => `/instruments/${instrumentId}/prices`,
    bulk: (instrumentId: Guid) => `/instruments/${instrumentId}/prices/bulk`,
    byId: (instrumentId: Guid, priceId: Guid) =>
      `/instruments/${instrumentId}/prices/${priceId}`,
  },
};

export const AUTH_ALLOWLIST = [
  endpoints.auth.login,
  endpoints.auth.signup,
  endpoints.auth.googleLogin,
  endpoints.auth.refresh,
  endpoints.auth.forgotPassword,
  endpoints.auth.resetPassword,
];
