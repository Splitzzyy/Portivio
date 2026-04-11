import { Injectable } from '@angular/core';
import { Subject, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * Thin wrapper around Google Identity Services (GIS).
 *
 * Responsibilities:
 *   1. Lazy-initialize `google.accounts.id` on first use (the <script> tag in
 *      index.html may not have resolved yet at app boot).
 *   2. Render the official Google Sign-In button into a host element — the
 *      official button handles consent screens, one-tap, account chooser etc.
 *      much better than a custom button.
 *   3. Surface received ID tokens through an RxJS Subject so components can
 *      subscribe and hand the token to AuthService.googleLogin().
 *
 * Notes:
 *   - We don't load the GIS script here — it's declared in index.html so the
 *     browser can fetch it in parallel with the app bundle.
 *   - On failure to find `window.google`, we retry a few times with a small
 *     backoff before giving up. This covers the race between `app-init` and
 *     the GIS script finishing download.
 */
@Injectable({ providedIn: 'root' })
export class GoogleAuthService {
  private initialized = false;
  private readonly tokenSubject = new Subject<string>();

  /** Emits Google ID tokens received from the GIS callback. */
  public readonly idToken$: Observable<string> = this.tokenSubject.asObservable();

  /**
   * Initialize GIS. Safe to call more than once — subsequent calls are no-ops.
   * Resolves when `google.accounts.id.initialize()` has been called with our
   * client_id, or rejects after a short retry window if GIS never loads.
   */
  private async ensureInitialized(): Promise<void> {
    if (this.initialized) return;

    const google = await this.waitForGoogle();
    const clientId = environment.oauth.google.clientId;

    google.accounts.id.initialize({
      client_id: clientId,
      callback: (response) => this.handleCredentialResponse(response),
      auto_select: false,
      cancel_on_tap_outside: true,
      context: 'signin',
      ux_mode: 'popup'
    });

    this.initialized = true;
  }

  /**
   * Render the official Google Sign-In button into `parent`. Caller is
   * responsible for providing an in-DOM element — typically called from
   * `ngAfterViewInit` with a `#googleBtnContainer` reference.
   */
  async renderButton(parent: HTMLElement): Promise<void> {
    try {
      await this.ensureInitialized();
      window.google!.accounts.id.renderButton(parent, {
        type: 'standard',
        theme: 'outline',
        size: 'large',
        text: 'signin_with',
        shape: 'rectangular',
        logo_alignment: 'left',
        width: parent.clientWidth || 320
      });
    } catch {
      // GIS failed to load (network, adblock, etc.) — leave the fallback
      // handcrafted button visible. No toast: the user may never click it.
    }
  }

  /** Programmatically trigger the One Tap prompt (optional). */
  async prompt(): Promise<void> {
    await this.ensureInitialized();
    window.google!.accounts.id.prompt();
  }

  private handleCredentialResponse(response: GoogleCredentialResponse): void {
    if (response?.credential) {
      this.tokenSubject.next(response.credential);
    }
  }

  /**
   * Poll briefly for `window.google`. GIS loads async so the app can finish
   * booting before the script is ready.
   */
  private waitForGoogle(): Promise<GoogleNamespace> {
    return new Promise((resolve, reject) => {
      const start = Date.now();
      const timeoutMs = 5000;
      const tick = () => {
        if (window.google?.accounts?.id) {
          resolve(window.google);
          return;
        }
        if (Date.now() - start > timeoutMs) {
          reject(new Error('Google Identity Services failed to load within 5s'));
          return;
        }
        setTimeout(tick, 100);
      };
      tick();
    });
  }
}
