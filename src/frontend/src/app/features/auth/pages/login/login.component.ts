import { Component, OnInit, OnDestroy, AfterViewInit, ElementRef, ViewChild } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { AuthService } from '../../../../core/services/auth.service';
import { GoogleAuthService } from '../../../../core/services/google-auth.service';
import { ApiErrorResponse, LoginCredentials } from '../../../../core/models/auth.model';
import { emailFormatValidator, normalizeEmailControl, normalizeEmailValue } from '../../auth-form.utils';

/**
 * Login page. Supports email/password and Google Identity Services SSO.
 * The Google button is rendered by GIS into `#googleBtnContainer` on view init.
 */
@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit, OnDestroy, AfterViewInit {
  @ViewChild('googleBtnContainer') googleBtnContainer?: ElementRef<HTMLDivElement>;

  loginForm: FormGroup;
  loading = false;
  googleLoading = false;
  resendLoading = false;
  submitted = false;
  errorMessage: string | null = null;
  successMessage: string | null = null;
  showPassword = false;
  returnUrl = '/dashboard';
  pendingVerificationEmail: string | null = null;

  private destroy$ = new Subject<void>();

  constructor(
    private formBuilder: FormBuilder,
    private authService: AuthService,
    private googleAuth: GoogleAuthService,
    private router: Router,
    private route: ActivatedRoute
  ) {
    this.loginForm = this.formBuilder.group({
      email: ['', [Validators.required, emailFormatValidator()]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      rememberMe: [false]
    });
  }

  ngOnInit(): void {
    this.returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/dashboard';
    const prefilledEmail = this.route.snapshot.queryParams['email'];

    if (this.route.snapshot.queryParams['resetSuccess']) {
      this.successMessage = 'Password reset successful. Please log in.';
    }

    if (prefilledEmail) {
      this.loginForm.patchValue({
        email: normalizeEmailValue(prefilledEmail)
      });
    }

    this.googleAuth.idToken$
      .pipe(takeUntil(this.destroy$))
      .subscribe(idToken => this.exchangeGoogleToken(idToken));
  }

  ngAfterViewInit(): void {
    if (this.googleBtnContainer) {
      this.googleAuth.renderButton(this.googleBtnContainer.nativeElement);
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get f() {
    return this.loginForm.controls;
  }

  onLogin(): void {
    this.submitted = true;
    this.errorMessage = null;
    this.successMessage = null;
    this.pendingVerificationEmail = null;
    this.normalizeEmailField();

    if (this.loginForm.invalid) {
      return;
    }

    this.loading = true;
    const credentials: LoginCredentials = {
      email: normalizeEmailValue(this.f['email'].value),
      password: this.f['password'].value,
      rememberMe: this.f['rememberMe'].value
    };

    this.authService
      .login(credentials)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: response => {
          this.loading = false;
          this.pendingVerificationEmail = null;
          if (response.success) {
            this.router.navigate([this.returnUrl]);
          } else {
            this.errorMessage = response.message || 'Login failed. Please try again.';
          }
        },
        error: error => {
          this.loading = false;
          this.handleAuthError(error, credentials.email, 'Login failed. Please try again.');
        }
      });
  }

  resendVerificationEmail(): void {
    if (!this.pendingVerificationEmail || this.resendLoading) {
      return;
    }

    this.resendLoading = true;
    this.errorMessage = null;

    this.authService
      .resendVerificationEmail(this.pendingVerificationEmail)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: response => {
          this.resendLoading = false;
          this.successMessage = response.message || 'Verification email sent. Please check your inbox.';
        },
        error: error => {
          this.resendLoading = false;
          this.errorMessage = error?.error?.message || 'Could not resend verification email right now.';
        }
      });
  }

  private exchangeGoogleToken(idToken: string): void {
    this.googleLoading = true;
    this.errorMessage = null;
    this.authService
      .googleLogin(idToken)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: response => {
          this.googleLoading = false;
          if (response.success) {
            this.router.navigate([this.returnUrl]);
          } else {
            this.errorMessage = response.message || 'Google sign-in failed.';
          }
        },
        error: error => {
          this.googleLoading = false;
          this.errorMessage = error?.error?.message || 'Google sign-in is not available right now.';
        }
      });
  }

  togglePasswordVisibility(): void {
    this.showPassword = !this.showPassword;
  }

  normalizeEmailField(): void {
    normalizeEmailControl(this.loginForm.get('email'));
  }

  goToForgotPassword(): void {
    this.router.navigate(['/auth/forgot-password']);
  }

  goToSignup(): void {
    this.router.navigate(['/auth/signup']);
  }

  private handleAuthError(
    error: { status?: number; error?: ApiErrorResponse },
    email: string,
    fallbackMessage: string
  ): void {
    const backendMessage = error?.error?.message;
    this.errorMessage = backendMessage || fallbackMessage;

    if (backendMessage?.includes('Email not verified')) {
      this.pendingVerificationEmail = normalizeEmailValue(email);
    }
  }
}
