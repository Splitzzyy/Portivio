import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AssetService } from './asset.service';
import { environment } from '../../../environments/environment';
import {
  AddStockRequest, AddMutualFundRequest, AddGoldRequest,
  AddPpfRequest, AddFixedDepositRequest, AddRecurringDepositRequest,
  AssetIngestResponse
} from '../models/portfolio.model';

describe('AssetService', () => {
  let service: AssetService;
  let httpMock: HttpTestingController;
  const base = environment.apiUrl;
  const profileId = 'profile-123';

  const mockResponse: AssetIngestResponse = {
    instrumentId: 'inst-1',
    instrumentName: 'Test Instrument',
    symbol: 'TEST',
    transactionId: 'txn-1',
    message: 'Created'
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AssetService]
    });
    service = TestBed.inject(AssetService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('addStock', () => {
    const req: AddStockRequest = {
      name: 'Tata Consultancy Services', symbol: 'TCS', exchange: 'NSE',
      quantity: 10, price: 4127.10, date: '2025-01-01'
    };

    it('POST to /assets/stock', () => {
      service.addStock(profileId, req).subscribe(res => expect(res).toEqual(mockResponse));
      const http = httpMock.expectOne(`${base}/profiles/${profileId}/assets/stock`);
      expect(http.request.method).toBe('POST');
      http.flush(mockResponse);
    });

    it('sends correct body', () => {
      service.addStock(profileId, req).subscribe();
      const http = httpMock.expectOne(`${base}/profiles/${profileId}/assets/stock`);
      expect(http.request.body).toEqual(req);
      http.flush(mockResponse);
    });
  });

  describe('addMutualFund', () => {
    const req: AddMutualFundRequest = {
      schemeName: 'PPFAS Flexi Cap', schemeCode: 'PPFAS-FLEXI',
      units: 100, navPerUnit: 86.42, date: '2025-01-01'
    };

    it('POST to /assets/mutual-fund', () => {
      service.addMutualFund(profileId, req).subscribe();
      const http = httpMock.expectOne(`${base}/profiles/${profileId}/assets/mutual-fund`);
      expect(http.request.method).toBe('POST');
      expect(http.request.body).toEqual(req);
      http.flush(mockResponse);
    });
  });

  describe('addGold', () => {
    const req: AddGoldRequest = {
      form: 'Digital', purity: '24K',
      weightGrams: 8, ratePerGram: 7300, makingChargesInr: 0,
      date: '2025-01-01'
    };

    it('POST to /assets/gold', () => {
      service.addGold(profileId, req).subscribe();
      const http = httpMock.expectOne(`${base}/profiles/${profileId}/assets/gold`);
      expect(http.request.method).toBe('POST');
      expect(http.request.body).toEqual(req);
      http.flush(mockResponse);
    });
  });

  describe('addPpf', () => {
    const req: AddPpfRequest = {
      accountNo: 'PPF001', openedOn: '2010-04-01',
      currentRatePercent: 7.1, initialContribution: 50000,
      contributionDate: '2025-01-01'
    };

    it('POST to /assets/ppf', () => {
      service.addPpf(profileId, req).subscribe();
      const http = httpMock.expectOne(`${base}/profiles/${profileId}/assets/ppf`);
      expect(http.request.method).toBe('POST');
      expect(http.request.body).toEqual(req);
      http.flush(mockResponse);
    });
  });

  describe('addFixedDeposit', () => {
    const req: AddFixedDepositRequest = {
      bank: 'HDFC Bank', accountNo: 'FD-001',
      principal: 200000, ratePercent: 7.1,
      compounding: 'Quarterly', payoutFrequency: 'OnMaturity',
      startDate: '2025-01-01', maturityDate: '2026-01-01',
      prematurePenaltyPct: 0
    };

    it('POST to /assets/fixed-deposit', () => {
      service.addFixedDeposit(profileId, req).subscribe();
      const http = httpMock.expectOne(`${base}/profiles/${profileId}/assets/fixed-deposit`);
      expect(http.request.method).toBe('POST');
      expect(http.request.body).toEqual(req);
      http.flush(mockResponse);
    });
  });

  describe('addRecurringDeposit', () => {
    const req: AddRecurringDepositRequest = {
      bank: 'ICICI Bank', accountNo: '',
      monthlyAmount: 5000, ratePercent: 6.5,
      startDate: '2025-01-01', tenureMonths: 12
    };

    it('POST to /assets/recurring-deposit', () => {
      service.addRecurringDeposit(profileId, req).subscribe();
      const http = httpMock.expectOne(`${base}/profiles/${profileId}/assets/recurring-deposit`);
      expect(http.request.method).toBe('POST');
      expect(http.request.body).toEqual(req);
      http.flush(mockResponse);
    });
  });
});
