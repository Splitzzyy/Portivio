import type { ExpoConfig } from 'expo/config';

const isProd = process.env.APP_ENV === 'prod';

const config: ExpoConfig = {
  name: 'Portivio',
  slug: 'portivio',
  version: '0.1.0',
  orientation: 'portrait',
  icon: './assets/icon.png',
  userInterfaceStyle: 'automatic',
  splash: {
    image: './assets/splash.png',
    resizeMode: 'contain',
    backgroundColor: '#0F172A',
  },
  android: {
    package: 'com.portivio.android',
    versionCode: 1,
    adaptiveIcon: {
      foregroundImage: './assets/adaptive-icon.png',
      backgroundColor: '#0F172A',
    },
  },
  plugins: [
    [
      'expo-build-properties',
      {
        android: {
          minSdkVersion: 29,
          compileSdkVersion: 34,
          targetSdkVersion: 34,
          usesCleartextTraffic: !isProd,
        },
      },
    ],
    'expo-secure-store',
  ],
  extra: {
    apiUrl: isProd ? 'https://TBD/api' : 'http://10.0.2.2:5274/api',
    googleClientId: 'YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com',
    appEnv: isProd ? 'prod' : 'dev',
  },
};

export default config;
