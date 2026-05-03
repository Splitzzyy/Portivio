import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddStockRequest,
  AddMutualFundRequest,
  AddGoldRequest,
  AddPpfRequest,
  AddFixedDepositRequest,
  AddRecurringDepositRequest,
  AssetIngestResponse
} from '../models/portfolio.model';

@Injectable({ providedIn: 'root' })
export class AssetService {
  private base = environment.apiUrl;

  constructor(private http: HttpClient) {}

  private url(profileId: string, type: string): string {
    return `${this.base}/profiles/${profileId}/assets/${type}`;
  }

  addStock(profileId: string, body: AddStockRequest): Observable<AssetIngestResponse> {
    return this.http.post<AssetIngestResponse>(this.url(profileId, 'stock'), body);
  }

  addMutualFund(profileId: string, body: AddMutualFundRequest): Observable<AssetIngestResponse> {
    return this.http.post<AssetIngestResponse>(this.url(profileId, 'mutual-fund'), body);
  }

  addGold(profileId: string, body: AddGoldRequest): Observable<AssetIngestResponse> {
    return this.http.post<AssetIngestResponse>(this.url(profileId, 'gold'), body);
  }

  addPpf(profileId: string, body: AddPpfRequest): Observable<AssetIngestResponse> {
    return this.http.post<AssetIngestResponse>(this.url(profileId, 'ppf'), body);
  }

  addFixedDeposit(profileId: string, body: AddFixedDepositRequest): Observable<AssetIngestResponse> {
    return this.http.post<AssetIngestResponse>(this.url(profileId, 'fixed-deposit'), body);
  }

  addRecurringDeposit(profileId: string, body: AddRecurringDepositRequest): Observable<AssetIngestResponse> {
    return this.http.post<AssetIngestResponse>(this.url(profileId, 'recurring-deposit'), body);
  }
}
