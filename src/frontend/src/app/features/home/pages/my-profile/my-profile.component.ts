import { Component, OnInit } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ValidationErrors, Validators } from '@angular/forms';
import { AuthService } from '../../../../core/services/auth.service';
import { EmailSummaryService } from '../../../../core/services/email-summary.service';
import {
  DayOfWeek,
  EmailSummaryFrequency,
  EmailSummaryPreferenceResponse,
  MonthlyDayMode,
  UpdateEmailSummaryPreferenceRequest
} from '../../../../core/models/email-summary.model';

@Component({
  selector: 'app-my-profile',
  templateUrl: './my-profile.component.html',
  styleUrls: ['../shared-page.scss', './my-profile.component.scss']
})
export class MyProfileComponent implements OnInit {
  profileForm: FormGroup;
  passwordForm: FormGroup;
  emailSummaryForm: FormGroup;

  recipientEmail = '';
  emailSummaryPreference: EmailSummaryPreferenceResponse | null = null;

  emailSummaryLoading = false;
  emailSummarySaving = false;
  emailSummarySuccess = false;
  emailSummaryError = '';

  emailSummarySendingNow = false;

  profileUpdating = false;
  profileSuccess = false;
  profileError = '';

  passwordUpdating = false;
  passwordSuccess = false;
  passwordError = '';

  private readonly defaultTimeZoneId = 'Asia/Kolkata';

