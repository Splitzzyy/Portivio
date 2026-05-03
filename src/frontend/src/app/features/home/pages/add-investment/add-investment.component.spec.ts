import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { ToastrService } from 'ngx-toastr';
import { of, throwError } from 'rxjs';
import { AddInvestmentComponent } from './add-investment.component';
import { ProfileService } from '../../../../core/services/profile.service';
import { InstrumentService } from '../../../../core/services/instrument.service';
import { TransactionService } from '../../../../core/services/transaction.service';
import { AssetService } from '../../../../core/services/asset.service';
import { AssetIngestResponse, Instrument, Profile } from '../../../../core/models/portfolio.model';

describe('AddInvestmentComponent', () => {
  let component: AddInvestmentComponent;
  let fixture: ComponentFixture<AddInvestmentComponent>;
  let profileSvc: jasmine.SpyObj<ProfileService>;
  let instrumentSvc: jasmine.SpyObj<InstrumentService>;
  let transactionSvc: jasmine.SpyObj<TransactionService>;
  let assetSvc: jasmine.SpyObj<AssetService>;
  let toastr: jasmine.SpyObj<ToastrService>;

  const mockProfile: Profile = {
    id: 'p1', userId: 'u1', name: 'Personal',
    baseCurrency: 'INR', description: '', createdAt: '2025-01-01'
  };

  const mockInstruments: Instrument[] = [
    { id: 'i1', assetTypeId: 'at1', assetTypeName: 'Equity', name: 'TCS Ltd', symbol: 'TCS', currency: 'INR' },
    { id: 'i2', assetTypeId: 'at2', assetTypeName: 'Mutual Fund', name: 'PPFAS Flexi Cap', symbol: 'PPFAS-FLEXI', currency: 'INR' }
  ];

  const mockIngestResponse: AssetIngestResponse = {
    instrumentId: 'i1', instrumentName: 'TCS', symbol: 'TCS',
    transactionId: 't1', message: 'Created'
  };

  beforeEach(async () => {
    profileSvc      = jasmine.createSpyObj('ProfileService',     ['list']);
    instrumentSvc   = jasmine.createSpyObj('InstrumentService',  ['listInstruments']);
    transactionSvc  = jasmine.createSpyObj('TransactionService', ['list']);
    assetSvc        = jasmine.createSpyObj('AssetService', [
      'addStock', 'addMutualFund', 'addGold', 'addPpf', 'addFixedDeposit', 'addRecurringDeposit'
    ]);
    toastr          = jasmine.createSpyObj('ToastrService', ['success', 'error']);

    profileSvc.list.and.returnValue(of([mockProfile]));
    instrumentSvc.listInstruments.and.returnValue(of(mockInstruments));
    transactionSvc.list.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [RouterTestingModule, FormsModule, CommonModule],
      declarations: [AddInvestmentComponent],
      providers: [
        { provide: ProfileService,    useValue: profileSvc     },
        { provide: InstrumentService, useValue: instrumentSvc  },
        { provide: TransactionService,useValue: transactionSvc },
        { provide: AssetService,      useValue: assetSvc       },
        { provide: ToastrService,     useValue: toastr         }
      ]
    }).compileComponents();

    fixture   = TestBed.createComponent(AddInvestmentComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  // ---- creation & init ----

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('starts on step 1 with no type selected', () => {
    expect(component.step).toBe(1);
    expect(component.selectedType).toBeNull();
  });

  it('loads profiles on init and sets first as selected', () => {
    expect(profileSvc.list).toHaveBeenCalled();
    expect(component.profiles.length).toBe(1);
    expect(component.selectedProfileId).toBe('p1');
  });

  it('loads instruments on init', () => {
    expect(instrumentSvc.listInstruments).toHaveBeenCalled();
    expect(component.instruments.length).toBe(2);
  });

  it('loads recent transactions for selected profile', () => {
    expect(transactionSvc.list).toHaveBeenCalledWith('p1', 1, 5);
  });

  // ---- navigation ----

  describe('pickType', () => {
    it('advances to step 2 with correct type', () => {
      component.pickType(component.assetTypes[0]);
      expect(component.step).toBe(2);
      expect(component.selectedType).toBe('STOCK');
    });

    it('clears errors on type pick', () => {
      component.errors = { name: 'required' };
      component.pickType(component.assetTypes[0]);
      expect(component.errors).toEqual({});
    });
  });

  describe('changeType', () => {
    it('returns to step 1 and clears selectedType', () => {
      component.pickType(component.assetTypes[0]);
      component.changeType();
      expect(component.step).toBe(1);
      expect(component.selectedType).toBeNull();
    });
  });

  describe('selectedTypeConfig', () => {
    it('returns correct config', () => {
      component.pickType(component.assetTypes[2]); // GOLD
      expect(component.selectedTypeConfig?.id).toBe('GOLD');
      expect(component.selectedTypeConfig?.name).toBe('Gold');
    });

    it('returns undefined when no type selected', () => {
      expect(component.selectedTypeConfig).toBeUndefined();
    });
  });

  // ---- validation ----

  describe('validate – STOCK', () => {
    beforeEach(() => component.pickType(component.assetTypes[0]));

    it('flags empty required fields', () => {
      component.stockForm = { name: '', symbol: '', exchange: 'NSE', isin: '', quantity: '', price: '', date: '', charges: '', notes: '' };
      const e = component.validate();
      expect(e['name']).toBeTruthy();
      expect(e['symbol']).toBeTruthy();
      expect(e['quantity']).toBeTruthy();
      expect(e['price']).toBeTruthy();
      expect(e['date']).toBeTruthy();
    });

    it('returns empty errors for valid stock form', () => {
      component.stockForm = { name: 'TCS', symbol: 'TCS', exchange: 'NSE', isin: '', quantity: '10', price: '4127', date: '2025-01-01', charges: '', notes: '' };
      expect(Object.keys(component.validate()).length).toBe(0);
    });

    it('rejects zero quantity', () => {
      component.stockForm.quantity = '0';
      expect(component.validate()['quantity']).toBeTruthy();
    });
  });

  describe('validate – MF', () => {
    beforeEach(() => component.pickType(component.assetTypes[1]));

    it('flags empty scheme name, code, nav, units', () => {
      const e = component.validate();
      expect(e['schemeName']).toBeTruthy();
      expect(e['schemeCode']).toBeTruthy();
      expect(e['nav']).toBeTruthy();
      expect(e['units']).toBeTruthy();
    });

    it('passes with valid MF form', () => {
      component.mfForm = { schemeName: 'PPFAS', schemeCode: 'PPFAS-FLEXI', isin: '', mode: 'Lumpsum', units: '100', nav: '86.42', date: '2025-01-01', folio: '' };
      expect(Object.keys(component.validate()).length).toBe(0);
    });
  });

  describe('validate – GOLD', () => {
    beforeEach(() => component.pickType(component.assetTypes[2]));

    it('flags empty amount and grams', () => {
      const e = component.validate();
      expect(e['amount']).toBeTruthy();
      expect(e['grams']).toBeTruthy();
    });
  });

  describe('validate – PPF', () => {
    beforeEach(() => component.pickType(component.assetTypes[3]));

    it('flags accountNo, openedOn, rate, amount, date', () => {
      component.ppfForm = { accountNo: '', openedOn: '', currentRatePercent: '', amount: '', date: '', notes: '' };
      const e = component.validate();
      expect(e['accountNo']).toBeTruthy();
      expect(e['openedOn']).toBeTruthy();
      expect(e['currentRatePercent']).toBeTruthy();
      expect(e['amount']).toBeTruthy();
      expect(e['date']).toBeTruthy();
    });
  });

  describe('validate – FDRD (FD)', () => {
    beforeEach(() => {
      component.pickType(component.assetTypes[4]);
      component.fdRdForm.subtype = 'FD';
    });

    it('flags amount, bank, rate, startDate, maturityDate', () => {
      component.fdRdForm = { subtype: 'FD', bank: '', accountNo: '', amount: '', ratePercent: '', compounding: 'Quarterly', startDate: '', maturityDate: '', tenureMonths: '', notes: '' };
      const e = component.validate();
      expect(e['amount']).toBeTruthy();
      expect(e['bank']).toBeTruthy();
      expect(e['ratePercent']).toBeTruthy();
      expect(e['startDate']).toBeTruthy();
      expect(e['maturityDate']).toBeTruthy();
      expect(e['tenureMonths']).toBeFalsy();
    });
  });

  describe('validate – FDRD (RD)', () => {
    beforeEach(() => {
      component.pickType(component.assetTypes[4]);
      component.fdRdForm.subtype = 'RD';
    });

    it('requires tenureMonths but NOT maturityDate', () => {
      component.fdRdForm = { subtype: 'RD', bank: '', accountNo: '', amount: '', ratePercent: '', compounding: 'Quarterly', startDate: '', maturityDate: '', tenureMonths: '', notes: '' };
      const e = component.validate();
      expect(e['tenureMonths']).toBeTruthy();
      expect(e['maturityDate']).toBeFalsy();
    });
  });

  // ---- computed getters ----

  describe('stockTotal', () => {
    it('computes quantity × price + charges', () => {
      component.stockForm.quantity = '10';
      component.stockForm.price    = '100';
      component.stockForm.charges  = '50';
      expect(component.stockTotal).toBe(1050);
    });

    it('is 0 for empty form', () => {
      expect(component.stockTotal).toBe(0);
    });
  });

  describe('stockAvgCost', () => {
    it('returns total / quantity', () => {
      component.stockForm.quantity = '10';
      component.stockForm.price    = '100';
      component.stockForm.charges  = '0';
      expect(component.stockAvgCost).toBe(100);
    });

    it('returns 0 when quantity is 0', () => {
      component.stockForm.quantity = '0';
      component.stockForm.price    = '100';
      expect(component.stockAvgCost).toBe(0);
    });
  });

  describe('mfTotal', () => {
    it('computes units × nav', () => {
      component.mfForm.units = '100';
      component.mfForm.nav   = '86.42';
      expect(component.mfTotal).toBeCloseTo(8642, 0);
    });
  });

  describe('goldRatePerGram', () => {
    it('computes amount / grams', () => {
      component.goldForm.amount = '58400';
      component.goldForm.grams  = '8';
      expect(component.goldRatePerGram).toBe(7300);
    });

    it('returns 0 when grams is empty', () => {
      component.goldForm.amount = '1000';
      component.goldForm.grams  = '';
      expect(component.goldRatePerGram).toBe(0);
    });
  });

  describe('fdMaturityValue – FD', () => {
    beforeEach(() => { component.fdRdForm.subtype = 'FD'; });

    it('returns compound interest value', () => {
      component.fdRdForm.amount       = '100000';
      component.fdRdForm.ratePercent  = '10';
      component.fdRdForm.startDate    = '2025-01-01';
      component.fdRdForm.maturityDate = '2026-01-01';
      // tenure ≈ 365/365.25 yr → P*(1.1)^0.999 ≈ 109,993
      const val = component.fdMaturityValue;
      expect(val).toBeGreaterThan(109900);
      expect(val).toBeLessThan(110100);
    });

    it('returns 0 when dates missing', () => {
      component.fdRdForm.amount      = '100000';
      component.fdRdForm.ratePercent = '10';
      component.fdRdForm.startDate   = '';
      component.fdRdForm.maturityDate = '';
      expect(component.fdMaturityValue).toBe(0);
    });
  });

  describe('fdMaturityValue – RD', () => {
    beforeEach(() => { component.fdRdForm.subtype = 'RD'; });

    it('returns installment × tenureMonths', () => {
      component.fdRdForm.amount       = '5000';
      component.fdRdForm.tenureMonths = '12';
      expect(component.fdMaturityValue).toBe(60000);
    });
  });

  describe('deriveFY', () => {
    it('returns FY 25–26 for April 2025', () => {
      expect(component.deriveFY('2025-04-01')).toBe('FY 25–26');
    });

    it('returns FY 24–25 for January 2025', () => {
      expect(component.deriveFY('2025-01-15')).toBe('FY 24–25');
    });

    it('returns — for empty string', () => {
      expect(component.deriveFY('')).toBe('—');
    });
  });

  describe('formatINR', () => {
    it('formats with ₹ prefix', () => {
      expect(component.formatINR(1000)).toContain('₹');
      expect(component.formatINR(1000)).toContain('1,000');
    });

    it('returns — for 0', () => {
      expect(component.formatINR(0)).toBe('—');
    });

    it('returns — for Infinity', () => {
      expect(component.formatINR(Infinity)).toBe('—');
    });
  });

  // ---- submit ----

  describe('submit – validation failure', () => {
    it('blocks submit when no profile selected', () => {
      component.selectedProfileId = '';
      component.pickType(component.assetTypes[0]);
      component.submit(false);
      expect(assetSvc.addStock).not.toHaveBeenCalled();
      expect(toastr.error).toHaveBeenCalledWith('Select a profile first');
    });

    it('shows error toastr and does NOT call API', () => {
      component.pickType(component.assetTypes[0]);
      component.stockForm.name = '';
      component.submit(false);
      expect(assetSvc.addStock).not.toHaveBeenCalled();
      expect(toastr.error).toHaveBeenCalled();
    });

    it('sets errors object', () => {
      component.pickType(component.assetTypes[0]);
      component.stockForm = { name: '', symbol: '', exchange: 'NSE', isin: '', quantity: '', price: '', date: '', charges: '', notes: '' };
      component.submit(false);
      expect(Object.keys(component.errors).length).toBeGreaterThan(0);
    });
  });

  describe('submit – STOCK success', () => {
    beforeEach(() => {
      component.pickType(component.assetTypes[0]);
      component.stockForm = { name: 'TCS', symbol: 'tcs', exchange: 'NSE', isin: '', quantity: '10', price: '4127', date: '2025-01-01', charges: '50', notes: 'test' };
      assetSvc.addStock.and.returnValue(of(mockIngestResponse));
    });

    it('calls addStock with correct payload (symbol uppercased)', () => {
      component.submit(false);
      expect(assetSvc.addStock).toHaveBeenCalledWith('p1', jasmine.objectContaining({
        name: 'TCS', symbol: 'TCS', exchange: 'NSE',
        quantity: 10, price: 4127, date: '2025-01-01', notes: 'test'
      }));
    });

    it('shows success toastr', () => {
      component.submit(false);
      expect(toastr.success).toHaveBeenCalled();
    });

    it('resets form and stays on step 2 for save & add another', () => {
      component.submit(true);
      expect(component.stockForm.name).toBe('');
      expect(component.errors).toEqual({});
      expect(component.step).toBe(2);
    });
  });

  describe('submit – STOCK error', () => {
    beforeEach(() => {
      component.pickType(component.assetTypes[0]);
      component.stockForm = { name: 'TCS', symbol: 'TCS', exchange: 'NSE', isin: '', quantity: '10', price: '4127', date: '2025-01-01', charges: '', notes: '' };
      assetSvc.addStock.and.returnValue(throwError(() => ({ error: { message: 'Server error' } })));
    });

    it('shows error message from API', () => {
      component.submit(false);
      expect(toastr.error).toHaveBeenCalledWith('Server error');
    });

    it('falls back to generic message when no error.message', () => {
      assetSvc.addStock.and.returnValue(throwError(() => ({})));
      component.submit(false);
      expect(toastr.error).toHaveBeenCalledWith('Save failed');
    });
  });

  describe('submit – MF', () => {
    beforeEach(() => {
      component.pickType(component.assetTypes[1]);
      component.mfForm = { schemeName: 'PPFAS Flexi', schemeCode: 'PPFAS-FLEXI', isin: 'INF123', mode: 'Lumpsum', units: '100', nav: '86.42', date: '2025-01-01', folio: 'FOLIO123' };
      assetSvc.addMutualFund.and.returnValue(of(mockIngestResponse));
    });

    it('calls addMutualFund with correct payload', () => {
      component.submit(false);
      expect(assetSvc.addMutualFund).toHaveBeenCalledWith('p1', jasmine.objectContaining({
        schemeName: 'PPFAS Flexi', schemeCode: 'PPFAS-FLEXI',
        units: 100, navPerUnit: 86.42, isin: 'INF123', notes: 'FOLIO123'
      }));
    });
  });

  describe('submit – GOLD', () => {
    beforeEach(() => {
      component.pickType(component.assetTypes[2]);
      component.goldForm = { subtype: 'SGB', amount: '58400', grams: '8', purity: '22K', date: '2025-01-01', source: 'MMTC' };
      assetSvc.addGold.and.returnValue(of(mockIngestResponse));
    });

    it('computes ratePerGram from amount / grams', () => {
      component.submit(false);
      expect(assetSvc.addGold).toHaveBeenCalledWith('p1', jasmine.objectContaining({
        form: 'SGB', purity: '22K',
        weightGrams: 8, ratePerGram: 7300, makingChargesInr: 0
      }));
    });
  });

  describe('submit – PPF', () => {
    beforeEach(() => {
      component.pickType(component.assetTypes[3]);
      component.ppfForm = { accountNo: 'PPF001', openedOn: '2010-04-01', currentRatePercent: '7.1', amount: '50000', date: '2025-04-01', notes: 'annual' };
      assetSvc.addPpf.and.returnValue(of(mockIngestResponse));
    });

    it('calls addPpf with correct payload', () => {
      component.submit(false);
      expect(assetSvc.addPpf).toHaveBeenCalledWith('p1', jasmine.objectContaining({
        accountNo: 'PPF001', openedOn: '2010-04-01',
        currentRatePercent: 7.1, initialContribution: 50000,
        contributionDate: '2025-04-01', notes: 'annual'
      }));
    });
  });

  describe('submit – FD', () => {
    beforeEach(() => {
      component.pickType(component.assetTypes[4]);
      component.fdRdForm = { subtype: 'FD', bank: 'HDFC Bank', accountNo: 'FD-001', amount: '200000', ratePercent: '7.1', compounding: 'Quarterly', startDate: '2025-01-01', maturityDate: '2026-01-01', tenureMonths: '', notes: '' };
      assetSvc.addFixedDeposit.and.returnValue(of(mockIngestResponse));
    });

    it('calls addFixedDeposit (not addRecurringDeposit)', () => {
      component.submit(false);
      expect(assetSvc.addFixedDeposit).toHaveBeenCalled();
      expect(assetSvc.addRecurringDeposit).not.toHaveBeenCalled();
    });

    it('sends correct FD payload', () => {
      component.submit(false);
      expect(assetSvc.addFixedDeposit).toHaveBeenCalledWith('p1', jasmine.objectContaining({
        bank: 'HDFC Bank', principal: 200000, ratePercent: 7.1,
        compounding: 'Quarterly', payoutFrequency: 'OnMaturity'
      }));
    });
  });

  describe('submit – RD', () => {
    beforeEach(() => {
      component.pickType(component.assetTypes[4]);
      component.fdRdForm = { subtype: 'RD', bank: 'ICICI Bank', accountNo: '', amount: '5000', ratePercent: '6.5', compounding: 'Quarterly', startDate: '2025-01-01', maturityDate: '', tenureMonths: '12', notes: '' };
      assetSvc.addRecurringDeposit.and.returnValue(of(mockIngestResponse));
    });

    it('calls addRecurringDeposit (not addFixedDeposit)', () => {
      component.submit(false);
      expect(assetSvc.addRecurringDeposit).toHaveBeenCalled();
      expect(assetSvc.addFixedDeposit).not.toHaveBeenCalled();
    });

    it('sends correct RD payload', () => {
      component.submit(false);
      expect(assetSvc.addRecurringDeposit).toHaveBeenCalledWith('p1', jasmine.objectContaining({
        bank: 'ICICI Bank', monthlyAmount: 5000,
        ratePercent: 6.5, tenureMonths: 12
      }));
    });
  });

  // ---- typeahead ----

  describe('filteredStocks', () => {
    it('returns instruments with Equity asset type', () => {
      component.stockQuery = '';
      const results = component.filteredStocks;
      expect(results.every(i => i.assetTypeName.toLowerCase().includes('equity'))).toBeTrue();
    });

    it('filters by symbol query', () => {
      component.stockQuery = 'tcs';
      expect(component.filteredStocks.length).toBe(1);
      expect(component.filteredStocks[0].symbol).toBe('TCS');
    });

    it('returns empty when no match', () => {
      component.stockQuery = 'xyz999';
      expect(component.filteredStocks.length).toBe(0);
    });
  });

  describe('filteredMfs', () => {
    it('returns instruments with Mutual Fund asset type', () => {
      component.mfQuery = '';
      const results = component.filteredMfs;
      expect(results.every(i => i.assetTypeName.toLowerCase().includes('mutual'))).toBeTrue();
    });

    it('filters by fund name', () => {
      component.mfQuery = 'ppfas';
      expect(component.filteredMfs.length).toBe(1);
      expect(component.filteredMfs[0].symbol).toBe('PPFAS-FLEXI');
    });
  });

  describe('selectStock', () => {
    it('fills stockForm name and symbol, closes dropdown', () => {
      component.stockDropdownOpen = true;
      component.selectStock(mockInstruments[0]);
      expect(component.stockForm.name).toBe('TCS Ltd');
      expect(component.stockForm.symbol).toBe('TCS');
      expect(component.stockDropdownOpen).toBeFalse();
    });
  });

  describe('selectMf', () => {
    it('fills mfForm schemeName and schemeCode, closes dropdown', () => {
      component.mfDropdownOpen = true;
      component.selectMf(mockInstruments[1]);
      expect(component.mfForm.schemeName).toBe('PPFAS Flexi Cap');
      expect(component.mfForm.schemeCode).toBe('PPFAS-FLEXI');
      expect(component.mfDropdownOpen).toBeFalse();
    });
  });

  describe('closeDropdowns', () => {
    it('closes both dropdowns on document click', () => {
      component.stockDropdownOpen = true;
      component.mfDropdownOpen    = true;
      component.closeDropdowns();
      expect(component.stockDropdownOpen).toBeFalse();
      expect(component.mfDropdownOpen).toBeFalse();
    });
  });

  describe('onProfileChange', () => {
    it('reloads recent transactions for new profile', () => {
      transactionSvc.list.calls.reset();
      component.selectedProfileId = 'p2';
      component.onProfileChange();
      expect(transactionSvc.list).toHaveBeenCalledWith('p2', 1, 5);
    });
  });
});
