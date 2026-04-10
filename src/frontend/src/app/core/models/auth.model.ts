/**
 * Auth domain types.
 * Shapes mirror the backend DTOs in src/backend/Portivio.Application/DTOs/Auth/
 * exactly (camelCase over the wire via ASP.NET default JSON serialization).
 * Keep these in sync when the backend contract changes.
 */

export interface User {
  id: string;
  email: string;
  name: string;
  isVerified: boolean;
  isActive: boolean;
}

export interface LoginCredentials {
  email: string;
  password: string;
  rememberMe?: boolean;
}

export interface SignupForm {
  email: string;
  name: string;
  password: string;
  confirmPassword: string;
  acceptTerms: boolean;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPassword {
  email: string;
  resetToken: string;
  newPassword: string;
  confirmPassword: string;
}

export interface GoogleLoginRequest {
  token: string;
  deviceInfo?: string;
  ipAddress?: string;
}

export interface AuthResponse {
  success: boolean;
  message?: string;
  accessToken?: string;
  refreshToken?: string;
  accessTokenExpiry?: string;
  refreshTokenExpiry?: string;
  user?: User;
}

export interface SimpleResponse {
  success: boolean;
  message?: string;
}

export interface ApiError {
  success: false;
  message: string;
  errors: string[];
}
