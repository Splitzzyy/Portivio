import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Instrument,
  CreateInstrumentRequest,
  UpdateInstrumentRequest,
  AssetType,
  CreateAssetTypeRequest
} from '../models/portfolio.model';

@Injectable({ providedIn: 'root' })
export class InstrumentService {
  private readonly INSTRUMENTS_URL = `${environment.apiUrl}/instruments`;
  private readonly ASSET_TYPES_URL = `${environment.apiUrl}/asset-types`;

  constructor(private http: HttpClient) {}

  listInstruments(assetTypeId?: string): Observable<Instrument[]> {
    let params = new HttpParams();
    if (assetTypeId) {
      params = params.set('assetTypeId', assetTypeId);
    }
    return this.http.get<Instrument[]>(this.INSTRUMENTS_URL, { params });
  }

  getInstrument(id: string): Observable<Instrument> {
    return this.http.get<Instrument>(`${this.INSTRUMENTS_URL}/${id}`);
  }

  createInstrument(body: CreateInstrumentRequest): Observable<Instrument> {
    return this.http.post<Instrument>(this.INSTRUMENTS_URL, body);
  }

  updateInstrument(id: string, body: UpdateInstrumentRequest): Observable<Instrument> {
    return this.http.put<Instrument>(`${this.INSTRUMENTS_URL}/${id}`, body);
  }

  deleteInstrument(id: string): Observable<void> {
    return this.http.delete<void>(`${this.INSTRUMENTS_URL}/${id}`);
  }

  listAssetTypes(): Observable<AssetType[]> {
    return this.http.get<AssetType[]>(this.ASSET_TYPES_URL);
  }

  createAssetType(body: CreateAssetTypeRequest): Observable<AssetType> {
    return this.http.post<AssetType>(this.ASSET_TYPES_URL, body);
  }

  deleteAssetType(id: string): Observable<void> {
    return this.http.delete<void>(`${this.ASSET_TYPES_URL}/${id}`);
  }
}
