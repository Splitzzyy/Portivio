import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { MyProfileComponent } from './my-profile.component';
import { AuthService } from '../../../../core/services/auth.service';
import { EmailSummaryService } from '../../../../core/services/email-summary.service';
import { EmailSummaryPreferenceResponse } from '../../../../core/models/email-summary.model';

describe('MyProfileComponent', () => {
  let component: MyProfileComponent;
  let fixture: ComponentFixture<MyProfileComponent>;
  let authServiceSpy: jasmine.SpyObj<AuthService>;
  let emailSummaryServiceSpy: jasmine.SpyObj<EmailSummaryService>;

  beforeEach(async () => {
    authServiceSpy = jasmine.createSpyObj('AuthService', ['getCurrentUser', 'updateProfile', 'changePassword']);
    authServiceSpy.getCurrentUser.and.returnValue({ id: '1', email: 'test@example.com', name: 'Test User', isVerified: true, isActive: true });

    emailSummaryServiceSpy = jasmine.createSpyObj('EmailSummaryService', ['getPreference', 'updatePreference', 'sendNow']);
    const pref: EmailSummaryPreferenceResponse = {
      id: 'pref-1',
      userId: '1',
      isEnabled: true,
      frequency: 'Weekly',
      timeOfDay: '10:00',
      weeklyDayOfWeek: 'Monday',
      monthlyDayMode: null,
      monthlyDayOfMonth: null,
      timeZoneId: 'Asia/Kolkata',
      lastSendStatus: null,
      lastSendAttemptAtUtc: null,
      lastSendSucceededAtUtc: null,
      lastSendError: null,
      lastManualQueuedAtUtc: null,
      nextRunAtUtc: null,
      createdAtUtc: '2026-05-14T00:00:00Z',
      updatedAtUtc: '2026-05-14T00:00:00Z'
    };
    emailSummaryServiceSpy.getPreference.and.returnValue(of(pref));
    emailSummaryServiceSpy.updatePreference.and.returnValue(of(pref));
    emailSummaryServiceSpy.sendNow.and.returnValue(of({ ...pref, lastSendStatus: 'Queued' }));

    await TestBed.configureTestingModule({
      declarations: [MyProfileComponent],
      imports: [ReactiveFormsModule, HttpClientTestingModule],
      providers: [
        { provide: AuthService, useValue: authServiceSpy },
        { provide: EmailSummaryService, useValue: emailSummaryServiceSpy }
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(MyProfileComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('loads email summary preference on init', () => {
    expect(emailSummaryServiceSpy.getPreference).toHaveBeenCalled();
    expect(component.recipientEmail).toBe('test@example.com');
    expect(component.emailSummaryForm.get('frequency')?.value).toBe('Weekly');
    expect(component.emailSummaryForm.get('dayOfWeek')?.value).toBe('Monday');
    expect(component.emailSummaryForm.get('timeOfDay')?.value).toBe('10:00');
  });

  it('does not save when enabled weekly missing dayOfWeek', () => {
    component.emailSummaryForm.patchValue({
      isEnabled: true,
      frequency: 'Weekly',
      dayOfWeek: null
    });

    component.onSaveEmailSummaryPreference();
    expect(emailSummaryServiceSpy.updatePreference).not.toHaveBeenCalled();
  });

  it('saves disabled preference without schedule fields', () => {
    component.emailSummaryForm.patchValue({ isEnabled: false });

    component.onSaveEmailSummaryPreference();
    expect(emailSummaryServiceSpy.updatePreference).toHaveBeenCalled();

    const req = emailSummaryServiceSpy.updatePreference.calls.mostRecent().args[0];
    expect(req.isEnabled).toBeFalse();
  });

  it('preserves backend timezone when saving an enabled preference', () => {
    component.emailSummaryPreference = {
      ...component.emailSummaryPreference!,
      timeZoneId: 'America/New_York'
    };
    component.emailSummaryForm.patchValue({
      isEnabled: true,
      frequency: 'Daily',
      timeOfDay: '08:30',
      timeZoneId: 'America/New_York'
    });

    component.onSaveEmailSummaryPreference();

    const req = emailSummaryServiceSpy.updatePreference.calls.mostRecent().args[0];
    expect(req.timeZoneId).toBe('America/New_York');
  });

  it('disables schedule controls from the reactive form model when summaries are disabled', () => {
    component.emailSummaryForm.patchValue({ isEnabled: false });

    expect(component.emailSummaryForm.get('frequency')?.disabled).toBeTrue();
    expect(component.emailSummaryForm.get('timeOfDay')?.disabled).toBeTrue();
  });

  it('send now updates status to queued', () => {
    component.onSendSummaryNow();
    expect(emailSummaryServiceSpy.sendNow).toHaveBeenCalled();
    expect(component.emailSummaryPreference?.lastSendStatus).toBe('Queued');
  });

  it('save surfaces backend error message', () => {
    emailSummaryServiceSpy.updatePreference.and.returnValue(throwError(() => ({ error: { message: 'Nope' } })));
    component.emailSummaryForm.patchValue({ isEnabled: false });

    component.onSaveEmailSummaryPreference();
    expect(component.emailSummaryError).toBe('Nope');
  });
});
