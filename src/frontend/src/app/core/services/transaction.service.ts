import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Transaction,
  CreateTransactionRequest,
  UpdateTransactionRequest,
  PagedResult
} from '../models/portfolio.model';

@Injectable({ providedIn: 'root' })
export class TransactionService {
  private base(profileId: string): string {
    return `${environment.apiUrl}/profiles/${profileId}/transactions`;
  }

  constructor(private http: HttpClient) {}

  list(profileId: string, page = 1, pageSize = 50, includeDeleted = false, sortBy = 'added'): Observable<PagedResult<Transaction>> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize)
      .set('sortBy', sortBy);
    if (includeDeleted) params = params.set('includeDeleted', 'true');
    return this.http.get<PagedResult<Transaction>>(this.base(profileId), { params });
  }

  create(profileId: string, body: CreateTransactionRequest): Observable<Transaction> {
    return this.http.post<Transaction>(this.base(profileId), body);
  }

  update(profileId: string, txId: string, body: UpdateTransactionRequest): Observable<Transaction> {
    return this.http.put<Transaction>(`${this.base(profileId)}/${txId}`, body);
  }

  delete(profileId: string, txId: string): Observable<void> {
    return this.http.delete<void>(`${this.base(profileId)}/${txId}`);
  }
}
