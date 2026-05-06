import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { AuthService } from '../../../../core/services/auth.service';
import { emailFormatValidator, normalizeEmailValue } from '../../auth-form.utils';

@Component({
  selector: 'app-verify-email',
  templateUrl: './verify-email.component.html',
  styleUrls: ['./verify-email.component.scss']
})
export class VerifyEmailComponent implements OnInit, OnDestroy {
  loading = false;
  verified = false;
  tokenValid = true;
  errorMessage: string | null = null;
  successMessage: string | null = null;
  redirectCountdown = 3;
  email = '';
  token = '';

  showResend = false;
  resendLoading = false;
  resendCooldownSeconds = 0;
  private resendCooldownTimerId: ReturnType<typeof setInterval> | null = null;
  manualEmailMode = false;
  manualResendForm!: FormGroup;
  manualResendSubmitted = false;

  private destroy$ = new Subject<void>();
  private redirectTimerId: ReturnType<typeof setInterval> | null = null;

  constructor(
    private authService: AuthService,
    private route: ActivatedRoute,
    private router: Router,
    private formBuilder: FormBuilder
  ) {}

  get f() {
    return this.manualResendForm?.controls;
  }

  ngOnInit(): void {
    this.email = this.route.snapshot.queryParamMap.get('email') || '';
    this.token = this.route.snapshot.queryParamMap.get('token') || '';

    if (!this.email || !this.token) {
      this.tokenValid = false;
      this.errorMessage = 'Invalid verification link. Please request a new email.';
      this.manualEmailMode = !this.email;
      if (this.manualEmailMode) {
        this.manualResendForm = this.formBuilder.group({
          email: ['', [Validators.required, emailFormatValidator()]]
        });
      }
      return;
    }

    this.verify();
  }

  ngOnDestroy(): void {
    this.clearTimer();
    this.clearResendCooldown();
    this.destroy$.next();
    this.destroy$.complete();
  }

  private verify(): void {
    this.loading = true;
    this.errorMessage = null;

    this.authService
      .verifyEmail({ email: this.email, verificationToken: this.token })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.loading = false;
          if (response.success) {
            this.handleSuccess();
          } else {
            this.errorMessage = response.message || 'Verification failed.';
          }
        },
        error: (error) => {
          this.loading = false;

          if (error.status === 400) {
            if (this.isAlreadyVerifiedError(error)) {
              this.handleSuccess();
            } else {
              this.errorMessage = 'Invalid or expired verification link.';
              this.showResend = true;
            }
          } else if (error.status === 404) {
            this.errorMessage = error?.error?.message || 'Account not found.';
            this.showResend = false;
          } else {
            this.errorMessage = error?.error?.message || 'Verification failed. Please try again.';
            this.showResend = false;
          }
        }
      });
  }

  private handleSuccess(): void {
    this.verified = true;
    this.startCountdown();
  }

  private isAlreadyVerifiedError(error: { error?: { message?: string } }): boolean {
    return !!error?.error?.message?.toLowerCase().includes('already verified');
  }

  onResend(): void {
    if (this.resendCooldownSeconds > 0 || this.resendLoading) return;
    this.resendLoading = true;
    this.errorMessage = null;
    this.successMessage = null;

    this.authService
      .resendVerificationEmail(this.email)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: response => {
          this.resendLoading = false;
          this.successMessage = response.message || 'Verification email sent.';
          this.startResendCooldown();
        },
        error: error => {
          this.resendLoading = false;
          this.errorMessage = error?.error?.message || 'Could not resend. Please try again.';
        }
      });
  }

  onManualResend(): void {
    this.manualResendSubmitted = true;
    if (this.manualResendForm.invalid) return;
    if (this.resendCooldownSeconds > 0 || this.resendLoading) return;
    
    const email = normalizeEmailValue(this.manualResendForm.get('email')!.value);
    this.resendLoading = true;
    this.errorMessage = null;
    this.successMessage = null;

    this.authService
      .resendVerificationEmail(email)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: response => {
          this.resendLoading = false;
          this.successMessage = response.message || 'Verification email sent.';
          this.startResendCooldown();
        },
        error: error => {
          this.resendLoading = false;
          this.errorMessage = error?.error?.message || 'Could not resend. Please try again.';
        }
      });
  }

  private startResendCooldown(): void {
    this.resendCooldownSeconds = 30;
    this.resendCooldownTimerId = setInterval(() => {
      this.resendCooldownSeconds -= 1;
      if (this.resendCooldownSeconds <= 0) {
        this.clearResendCooldown();
      }
    }, 1000);
  }

  private clearResendCooldown(): void {
    if (this.resendCooldownTimerId !== null) {
      clearInterval(this.resendCooldownTimerId);
      this.resendCooldownTimerId = null;
    }
    this.resendCooldownSeconds = 0;
  }

  private startCountdown(): void {
    this.redirectCountdown = 3;
    this.redirectTimerId = setInterval(() => {
      this.redirectCountdown--;
      if (this.redirectCountdown <= 0) {
        this.continueNow();
      }
    }, 1000);
  }

  private clearTimer(): void {
    if (this.redirectTimerId) {
      clearInterval(this.redirectTimerId);
      this.redirectTimerId = null;
    }
  }

  continueNow(): void {
    this.clearTimer();
    this.router.navigate(['/auth/login'], {
      queryParams: { verified: 'true', email: this.email }
    });
  }

  backToLogin(): void {
    this.router.navigate(['/auth/login']);
  }
}
