export const environment = {
  production: false,
  apiUrl: 'http://localhost:5274/api',
  oauth: {
    google: {
      // Replace with your real OAuth 2.0 Client ID from Google Cloud Console
      // (APIs & Services → Credentials → OAuth 2.0 Client IDs).
      // Authorized JavaScript origin for dev: http://localhost:4200
      clientId: 'YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com'
    }
  }
};
