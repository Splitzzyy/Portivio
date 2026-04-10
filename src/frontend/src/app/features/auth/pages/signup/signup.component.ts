import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { AuthService } from '../../../../core/services/auth.service';
import { SignupForm } from '../../../../core/models/auth.model';

/** Cross-field validator: password and confirmPassword must match. */
function passwordMatchValidator(control: AbstractControl): ValidationErrors | null {
  const password = control.get('password');
  const confirmPassword = control.get('confirmPassword');
  if (!password || !confirmPassword) return null;
  return password.value === confirmPassword.value ? null : { passwordMismatch: true };
}

/**
 * Signup page. Form shape matches the backend SignupRequest
 * (Email, Name, Password, ConfirmPassword). Single `name` field rather than
 * split first/last so we don't have to guess how the backend will collate
 * them.
 */
@Component({
  selector: 'app-signup',
  templateUrl: './signup.component.html',
  styleUrls: ['./signup.component.scss']
})
export class SignupComponent implements OnInit, OnDestroy {
  signupForm: FormGroup;
  loading = false;
  submitted = false;
  errorMessage: string | null = null;
  successMessage: string | null = null;
  showPassword = false;
  showConfirmPassword = false;
  passwordStrength = 0;
  passwordStrengthText = '';

  private destroy$ = new Subject<void>();

  constructor(
    private formBuilder: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    this.signupForm = this.formBuilder.group({
      name: ['', [Validators.required, Validators.minLength(2)]],
      email: ['', [Validators.required, Validators.email]],
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

  /** Enforce uppercase + lowercase + digit + special char. */
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

    if (this.signupForm.invalid) {
      return;
    }

    this.loading = true;
    const signupData: SignupForm = {
      email: this.f['email'].value,
      name: this.f['name'].value,
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
            // If the backend auto-logs-the-user-in (accessToken present), jump
            // straight to the dashboard. Otherwise land on login so the user
            // can verify email and sign in.
            if (response.accessToken) {
              this.router.navigate(['/dashboard']);
            } else {
              this.successMessage = 'Account created. Please verify your email and log in.';
              setTimeout(() => this.router.navigate(['/auth/login']), 2000);
            }
          } else {
            this.errorMessage = response.message || 'Signup failed. Please try again.';
          }
        },
        error: error => {
          this.loading = false;
          this.errorMessage = error?.error?.message || 'Signup failed. Please try again.';
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

  goToLogin(): void {
    this.router.navigate(['/auth/login']);
  }
}
