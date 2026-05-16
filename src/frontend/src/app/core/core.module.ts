import { NgModule, APP_INITIALIZER } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HTTP_INTERCEPTORS } from '@angular/common/http';
import { AuthService } from './services/auth.service';
import { AuthGuard, NoAuthGuard } from './guards/auth.guard';
import { JwtInterceptor } from './interceptors/jwt.interceptor';
import { ErrorInterceptor } from './interceptors/error.interceptor';

/**
 * Factory for restoring session on startup.
 */
export function initializeApp(authService: AuthService) {
  return () => authService.restoreSession();
}

/**
 * Core module containing singleton services, guards, and interceptors.
 * Imported exactly once, from AppModule.
 *
 * Interceptor ordering matters: HTTP_INTERCEPTORS run top-to-bottom on the
 * request path and bottom-to-top on the response path. We want JwtInterceptor
 * to see 401s first (so it can refresh transparently) and ErrorInterceptor to
 * receive everything else, so JwtInterceptor is registered FIRST.
 */
@NgModule({
  declarations: [],
  imports: [CommonModule],
  providers: [
    AuthService,
    AuthGuard,
    NoAuthGuard,
    {
      provide: APP_INITIALIZER,
      useFactory: initializeApp,
      deps: [AuthService],
      multi: true
    },
    {
      provide: HTTP_INTERCEPTORS,
      useClass: JwtInterceptor,
      multi: true
    },
    {
      provide: HTTP_INTERCEPTORS,
      useClass: ErrorInterceptor,
      multi: true
    }
  ]
})
export class CoreModule {}
