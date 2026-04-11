import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { AuthService } from '../../../../core/services/auth.service';
import { ApiErrorResponse, SignupForm } from '../../../../core/models/auth.model';
import { emailFormatValidator, normalizeEmailControl, normalizeEmailValue } from '../../auth-form.utils';

function passwordMatchValidator(control: AbstractControl): ValidationErrors | null {
  const password = control.get('password');
  const confirmPassword = control.get('confirmPassword');
  if (!password || !confirmPassword) return null;
  return password.value === confirmPassword.value ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-signup',
  templateUrl: './signup.component.html',
  styleUrls: ['./signup.component.scss']
})
export class SignupComponent implements OnInit, OnDestroy {
  signupForm: FormGroup;
  loading = false;
  resendLoading = false;
  submitted = false;
  errorMessage: string | null = null;
  successMessage: string | null = null;
  showPassword = false;
  showConfirmPassword = false;
  passwordStrength = 0;
  passwordStrengthText = '';
  pendingVerificationEmail: string | null = null;
  existingAccountEmail: string | null = null;

  private destroy$ = new Subject<void>();

  constructor(
    private formBuilder: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    this.signupForm = this.formBuilder.group({
      name: ['', [Validators.required, Validators.minLength(2)]],
      email: ['', [Validators.required, emailFormatValidator()]],
      password: ['', [Validators.required, Validators.minLength(8), this.passwordValidator.bind(this)]],
      confirmPassword: ['', Validators.required],
      acceptTerms: [false, Validators.requiredTrue]
    }, { validators: passwordMatchValidator });
  }

  ngOnInit(): void {
    this.signupForm.get('password')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.updatePasswordStrength());
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get f() {
    return this.signupForm.controls;
  }

  private passwordValidator(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (!value) return null;

    const hasUpperCase = /[A-Z]/.test(value);
    const hasLowerCase = /[a-z]/.test(value);
    const hasNumeric = /[0-9]/.test(value);
    const hasSpecialChar = /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(value);

    return (hasUpperCase && hasLowerCase && hasNumeric && hasSpecialChar)
      ? null
      : { weakPassword: true };
  }

  private updatePasswordStrength(): void {
    const password = this.signupForm.get('password')?.value || '';
    let strength = 0;

    if (password.length >= 8) strength++;
    if (password.length >= 12) strength++;
    if (/[A-Z]/.test(password)) strength++;
    if (/[0-9]/.test(password)) strength++;
    if (/[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password)) strength++;

    this.passwordStrength = (strength / 5) * 100;

    if (this.passwordStrength <= 20) this.passwordStrengthText = 'Weak';
    else if (this.passwordStrength <= 50) this.passwordStrengthText = 'Fair';
    else if (this.passwordStrength <= 80) this.passwordStrengthText = 'Good';
    else this.passwordStrengthText = 'Strong';
  }

  onSignup(): void {
    this.submitted = true;
    this.errorMessage = null;
    this.successMessage = null;
    this.pendingVerificationEmail = null;
    this.existingAccountEmail = null;
    this.normalizeEmailField();

    if (this.signupForm.invalid) {
      return;
    }

    this.loading = true;
    const email = normalizeEmailValue(this.f['email'].value);
    const signupData: SignupForm = {
      email,
      name: this.f['name'].value.trim(),
      password: this.f['password'].value,
      confirmPassword: this.f['confirmPassword'].value,
      acceptTerms: this.f['acceptTerms'].value
    };

    this.authService
      .signup(signupData)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: response => {
          this.loading = false;
          if (response.success) {
            if (response.accessToken) {
              this.router.navigate(['/dashboard']);
            } else {
              this.pendingVerificationEmail = email;
              this.successMessage = response.message || 'Account created. Please verify your email and log in.';
            }
          } else {
            this.errorMessage = response.message || 'Signup failed. Please try again.';
          }
        },
        error: error => {
          this.loading = false;
          this.handleSignupError(error, email);
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

  togglePasswordVisibility(): void {
    this.showPassword = !this.showPassword;
  }

  toggleConfirmPasswordVisibility(): void {
    this.showConfirmPassword = !this.showConfirmPassword;
  }

  normalizeEmailField(): void {
    normalizeEmailControl(this.signupForm.get('email'));
  }

  getPasswordStrengthColor(): string {
    if (this.passwordStrength <= 20) return '#ef4444';
    if (this.passwordStrength <= 50) return '#f97316';
    if (this.passwordStrength <= 80) return '#eab308';
    return '#10b981';
  }

  goToLogin(): void {
    this.router.navigate(['/auth/login'], this.existingAccountEmail
      ? { queryParams: { email: this.existingAccountEmail } }
      : undefined);
  }

  private handleSignupError(error: { status?: number; error?: ApiErrorResponse }, email: string): void {
    const backendMessage = error?.error?.message;
    this.errorMessage = backendMessage || 'Signup failed. Please try again.';

    if (backendMessage?.includes('Email already registered')) {
      this.existingAccountEmail = email;
    }
  }
}
