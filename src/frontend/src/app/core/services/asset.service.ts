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

  private urlWithInstrument(profileId: string, type: string, instrumentId: string): string {
    return `${this.url(profileId, type)}/${instrumentId}`;
  }

  addStock(profileId: string, body: AddStockRequest): Observable<AssetIngestResponse> {
    return this.http.post<AssetIngestResponse>(this.url(profileId, 'stock'), body);
  }

  updateStock(profileId: string, instrumentId: string, body: AddStockRequest): Observable<AssetIngestResponse> {
    return this.http.put<AssetIngestResponse>(this.urlWithInstrument(profileId, 'stock', instrumentId), body);
  }

  addMutualFund(profileId: string, body: AddMutualFundRequest): Observable<AssetIngestResponse> {
    return this.http.post<AssetIngestResponse>(this.url(profileId, 'mutual-fund'), body);
  }

  updateMutualFund(profileId: string, instrumentId: string, body: AddMutualFundRequest): Observable<AssetIngestResponse> {
    return this.http.put<AssetIngestResponse>(this.urlWithInstrument(profileId, 'mutual-fund', instrumentId), body);
  }

  addGold(profileId: string, body: AddGoldRequest): Observable<AssetIngestResponse> {
    return this.http.post<AssetIngestResponse>(this.url(profileId, 'gold'), body);
  }

  updateGold(profileId: string, instrumentId: string, body: AddGoldRequest): Observable<AssetIngestResponse> {
    return this.http.put<AssetIngestResponse>(this.urlWithInstrument(profileId, 'gold', instrumentId), body);
  }

  addPpf(profileId: string, body: AddPpfRequest): Observable<AssetIngestResponse> {
    return this.http.post<AssetIngestResponse>(this.url(profileId, 'ppf'), body);
  }

  updatePpf(profileId: string, instrumentId: string, body: AddPpfRequest): Observable<AssetIngestResponse> {
    return this.http.put<AssetIngestResponse>(this.urlWithInstrument(profileId, 'ppf', instrumentId), body);
  }

  addFixedDeposit(profileId: string, body: AddFixedDepositRequest): Observable<AssetIngestResponse> {
    return this.http.post<AssetIngestResponse>(this.url(profileId, 'fixed-deposit'), body);
  }

  updateFixedDeposit(profileId: string, instrumentId: string, body: AddFixedDepositRequest): Observable<AssetIngestResponse> {
    return this.http.put<AssetIngestResponse>(this.urlWithInstrument(profileId, 'fixed-deposit', instrumentId), body);
  }

  addRecurringDeposit(profileId: string, body: AddRecurringDepositRequest): Observable<AssetIngestResponse> {
    return this.http.post<AssetIngestResponse>(this.url(profileId, 'recurring-deposit'), body);
  }

  updateRecurringDeposit(profileId: string, instrumentId: string, body: AddRecurringDepositRequest): Observable<AssetIngestResponse> {
    return this.http.put<AssetIngestResponse>(this.urlWithInstrument(profileId, 'recurring-deposit', instrumentId), body);
  }
}
