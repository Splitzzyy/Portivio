import { Component, OnInit, OnDestroy, AfterViewInit, ElementRef, ViewChild } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../../../core/services/auth.service';
import { GoogleAuthService } from '../../../../core/services/google-auth.service';
import { LoginCredentials } from '../../../../core/models/auth.model';

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
  submitted = false;
  errorMessage: string | null = null;
  successMessage: string | null = null;
  showPassword = false;
  returnUrl = '/dashboard';

  private destroy$ = new Subject<void>();

  constructor(
    private formBuilder: FormBuilder,
    private authService: AuthService,
    private googleAuth: GoogleAuthService,
    private router: Router,
    private route: ActivatedRoute,
    private toastr: ToastrService
  ) {
    this.loginForm = this.formBuilder.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      rememberMe: [false]
    });
  }

  ngOnInit(): void {
    this.returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/dashboard';

    if (this.route.snapshot.queryParams['resetSuccess']) {
      this.successMessage = 'Password reset successful. Please log in.';
    }

    // Listen for Google ID tokens emitted by GIS and exchange for a session.
    this.googleAuth.idToken$
      .pipe(takeUntil(this.destroy$))
      .subscribe(idToken => this.exchangeGoogleToken(idToken));
  }

  ngAfterViewInit(): void {
    // Render the official Google button once GIS finishes loading.
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

    if (this.loginForm.invalid) {
      return;
    }

    this.loading = true;
    const credentials: LoginCredentials = {
      email: this.f['email'].value,
      password: this.f['password'].value,
      rememberMe: this.f['rememberMe'].value
    };

    this.authService
      .login(credentials)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: response => {
          this.loading = false;
          if (response.success) {
            this.router.navigate([this.returnUrl]);
          } else {
            this.errorMessage = response.message || 'Login failed. Please try again.';
          }
        },
        error: error => {
          this.loading = false;
          // Error interceptor already fired a toast; keep an inline message too.
          this.errorMessage = error?.error?.message || 'Login failed. Please try again.';
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
          // 501 Not Implemented surfaces here — toast already shown by error interceptor.
          this.errorMessage = error?.error?.message || 'Google sign-in is not available right now.';
        }
      });
  }

  togglePasswordVisibility(): void {
    this.showPassword = !this.showPassword;
  }

  goToForgotPassword(): void {
    this.router.navigate(['/auth/forgot-password']);
  }

  goToSignup(): void {
    this.router.navigate(['/auth/signup']);
  }
}
