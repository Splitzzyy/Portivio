import { Injectable } from '@angular/core';
import {
  HttpRequest,
  HttpHandler,
  HttpEvent,
  HttpInterceptor,
  HttpErrorResponse
} from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';

/**
 * Global HTTP error interceptor.
 *
 * Maps HttpErrorResponse.status to a user-visible toast and always rethrows
 * so component-level handlers can still opt in to inline error UX. Must be
 * registered AFTER JwtInterceptor in the HTTP_INTERCEPTORS provider array —
 * that way JwtInterceptor sees 401s first and can refresh transparently;
 * this interceptor only reports 401s that survived the refresh round-trip.
 */
@Injectable()
export class ErrorInterceptor implements HttpInterceptor {
  // 0-status errors (network / CORS) are very noisy during dev — keep the
  // console hint to once-per-session so the devtools don't flood.
  private loggedCorsHint = false;

  constructor(private toastr: ToastrService) {}

  intercept(
    request: HttpRequest<unknown>,
    next: HttpHandler
  ): Observable<HttpEvent<unknown>> {
    return next.handle(request).pipe(
      catchError((error: HttpErrorResponse) => {
        if (!this.shouldHandleInline(request, error)) {
          this.report(error);
        }
        return throwError(() => error);
      })
    );
  }

  private shouldHandleInline(request: HttpRequest<unknown>, error: HttpErrorResponse): boolean {
    if (!this.isAuthRequest(request.url)) {
      return false;
    }

    return error.status >= 400 && error.status < 500;
  }

  private isAuthRequest(url: string): boolean {
    return url.includes('/auth/');
  }

  private report(error: HttpErrorResponse): void {
    const backendMessage = this.extractBackendMessage(error);

    switch (error.status) {
      case 0:
        this.toastr.error(
          'Cannot reach the server. Check your connection and try again.',
          'Network error'
        );
        if (!this.loggedCorsHint) {
          // Most 0-status errors during local dev are CORS misses. Leave a
          // breadcrumb in the console so devs know where to look.
          // eslint-disable-next-line no-console
          console.warn(
            '[ErrorInterceptor] 0-status error received. If the backend is ' +
            'running, this is almost certainly a CORS policy miss — ensure ' +
            'Portivio.API/Program.cs calls app.UseCors("AllowFrontend") with ' +
            'http://localhost:4200 whitelisted.'
          );
          this.loggedCorsHint = true;
        }
        break;

      case 400:
        this.toastr.error(backendMessage || 'Invalid request.', 'Bad request');
        break;

      case 401:
        // JwtInterceptor already tried to refresh — a 401 that reaches here
        // means the refresh failed or the request was already to an auth
        // endpoint. Surface it so the user isn't left staring at a dead form.
        this.toastr.error(
          backendMessage || 'Your session has expired. Please sign in again.',
          'Unauthorized'
        );
        break;

      case 403:
        this.toastr.warning(
          backendMessage || "You don't have permission for this action.",
          'Forbidden'
        );
        break;

      case 404:
        this.toastr.error(backendMessage || 'Not found.', 'Not found');
        break;

      case 409:
        this.toastr.warning(backendMessage || 'Conflict.', 'Conflict');
        break;

      case 500:
        this.toastr.error(
          'Something went wrong on our end. Please try again.',
          'Server error'
        );
        break;

      case 501:
        // Used by the Google SSO stub until the backend wires token validation.
        this.toastr.warning(
          backendMessage || 'This feature is not yet enabled on the server.',
          'Not implemented'
        );
        break;

      default:
        if (error.status >= 500) {
          this.toastr.error(
            'Something went wrong. Please try again.',
            `Server error (${error.status})`
          );
        } else if (error.status >= 400) {
          this.toastr.error(backendMessage || 'Request failed.', `Error ${error.status}`);
        }
    }
  }

  /**
   * Backend Result<T> shape: { success: false, message: string, errors: string[] }.
   * Fall back to error.message (HttpErrorResponse default) if shape differs.
   */
  private extractBackendMessage(error: HttpErrorResponse): string | null {
    const body = error.error as { message?: string; errors?: string[] } | null | string;
    if (!body) return null;
    if (typeof body === 'string') return body;
    if (body.errors && body.errors.length > 0) return body.errors[0];
    if (body.message) return body.message;
    return null;
  }
}
