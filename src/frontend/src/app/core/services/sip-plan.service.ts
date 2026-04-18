import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  SIPPlan,
  CreateSIPPlanRequest,
  UpdateSIPPlanRequest
} from '../models/portfolio.model';

@Injectable({ providedIn: 'root' })
export class SipPlanService {
  private base(profileId: string): string {
    return `${environment.apiUrl}/profiles/${profileId}/sip-plans`;
  }

  constructor(private http: HttpClient) {}

  list(profileId: string, activeOnly?: boolean): Observable<SIPPlan[]> {
    let params = new HttpParams();
    if (activeOnly !== undefined) {
      params = params.set('activeOnly', activeOnly);
    }
    return this.http.get<SIPPlan[]>(this.base(profileId), { params });
  }

  create(profileId: string, body: CreateSIPPlanRequest): Observable<SIPPlan> {
    return this.http.post<SIPPlan>(this.base(profileId), body);
  }

  update(profileId: string, sipId: string, body: UpdateSIPPlanRequest): Observable<SIPPlan> {
    return this.http.put<SIPPlan>(`${this.base(profileId)}/${sipId}`, body);
  }

  activate(profileId: string, sipId: string): Observable<SIPPlan> {
    return this.http.post<SIPPlan>(`${this.base(profileId)}/${sipId}/activate`, {});
  }

  deactivate(profileId: string, sipId: string): Observable<SIPPlan> {
    return this.http.post<SIPPlan>(`${this.base(profileId)}/${sipId}/deactivate`, {});
  }

  delete(profileId: string, sipId: string): Observable<void> {
    return this.http.delete<void>(`${this.base(profileId)}/${sipId}`);
  }
}
