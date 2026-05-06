import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, throwError } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  User,
  LoginCredentials,
  SignupForm,
  AuthResponse,
  ForgotPasswordRequest,
  ResetPassword,
  VerifyEmailRequest,
  GoogleLoginRequest,
  SimpleResponse
} from '../models/auth.model';

/**
 * Authentication service.
 * Wraps every /api/auth endpoint exposed by AuthController and manages the
 * access/refresh token + user in localStorage.
 *
 * Field names here must match the backend DTOs 1:1 — do not rename.
 */
@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly API_URL = `${environment.apiUrl}/auth`;

  // Storage keys are namespaced + versioned so an old build's stale keys are
  // cleared on first load after a breaking contract change.
  private static readonly STORAGE_VERSION = 'v2';
  private readonly ACCESS_TOKEN_KEY = `portivio_access_token_${AuthService.STORAGE_VERSION}`;
  private readonly REFRESH_TOKEN_KEY = `portivio_refresh_token_${AuthService.STORAGE_VERSION}`;
  private readonly ACCESS_EXPIRY_KEY = `portivio_access_expiry_${AuthService.STORAGE_VERSION}`;
  private readonly USER_KEY = `portivio_user_${AuthService.STORAGE_VERSION}`;

  private userSubject: BehaviorSubject<User | null>;
  public user$: Observable<User | null>;

  private isAuthenticatedSubject: BehaviorSubject<boolean>;
  public isAuthenticated$: Observable<boolean>;

  constructor(private http: HttpClient) {
    this.clearLegacyStorageKeys();

    const storedUser = this.getStoredUser();
    this.userSubject = new BehaviorSubject<User | null>(storedUser);
    this.user$ = this.userSubject.asObservable();

    const isAuthenticated = this.hasValidToken();
    this.isAuthenticatedSubject = new BehaviorSubject<boolean>(isAuthenticated);
    this.isAuthenticated$ = this.isAuthenticatedSubject.asObservable();
  }

  // ---------------------------------------------------------------------------
  // Public API — one method per backend endpoint
  // ---------------------------------------------------------------------------

  login(credentials: LoginCredentials): Observable<AuthResponse> {
    const payload = {
      email: credentials.email,
      password: credentials.password
    };

    return this.http.post<AuthResponse>(`${this.API_URL}/login`, payload).pipe(
      tap(response => this.handleAuthResponse(response))
    );
  }

  signup(form: SignupForm): Observable<AuthResponse> {
    const payload = {
      email: form.email,
      name: form.name,
      password: form.password,
      confirmPassword: form.confirmPassword
    };

    return this.http.post<AuthResponse>(`${this.API_URL}/signup`, payload).pipe(
      tap(response => this.handleAuthResponse(response))
    );
  }

  /**
   * Exchange a Google ID token (from Google Identity Services) for a Portivio session.
   * Backend currently returns 501 until GoogleLoginAsync is implemented — the
   * global error interceptor surfaces the backend message to the user.
   */
  googleLogin(idToken: string): Observable<AuthResponse> {
    const payload: GoogleLoginRequest = { token: idToken };
    return this.http.post<AuthResponse>(`${this.API_URL}/google-login`, payload).pipe(
      tap(response => this.handleAuthResponse(response))
    );
  }

  forgotPassword(request: ForgotPasswordRequest): Observable<SimpleResponse> {
    return this.http.post<SimpleResponse>(`${this.API_URL}/forgot-password`, request);
  }

  resendVerificationEmail(email: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(
      `${this.API_URL}/resend-verification`,
      null,
      { params: { email } }
    );
  }

  resetPassword(reset: ResetPassword): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.API_URL}/reset-password`, reset);
  }

  verifyEmail(request: VerifyEmailRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.API_URL}/verify-email`, request);
  }

  /**
   * Logout clears local session regardless of what the server says. We still
   * call the server so it can revoke the refresh token, but a failed logout
   * must not leave the user stuck in an authenticated state on the client.
   */
  logout(): Observable<SimpleResponse> {
    return this.http.post<SimpleResponse>(`${this.API_URL}/logout`, {}).pipe(
      tap(() => this.clearAuth()),
      catchError(error => {
        this.clearAuth();
        return throwError(() => error);
      })
    );
  }

  refreshToken(): Observable<AuthResponse> {
    // Refresh token lives in an HttpOnly cookie set by the backend on login.
    // Browser sends it automatically — just POST empty body. Backend falls
    // through to Request.Cookies["refreshToken"] when body token is absent.
    return this.http.post<AuthResponse>(
      `${this.API_URL}/refresh-token`,
      {}
    ).pipe(
      tap(response => this.handleAuthResponse(response)),
      catchError(error => {
        this.clearAuth();
        return throwError(() => error);
      })
    );
  }

  // ---------------------------------------------------------------------------
  // State accessors
  // ---------------------------------------------------------------------------

  getCurrentUser(): User | null {
    return this.userSubject.value;
  }

  isAuthenticated(): boolean {
    return this.hasValidToken();
  }

  getAccessToken(): string | null {
    return localStorage.getItem(this.ACCESS_TOKEN_KEY);
  }

  /** Clear local session state without making an HTTP call. Use when the server
   *  is unreachable or the session is already invalid (e.g. after refresh fails). */
  clearSession(): void {
    this.clearAuth();
  }

  getRefreshToken(): string | null {
    // Refresh token is in an HttpOnly cookie — not readable from JS.
    // Method kept for interface compatibility; always returns null.
    return null;
  }

  // ---------------------------------------------------------------------------
  // Internals
  // ---------------------------------------------------------------------------

  private handleAuthResponse(response: AuthResponse): void {
    if (!response || !response.success) {
      return;
    }

    if (response.accessToken) {
      localStorage.setItem(this.ACCESS_TOKEN_KEY, response.accessToken);
    }
    if (response.accessTokenExpiry) {
      localStorage.setItem(this.ACCESS_EXPIRY_KEY, response.accessTokenExpiry);
    }
    if (response.refreshToken) {
      localStorage.setItem(this.REFRESH_TOKEN_KEY, response.refreshToken);
    }
    if (response.user) {
      localStorage.setItem(this.USER_KEY, JSON.stringify(response.user));
      this.userSubject.next(response.user);
      this.isAuthenticatedSubject.next(true);
    }
  }

  private clearAuth(): void {
    localStorage.removeItem(this.ACCESS_TOKEN_KEY);
    localStorage.removeItem(this.REFRESH_TOKEN_KEY);
    localStorage.removeItem(this.ACCESS_EXPIRY_KEY);
    localStorage.removeItem(this.USER_KEY);
    this.userSubject.next(null);
    this.isAuthenticatedSubject.next(false);
  }

  private getStoredUser(): User | null {
    const userJson = localStorage.getItem(this.USER_KEY);
    if (!userJson) return null;
    try {
      return JSON.parse(userJson) as User;
    } catch {
      return null;
    }
  }

  /**
   * Access token is valid iff we have a token AND a stored expiry AND the
   * expiry is in the future. Using the backend-supplied expiry avoids decoding
   * the JWT client-side — simpler and one fewer place the contract can drift.
   */
  private hasValidToken(): boolean {
    const token = this.getAccessToken();
    const expiry = localStorage.getItem(this.ACCESS_EXPIRY_KEY);
    if (!token || !expiry) return false;

    const expiryMs = Date.parse(expiry);
    if (Number.isNaN(expiryMs)) return false;
    return expiryMs > Date.now();
  }

  /**
   * Remove keys from any earlier build so they don't linger and confuse
   * hasValidToken() or getStoredUser(). Runs once per page load.
   */
  private clearLegacyStorageKeys(): void {
    const legacyKeys = ['auth_token', 'refresh_token', 'user_data'];
    legacyKeys.forEach(k => localStorage.removeItem(k));
  }
}
