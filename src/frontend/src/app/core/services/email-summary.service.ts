import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  EmailSummaryPreferenceResponse,
  UpdateEmailSummaryPreferenceRequest
} from '../models/email-summary.model';

@Injectable({ providedIn: 'root' })
export class EmailSummaryService {
  private readonly API_URL = `${environment.apiUrl}/email-summary`;

  constructor(private http: HttpClient) {}

  getPreference(): Observable<EmailSummaryPreferenceResponse> {
    return this.http.get<EmailSummaryPreferenceResponse>(`${this.API_URL}/preferences`);
  }

  updatePreference(body: UpdateEmailSummaryPreferenceRequest): Observable<EmailSummaryPreferenceResponse> {
    return this.http.put<EmailSummaryPreferenceResponse>(`${this.API_URL}/preferences`, body);
  }

  sendNow(): Observable<EmailSummaryPreferenceResponse> {
    return this.http.post<EmailSummaryPreferenceResponse>(`${this.API_URL}/send-now`, {});
  }
}

