import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Holding, UpsertHoldingRequest } from '../models/portfolio.model';

@Injectable({ providedIn: 'root' })
export class HoldingService {
  private base(profileId: string): string {
    return `${environment.apiUrl}/profiles/${profileId}/holdings`;
  }

  constructor(private http: HttpClient) {}

  list(profileId: string): Observable<Holding[]> {
    return this.http.get<Holding[]>(this.base(profileId));
  }

  upsert(profileId: string, body: UpsertHoldingRequest): Observable<Holding> {
    return this.http.post<Holding>(this.base(profileId), body);
  }

  delete(profileId: string, holdingId: string): Observable<void> {
    return this.http.delete<void>(`${this.base(profileId)}/${holdingId}`);
  }
}
