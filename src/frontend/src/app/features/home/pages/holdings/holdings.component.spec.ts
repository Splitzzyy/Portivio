import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { RouterTestingModule } from '@angular/router/testing';
import { ToastrService } from 'ngx-toastr';
import { of, throwError } from 'rxjs';
import { HoldingsComponent } from './holdings.component';
import { ProfileService } from '../../../../core/services/profile.service';
import { InstrumentService } from '../../../../core/services/instrument.service';
import { HoldingService } from '../../../../core/services/holding.service';
import { ModalService } from '../../../../core/services/modal.service';
import { Holding, Instrument, Profile } from '../../../../core/models/portfolio.model';

describe('HoldingsComponent', () => {
  let component: HoldingsComponent;
  let fixture: ComponentFixture<HoldingsComponent>;
  let profileSvc: jasmine.SpyObj<ProfileService>;
  let instrumentSvc: jasmine.SpyObj<InstrumentService>;
  let holdingSvc: jasmine.SpyObj<HoldingService>;
  let modalSvc: jasmine.SpyObj<ModalService>;
  let toastr: jasmine.SpyObj<ToastrService>;

  const mockProfile: Profile = {
    id: 'p1', userId: 'u1', name: 'Personal',
    baseCurrency: 'INR', description: '', createdAt: '2025-01-01'
  };

  const mockInstruments: Instrument[] = [
    { id: 'i1', assetTypeId: 'at1', assetTypeName: 'Equity', name: 'TCS Ltd', symbol: 'TCS', currency: 'INR' }
  ];

  const initialHolding: Holding = {
    id: 'h1', profileId: 'p1', instrumentId: 'i1',
    instrumentName: 'TCS Ltd', instrumentSymbol: 'TCS', assetTypeName: 'Equity', currency: 'INR',
    quantity: 10, avgPrice: 100, currentPrice: 100, marketValue: 1000, unrealizedPnL: 0,
    lastUpdated: '2026-05-01T00:00:00Z'
  };

  const refreshedHolding: Holding = {
    ...initialHolding, currentPrice: 150, marketValue: 1500, unrealizedPnL: 500,
    lastUpdated: new Date().toISOString()
  };

  beforeEach(async () => {
    profileSvc    = jasmine.createSpyObj('ProfileService',    ['list']);
    instrumentSvc = jasmine.createSpyObj('InstrumentService', ['listInstruments']);
    holdingSvc    = jasmine.createSpyObj('HoldingService',    ['list', 'upsert', 'delete', 'refresh']);
    modalSvc      = jasmine.createSpyObj('ModalService',      ['open', 'close']);
    toastr        = jasmine.createSpyObj('ToastrService',     ['success', 'error']);

    profileSvc.list.and.returnValue(of([mockProfile]));
    instrumentSvc.listInstruments.and.returnValue(of(mockInstruments));
    holdingSvc.list.and.returnValue(of([initialHolding]));

    await TestBed.configureTestingModule({
      declarations: [HoldingsComponent],
      imports: [CommonModule, FormsModule, RouterTestingModule],
      providers: [
        { provide: ProfileService,    useValue: profileSvc },
        { provide: InstrumentService, useValue: instrumentSvc },
        { provide: HoldingService,    useValue: holdingSvc },
        { provide: ModalService,      useValue: modalSvc },
        { provide: ToastrService,     useValue: toastr }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(HoldingsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('refreshPrices replaces holdings with response and toggles spinner', () => {
    holdingSvc.refresh.and.returnValue(of([refreshedHolding]));

    component.refreshPrices();

    expect(holdingSvc.refresh).toHaveBeenCalledWith('p1');
    expect(component.refreshing).toBeFalse();
    expect(component.holdings.length).toBe(1);
    expect(component.holdings[0].currentPrice).toBe(150);
    expect(component.holdings[0].marketValue).toBe(1500);
    expect(toastr.success).toHaveBeenCalledWith('Holdings refreshed');
  });

  it('refreshPrices surfaces backend error via toastr', () => {
    holdingSvc.refresh.and.returnValue(throwError(() => ({ error: { message: 'Too many requests' } })));

    component.refreshPrices();

    expect(toastr.error).toHaveBeenCalledWith('Too many requests');
    expect(component.refreshing).toBeFalse();
  });

  it('refreshPrices is a no-op while a refresh is in flight', () => {
    component.refreshing = true;
    component.refreshPrices();
    expect(holdingSvc.refresh).not.toHaveBeenCalled();
  });

  it('formatRelative returns "just now" for recent timestamps and "Nm ago" for minutes', () => {
    const now = new Date();
    expect(component.formatRelative(now.toISOString())).toBe('just now');

    const fiveMinAgo = new Date(now.getTime() - 5 * 60_000);
    expect(component.formatRelative(fiveMinAgo.toISOString())).toBe('5m ago');

    const threeHoursAgo = new Date(now.getTime() - 3 * 60 * 60_000);
    expect(component.formatRelative(threeHoursAgo.toISOString())).toBe('3h ago');

    const twoDaysAgo = new Date(now.getTime() - 2 * 24 * 60 * 60_000);
    expect(component.formatRelative(twoDaysAgo.toISOString())).toBe('2 days ago');

    expect(component.formatRelative(null)).toBe('—');
  });

  it('openAddInvestmentModal forwards holding context for constrained modal flow', () => {
    component.openAddInvestmentModal('holdings-row-edit', initialHolding);

    expect(modalSvc.open).toHaveBeenCalledWith({
      source: 'holdings-row-edit',
      mode: 'edit',
      holdingId: 'h1',
      profileId: 'p1',
      instrumentId: 'i1',
      assetTypeName: 'Equity',
      instrumentName: 'TCS Ltd',
      instrumentSymbol: 'TCS',
      quantity: 10,
      price: 100,
      amount: 1000
    });
  });
});
