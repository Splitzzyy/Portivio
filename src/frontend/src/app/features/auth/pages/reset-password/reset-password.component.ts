import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { AuthService } from '../../../../core/services/auth.service';

/** Cross-field validator: newPassword and confirmPassword must match. */
function passwordMatchValidator(control: AbstractControl): ValidationErrors | null {
  const password = control.get('newPassword');
  const confirmPassword = control.get('confirmPassword');
  if (!password || !confirmPassword) return null;
  return password.value === confirmPassword.value ? null : { passwordMismatch: true };
}

/**
 * Reset-password page. The backend reset endpoint needs
 * `{ email, resetToken, newPassword, confirmPassword }`. The email and token
 * are passed via the reset link (/auth/reset-password/:token?email=...).
 * There is no pre-flight token validation endpoint, so we just try the reset
 * and surface any 404/400 from the server.
 */
@Component({
  selector: 'app-reset-password',
  templateUrl: './reset-password.component.html',
  styleUrls: ['./reset-password.component.scss']
})
export class ResetPasswordComponent implements OnInit, OnDestroy {
  resetForm: FormGroup;
  loading = false;
  submitted = false;
  errorMessage: string | null = null;
  successMessage: string | null = null;
  showPassword = false;
  showConfirmPassword = false;
  token = '';
  email = '';
  tokenValid = true;
  passwordStrength = 0;
  passwordStrengthText = '';

  private destroy$ = new Subject<void>();

  constructor(
    private formBuilder: FormBuilder,
    private authService: AuthService,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.resetForm = this.formBuilder.group({
      newPassword: ['', [Validators.required, Validators.minLength(8), this.passwordValidator.bind(this)]],
      confirmPassword: ['', Validators.required]
    }, { validators: passwordMatchValidator });
  }

  ngOnInit(): void {
    this.token = this.route.snapshot.paramMap.get('token') || '';
    this.email = this.route.snapshot.queryParamMap.get('email') || '';

    if (!this.token || !this.email) {
      this.tokenValid = false;
      this.errorMessage = 'Invalid reset link. Please request a new password reset email.';
    }

    this.resetForm.get('newPassword')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.updatePasswordStrength());
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get f() {
    return this.resetForm.controls;
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
    const password = this.resetForm.get('newPassword')?.value || '';
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

  onReset(): void {
    this.submitted = true;
    this.errorMessage = null;
    this.successMessage = null;

    if (this.resetForm.invalid || !this.tokenValid) {
      return;
    }

    this.loading = true;

    this.authService
      .resetPassword({
        email: this.email,
        resetToken: this.token,
        newPassword: this.f['newPassword'].value,
        confirmPassword: this.f['confirmPassword'].value
      })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: response => {
          this.loading = false;
          if (response.success) {
            this.successMessage = 'Password reset successful! Redirecting to login...';
            setTimeout(() => {
              this.router.navigate(['/auth/login'], { queryParams: { resetSuccess: true } });
            }, 2000);
          } else {
            this.errorMessage = response.message || 'Failed to reset password.';
          }
        },
        error: error => {
          this.loading = false;
          this.errorMessage = error?.error?.message || 'Failed to reset password. The link may be invalid or expired.';
        }
      });
  }

  togglePasswordVisibility(): void {
    this.showPassword = !this.showPassword;
  }

  toggleConfirmPasswordVisibility(): void {
    this.showConfirmPassword = !this.showConfirmPassword;
  }

  getPasswordStrengthColor(): string {
    if (this.passwordStrength <= 20) return '#ef4444';
    if (this.passwordStrength <= 50) return '#f97316';
    if (this.passwordStrength <= 80) return '#eab308';
    return '#10b981';
  }

  backToLogin(): void {
    this.router.navigate(['/auth/login']);
  }
}
