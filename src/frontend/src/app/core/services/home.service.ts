import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { HomeResponse } from '../models/portfolio.model';

@Injectable({ providedIn: 'root' })
export class HomeService {
  private readonly API_URL = `${environment.apiUrl}/home`;

  constructor(private http: HttpClient) {}

  getHome(): Observable<HomeResponse> {
    return this.http.get<HomeResponse>(this.API_URL);
  }
}
