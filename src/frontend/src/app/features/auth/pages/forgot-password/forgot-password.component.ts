import { Component, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { AuthService } from '../../../../core/services/auth.service';

/**
 * Forgot-password page. Submits an email to trigger the backend reset flow.
 * The backend returns `{ success, message }` — no session established.
 */
@Component({
  selector: 'app-forgot-password',
  templateUrl: './forgot-password.component.html',
  styleUrls: ['./forgot-password.component.scss']
})
export class ForgotPasswordComponent implements OnDestroy {
  forgotForm: FormGroup;
  loading = false;
  submitted = false;
  errorMessage: string | null = null;
  successMessage: string | null = null;
  resetEmailSent = false;
  sentEmail = '';

  private destroy$ = new Subject<void>();

  constructor(
    private formBuilder: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    this.forgotForm = this.formBuilder.group({
      email: ['', [Validators.required, Validators.email]]
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get f() {
    return this.forgotForm.controls;
  }

  onSubmit(): void {
    this.submitted = true;
    this.errorMessage = null;
    this.successMessage = null;

    if (this.forgotForm.invalid) {
      return;
    }

    this.loading = true;
    const email = this.f['email'].value;

    this.authService
      .forgotPassword({ email })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: response => {
          this.loading = false;
          if (response.success) {
            this.resetEmailSent = true;
            this.sentEmail = email;
            this.successMessage = `Password reset link has been sent to ${email}. Please check your inbox.`;
          } else {
            this.errorMessage = response.message || 'Failed to send reset email.';
          }
        },
        error: error => {
          this.loading = false;
          this.errorMessage = error?.error?.message || 'Failed to send reset email. Please try again.';
        }
      });
  }

  resendEmail(): void {
    this.submitted = false;
    this.resetEmailSent = false;
    this.successMessage = null;
    this.forgotForm.reset();
  }

  backToLogin(): void {
    this.router.navigate(['/auth/login']);
  }
}
