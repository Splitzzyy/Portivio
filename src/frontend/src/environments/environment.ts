export const environment = {
  production: false,
  apiUrl: 'http://localhost:3000/api',
  oauth: {
    google: {
      clientId: 'YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com',
      redirectUri: 'http://localhost:4200/auth/callback'
    },
    microsoft: {
      clientId: 'YOUR_MICROSOFT_CLIENT_ID',
      authority: 'https://login.microsoftonline.com/common',
      redirectUri: 'http://localhost:4200/auth/callback'
    }
  }
};