  readonly frequencies: EmailSummaryFrequency[] = ['Daily', 'Weekly', 'Monthly'];
  readonly weekDays: DayOfWeek[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
  readonly monthlyDayModes: MonthlyDayMode[] = ['DayOfMonth', 'LastDay'];

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private emailSummaryService: EmailSummaryService
  ) {
    this.profileForm = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(2)]]
    });

    this.passwordForm = this.fb.group({
      newPassword: ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: ['', Validators.required]
    }, { validators: this.passwordMatchValidator });

    this.emailSummaryForm = this.fb.group({
      isEnabled: [false],
      frequency: ['Daily' as EmailSummaryFrequency],
      timeOfDay: ['09:00'],
      dayOfWeek: [null as DayOfWeek | null],
      monthlyDayMode: ['DayOfMonth' as MonthlyDayMode],
      dayOfMonth: [1]
    }, { validators: this.emailSummaryScheduleValidator });
  }

  ngOnInit(): void {
    const user = this.authService.getCurrentUser();
    if (user) {
      this.profileForm.patchValue({ name: user.name });
      this.recipientEmail = user.email;
    }

    this.loadEmailSummaryPreference();
  }

  passwordMatchValidator(g: FormGroup) {
    return g.get('newPassword')?.value === g.get('confirmPassword')?.value
      ? null : { mismatch: true };
  }

  emailSummaryScheduleValidator = (group: AbstractControl): ValidationErrors | null => {
    const isEnabled = group.get('isEnabled')?.value === true;
    if (!isEnabled) return null;

    const frequency = group.get('frequency')?.value as EmailSummaryFrequency | null;
    const timeOfDay = (group.get('timeOfDay')?.value as string | null) ?? '';
    if (!timeOfDay) return { timeOfDayRequired: true };

    if (frequency === 'Weekly') {
      const dayOfWeek = group.get('dayOfWeek')?.value as DayOfWeek | null;
      return dayOfWeek ? null : { weeklyDayRequired: true };
    }

    if (frequency === 'Monthly') {
      const monthlyDayMode = group.get('monthlyDayMode')?.value as MonthlyDayMode | null;
      if (!monthlyDayMode) return { monthlyDayModeRequired: true };

      if (monthlyDayMode === 'DayOfMonth') {
        const dayOfMonth = Number(group.get('dayOfMonth')?.value);
        if (!Number.isFinite(dayOfMonth)) return { monthlyDayOfMonthRequired: true };
        if (dayOfMonth < 1 || dayOfMonth > 28) return { monthlyDayOfMonthRange: true };
      }
    }

    return null;
  };

  private loadEmailSummaryPreference(): void {
    this.emailSummaryLoading = true;
    this.emailSummaryError = '';

    this.emailSummaryService.getPreference().subscribe({
      next: (pref) => {
        this.emailSummaryLoading = false;
        this.emailSummaryPreference = pref;
        this.patchEmailSummaryForm(pref);
      },
      error: (err) => {
        this.emailSummaryLoading = false;
        this.emailSummaryError = err.error?.message || 'Failed to load email summary preference';
      }
    });
  }

  private patchEmailSummaryForm(pref: EmailSummaryPreferenceResponse): void {
    this.emailSummaryForm.patchValue({
      isEnabled: pref.isEnabled,
      frequency: (pref.frequency ?? 'Daily') as EmailSummaryFrequency,
      timeOfDay: pref.timeOfDay ?? '09:00',
      dayOfWeek: pref.weeklyDayOfWeek ?? null,
      monthlyDayMode: (pref.monthlyDayMode ?? 'DayOfMonth') as MonthlyDayMode,
      dayOfMonth: pref.monthlyDayOfMonth ?? 1
    }, { emitEvent: false });
    this.emailSummaryForm.updateValueAndValidity({ emitEvent: false });
  }

  onSaveEmailSummaryPreference(): void {
    this.emailSummarySuccess = false;
    this.emailSummaryError = '';

    this.emailSummaryForm.updateValueAndValidity();
    if (this.emailSummaryForm.invalid) return;

    this.emailSummarySaving = true;

    const body = this.buildUpdateEmailSummaryPreferenceRequest();
    this.emailSummaryService.updatePreference(body).subscribe({
      next: (pref) => {
        this.emailSummarySaving = false;
        this.emailSummaryPreference = pref;
        this.patchEmailSummaryForm(pref);
        this.emailSummarySuccess = true;
        setTimeout(() => this.emailSummarySuccess = false, 3000);
      },
      error: (err) => {
        this.emailSummarySaving = false;
        this.emailSummaryError = err.error?.message || 'Failed to save email summary preference';
      }
    });
  }

  onSendSummaryNow(): void {
    this.emailSummarySuccess = false;
    this.emailSummaryError = '';
    this.emailSummarySendingNow = true;

    this.emailSummaryService.sendNow().subscribe({
      next: (pref) => {
        this.emailSummarySendingNow = false;
        this.emailSummaryPreference = pref;
      },
      error: (err) => {
        this.emailSummarySendingNow = false;
        this.emailSummaryError = err.error?.message || 'Failed to queue summary email';
      }
    });
  }

  private buildUpdateEmailSummaryPreferenceRequest(): UpdateEmailSummaryPreferenceRequest {
    const v = this.emailSummaryForm.value;
    const isEnabled = v.isEnabled === true;

    if (!isEnabled) {
      const disabled: UpdateEmailSummaryPreferenceRequest = {
        isEnabled: false,
        frequency: null,
        timeOfDay: null,
        weeklyDayOfWeek: null,
        monthlyDayMode: null,
        monthlyDayOfMonth: null,
        timeZoneId: null
      };
      return disabled;
    }

    const frequency = v.frequency as EmailSummaryFrequency;
    const request: UpdateEmailSummaryPreferenceRequest = {
      isEnabled: true,
      frequency,
      timeOfDay: v.timeOfDay ?? null,
      weeklyDayOfWeek: frequency === 'Weekly' ? (v.dayOfWeek ?? null) : null,
      monthlyDayMode: frequency === 'Monthly' ? (v.monthlyDayMode ?? null) : null,
      monthlyDayOfMonth: frequency === 'Monthly' && v.monthlyDayMode === 'DayOfMonth'
        ? (Number(v.dayOfMonth) || null)
        : null,
      timeZoneId: this.defaultTimeZoneId
    };

    return request;
  }

  onUpdateProfile(): void {
    if (this.profileForm.invalid) return;

    this.profileUpdating = true;
    this.profileSuccess = false;
    this.profileError = '';

    this.authService.updateProfile(this.profileForm.value).subscribe({
      next: () => {
        this.profileUpdating = false;
        this.profileSuccess = true;
        setTimeout(() => this.profileSuccess = false, 3000);
      },
      error: (err) => {
        this.profileUpdating = false;
        this.profileError = err.error?.message || 'Failed to update profile';
      }
    });
  }

  onChangePassword(): void {
    if (this.passwordForm.invalid) return;

    this.passwordUpdating = true;
    this.passwordSuccess = false;
    this.passwordError = '';

    this.authService.changePassword(this.passwordForm.value).subscribe({
      next: () => {
        this.passwordUpdating = false;
        this.passwordSuccess = true;
        this.passwordForm.reset();
        setTimeout(() => this.passwordSuccess = false, 3000);
      },
      error: (err) => {
        this.passwordUpdating = false;
        this.passwordError = err.error?.message || 'Failed to change password';
      }
    });
  }
}
