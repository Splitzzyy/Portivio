import { Injectable } from '@angular/core';
import {
  HttpRequest,
  HttpHandler,
  HttpEvent,
  HttpInterceptor,
  HttpErrorResponse
} from '@angular/common/http';
import { Observable, throwError, BehaviorSubject } from 'rxjs';
import { catchError, filter, take, switchMap } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';
import { AuthResponse } from '../models/auth.model';

/**
 * Attaches the access token to outgoing requests and, on 401, attempts a
 * single refresh-token round-trip. Concurrent 401s during an in-flight
 * refresh are queued on `refreshTokenSubject` and replayed once the refresh
 * succeeds, so a page that fires N parallel requests only triggers one
 * refresh.
 *
 * Auth endpoints are explicitly excluded from the Authorization header —
 * signup/login must not send a stale bearer token.
 */
@Injectable()
export class JwtInterceptor implements HttpInterceptor {
  private isRefreshing = false;
  private refreshTokenSubject = new BehaviorSubject<string | null>(null);

  constructor(private authService: AuthService) {}

  intercept(
    request: HttpRequest<unknown>,
    next: HttpHandler
  ): Observable<HttpEvent<unknown>> {
    const token = this.authService.getAccessToken();
    if (token) {
      request = this.addTokenToRequest(request, token);
    }

    return next.handle(request).pipe(
      catchError(error => {
        if (error instanceof HttpErrorResponse && error.status === 401 && !this.isAuthUrl(request.url)) {
          return this.handle401Error(request, next);
        }
        return throwError(() => error);
      })
    );
  }

  private isAuthUrl(url: string): boolean {
    return url.includes('/auth/login')
      || url.includes('/auth/signup')
      || url.includes('/auth/google-login')
      || url.includes('/auth/refresh-token')
      || url.includes('/auth/forgot-password')
      || url.includes('/auth/reset-password')
      || url.includes('/auth/logout')
      || url.includes('/auth/verify-email')
      || url.includes('/auth/resend-verification');
  }

  private addTokenToRequest(request: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
    // Don't attach the bearer to auth endpoints — they're unauthenticated.
    if (this.isAuthUrl(request.url)) {
      return request;
    }
    return request.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    });
  }

  private handle401Error(
    request: HttpRequest<unknown>,
    next: HttpHandler
  ): Observable<HttpEvent<unknown>> {
    if (this.isRefreshing) {
      // Refresh already in-flight — wait for it, then replay with the new token.
      return this.refreshTokenSubject.pipe(
        filter(token => token !== null),
        take(1),
        switchMap(token => next.handle(this.addTokenToRequest(request, token as string)))
      );
    }

    this.isRefreshing = true;
    this.refreshTokenSubject.next(null);

    return this.authService.refreshToken().pipe(
      switchMap((response: AuthResponse) => {
        this.isRefreshing = false;
        const newToken = response.accessToken ?? null;
        this.refreshTokenSubject.next(newToken);
        if (!newToken) {
          return throwError(() => new Error('Refresh succeeded but no access token in response'));
        }
        return next.handle(this.addTokenToRequest(request, newToken));
      }),
      catchError(err => {
        this.isRefreshing = false;
        // Refresh failed — clear local session without hitting the server.
        // Calling authService.logout() here would fire another HTTP request
        // that could 401 and re-enter this handler, causing an infinite loop.
        this.authService.clearSession();
        return throwError(() => err);
      })
    );
  }
}
