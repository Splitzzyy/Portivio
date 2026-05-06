import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { AuthService } from '../../../../core/services/auth.service';

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
  redirectCountdown = 3;
  email = '';
  token = '';

  private destroy$ = new Subject<void>();
  private redirectTimerId: ReturnType<typeof setInterval> | null = null;

  constructor(
    private authService: AuthService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.email = this.route.snapshot.queryParamMap.get('email') || '';
    this.token = this.route.snapshot.queryParamMap.get('token') || '';

    if (!this.email || !this.token) {
      this.tokenValid = false;
      this.errorMessage = 'Invalid verification link.';
      return;
    }

    this.verify();
  }

  ngOnDestroy(): void {
    this.clearTimer();
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
            this.verified = true;
            this.startCountdown();
          } else {
            this.errorMessage = response.message || 'Verification failed.';
          }
        },
        error: (error) => {
          this.loading = false;
          this.errorMessage = error?.error?.message || 'Verification failed.';
        }
      });
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
