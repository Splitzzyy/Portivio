/**
 * Minimal ambient typings for Google Identity Services (GIS).
 * Loaded via <script src="https://accounts.google.com/gsi/client"> in index.html.
 * Keep these narrow — we only use the Sign In With Google flow.
 */

declare global {
  interface Window {
    google?: GoogleNamespace;
  }

  interface GoogleNamespace {
    accounts: {
      id: {
        initialize(config: GoogleInitConfig): void;
        prompt(momentListener?: (notification: GooglePromptMomentNotification) => void): void;
        renderButton(parent: HTMLElement, options: GoogleButtonOptions): void;
        disableAutoSelect(): void;
        cancel(): void;
      };
    };
  }

  interface GoogleInitConfig {
    client_id: string;
    callback: (response: GoogleCredentialResponse) => void;
    auto_select?: boolean;
    cancel_on_tap_outside?: boolean;
    context?: 'signin' | 'signup' | 'use';
    ux_mode?: 'popup' | 'redirect';
  }

  interface GoogleCredentialResponse {
    credential: string;
    select_by?: string;
    clientId?: string;
  }

  interface GoogleButtonOptions {
    type?: 'standard' | 'icon';
    theme?: 'outline' | 'filled_blue' | 'filled_black';
    size?: 'small' | 'medium' | 'large';
    text?: 'signin_with' | 'signup_with' | 'continue_with' | 'signin';
    shape?: 'rectangular' | 'pill' | 'circle' | 'square';
    logo_alignment?: 'left' | 'center';
    width?: number | string;
    locale?: string;
  }

  interface GooglePromptMomentNotification {
    isDisplayMoment(): boolean;
    isDisplayed(): boolean;
    isNotDisplayed(): boolean;
    getNotDisplayedReason(): string;
    isSkippedMoment(): boolean;
    getSkippedReason(): string;
    isDismissedMoment(): boolean;
    getDismissedReason(): string;
    getMomentType(): string;
  }
}

export {};
