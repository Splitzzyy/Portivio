import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { RouterTestingModule } from '@angular/router/testing';
import { ToastrService } from 'ngx-toastr';
import { of } from 'rxjs';
import { TransactionsComponent } from './transactions.component';
import { ProfileService } from '../../../../core/services/profile.service';
import { InstrumentService } from '../../../../core/services/instrument.service';
import { TransactionService } from '../../../../core/services/transaction.service';
import { ModalService } from '../../../../core/services/modal.service';
import { Instrument, PagedResult, Profile, Transaction } from '../../../../core/models/portfolio.model';

describe('TransactionsComponent', () => {
  let component: TransactionsComponent;
  let fixture: ComponentFixture<TransactionsComponent>;
  let profileSvc: jasmine.SpyObj<ProfileService>;
  let instrumentSvc: jasmine.SpyObj<InstrumentService>;
  let transactionSvc: jasmine.SpyObj<TransactionService>;
  let modalSvc: jasmine.SpyObj<ModalService>;
  let toastr: jasmine.SpyObj<ToastrService>;

  const mockProfile: Profile = {
    id: 'p1', userId: 'u1', name: 'Personal',
    baseCurrency: 'INR', description: '', createdAt: '2025-01-01'
  };
  const mockInstruments: Instrument[] = [
    { id: 'i1', assetTypeId: 'at1', assetTypeName: 'Equity', name: 'TCS', symbol: 'TCS', currency: 'INR' }
  ];

  function buildPage(items: Transaction[], page = 1, pageSize = 25, total = items.length): PagedResult<Transaction> {
    return { items, page, pageSize, total, hasMore: page * pageSize < total };
  }

  function buildTx(id: string, isDeleted = false): Transaction {
    return {
      id, profileId: 'p1', instrumentId: 'i1',
      instrumentName: 'TCS', instrumentSymbol: 'TCS', assetTypeName: 'Stocks',
      type: 'BUY', quantity: 1, price: 100, amount: 100,
      transactionDate: '2026-05-01T00:00:00Z', notes: '', isDeleted,
      createdAtUtc: '2026-05-01T00:00:00Z'
    };
  }

  beforeEach(async () => {
    profileSvc     = jasmine.createSpyObj('ProfileService',     ['list']);
    instrumentSvc  = jasmine.createSpyObj('InstrumentService',  ['listInstruments']);
    transactionSvc = jasmine.createSpyObj('TransactionService', ['list', 'create', 'update', 'delete']);
    modalSvc       = jasmine.createSpyObj('ModalService', ['open', 'close']);
    toastr         = jasmine.createSpyObj('ToastrService',      ['success', 'error']);

    profileSvc.list.and.returnValue(of([mockProfile]));
    instrumentSvc.listInstruments.and.returnValue(of(mockInstruments));
    transactionSvc.list.and.returnValue(of(buildPage([buildTx('t1'), buildTx('t2')], 1, 25, 2)));

    await TestBed.configureTestingModule({
      declarations: [TransactionsComponent],
      imports: [CommonModule, FormsModule, RouterTestingModule],
      providers: [
        { provide: ProfileService,     useValue: profileSvc },
        { provide: InstrumentService,  useValue: instrumentSvc },
        { provide: TransactionService, useValue: transactionSvc },
        { provide: ModalService,       useValue: modalSvc },
        { provide: ToastrService,      useValue: toastr }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TransactionsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('fetch populates items and total from PagedResult envelope', () => {
    expect(transactionSvc.list).toHaveBeenCalledWith('p1', 1, 25, false, 'added');
    expect(component.transactions.length).toBe(2);
    expect(component.total).toBe(2);
    expect(component.rangeStart).toBe(1);
    expect(component.rangeEnd).toBe(2);
  });

  it('nextPage is a no-op when hasMore is false', () => {
    transactionSvc.list.calls.reset();
    component.nextPage();
    expect(transactionSvc.list).not.toHaveBeenCalled();
    expect(component.page).toBe(1);
  });

  it('counter range reflects page 2 of a multi-page result', () => {
    transactionSvc.list.and.returnValue(of(buildPage(
      Array.from({ length: 25 }, (_, i) => buildTx('p2-' + i)),
      2, 25, 87
    )));
    component.page = 2;
    component.fetch();
    expect(component.rangeStart).toBe(26);
    expect(component.rangeEnd).toBe(50);
    expect(component.total).toBe(87);
    expect(component.hasMore).toBeTrue();
  });

  it('openInvestmentModal forwards source', () => {
    component.openInvestmentModal('transactions-add-investment');

    expect(modalSvc.open).toHaveBeenCalledWith({ source: 'transactions-add-investment' });
  });
});
