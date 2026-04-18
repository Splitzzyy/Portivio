import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Profile,
  CreateProfileRequest,
  UpdateProfileRequest
} from '../models/portfolio.model';

@Injectable({ providedIn: 'root' })
export class ProfileService {
  private readonly API_URL = `${environment.apiUrl}/profiles`;

  constructor(private http: HttpClient) {}

  list(): Observable<Profile[]> {
    return this.http.get<Profile[]>(this.API_URL);
  }

  create(body: CreateProfileRequest): Observable<Profile> {
    return this.http.post<Profile>(this.API_URL, body);
  }

  update(id: string, body: UpdateProfileRequest): Observable<Profile> {
    return this.http.put<Profile>(`${this.API_URL}/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`);
  }
}
