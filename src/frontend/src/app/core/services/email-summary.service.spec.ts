import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { EmailSummaryService } from './email-summary.service';
import { environment } from '../../../environments/environment';
import {
  EmailSummaryPreferenceResponse,
  UpdateEmailSummaryPreferenceRequest
} from '../models/email-summary.model';

describe('EmailSummaryService', () => {
  let service: EmailSummaryService;
  let httpMock: HttpTestingController;
  const base = environment.apiUrl;

  const mockPreference: EmailSummaryPreferenceResponse = {
    id: 'pref-1',
    userId: 'user-1',
    isEnabled: true,
    frequency: 'Daily',
    timeOfDay: '09:00',
    weeklyDayOfWeek: null,
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

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [EmailSummaryService]
    });
    service = TestBed.inject(EmailSummaryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getPreference', () => {
    it('GET to /email-summary/preferences', () => {
      service.getPreference().subscribe(res => expect(res).toEqual(mockPreference));
      const http = httpMock.expectOne(`${base}/email-summary/preferences`);
      expect(http.request.method).toBe('GET');
      http.flush(mockPreference);
    });
  });

  describe('updatePreference', () => {
    const req: UpdateEmailSummaryPreferenceRequest = {
      isEnabled: true,
      frequency: 'Weekly',
      timeOfDay: '10:15',
      weeklyDayOfWeek: 'Monday',
      timeZoneId: 'Asia/Kolkata'
    };

    it('PUT to /email-summary/preferences', () => {
      service.updatePreference(req).subscribe(res => expect(res).toEqual(mockPreference));
      const http = httpMock.expectOne(`${base}/email-summary/preferences`);
      expect(http.request.method).toBe('PUT');
      expect(http.request.body).toEqual(req);
      http.flush(mockPreference);
    });
  });

  describe('sendNow', () => {
    it('POST to /email-summary/send-now', () => {
      service.sendNow().subscribe(res => expect(res).toEqual(mockPreference));
      const http = httpMock.expectOne(`${base}/email-summary/send-now`);
      expect(http.request.method).toBe('POST');
      expect(http.request.body).toEqual({});
      http.flush(mockPreference);
    });
  });
});

