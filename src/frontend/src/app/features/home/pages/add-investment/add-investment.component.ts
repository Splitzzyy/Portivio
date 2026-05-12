import { Component, HostListener, OnDestroy, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subject } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { finalize, takeUntil } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';
import { Router, ActivatedRoute } from '@angular/router';
import {
  Profile, Instrument, Transaction,
  AddStockRequest, AddMutualFundRequest, AddGoldRequest,
  AddPpfRequest, AddFixedDepositRequest, AddRecurringDepositRequest,
  AssetIngestResponse
} from '../../../../core/models/portfolio.model';
import { ProfileService } from '../../../../core/services/profile.service';
import { InstrumentService } from '../../../../core/services/instrument.service';
import { TransactionService } from '../../../../core/services/transaction.service';
import { AssetService } from '../../../../core/services/asset.service';
import { ModalService } from '../../../../core/services/modal.service';

type AssetTypeId = 'STOCK' | 'MF' | 'GOLD' | 'PPF' | 'FDRD';

type AddInvestmentModalMode = 'create' | 'add-to-holding' | 'edit';

interface AddInvestmentModalPayload {
  source: string;
  holdingId?: string;
  profileId?: string;
  instrumentId?: string;
  assetTypeName?: string;
  instrumentName?: string;
  instrumentSymbol?: string;
  quantity?: number;
  price?: number;
  mode?: 'edit';
  instrument?: Instrument;
  transaction?: Transaction;
  asset?: unknown;
}

interface AssetTypeConfig {
  id: AssetTypeId;
  name: string;
  desc: string;
  icon: string;
  cls: string;
  fields: string[];
}

interface StockForm {
  name: string; symbol: string; exchange: string; isin: string;
  quantity: string; price: string; date: string; charges: string; notes: string;
}

interface MfForm {
  schemeName: string; schemeCode: string; isin: string; mode: string;
  units: string; nav: string; date: string; folio: string;
}

interface GoldForm {
  subtype: string; amount: string; grams: string; purity: string; date: string; source: string;
}

interface PpfForm {
  accountNo: string; openedOn: string; currentRatePercent: string;
  amount: string; date: string; notes: string;
}

interface FdRdForm {
  subtype: string; bank: string; accountNo: string; amount: string;
  ratePercent: string; compounding: string; startDate: string;
  maturityDate: string; tenureMonths: string; notes: string;
}

@Component({
  selector: 'app-add-investment',
  templateUrl: './add-investment.component.html',
  styleUrls: ['../shared-page.scss', './add-investment.component.scss']
})
export class AddInvestmentComponent implements OnInit, OnDestroy {
  readonly assetTypes: AssetTypeConfig[] = [
    { id: 'STOCK', name: 'Stocks', desc: 'Listed equity — NSE / BSE', icon: 'fa-chart-line', cls: 'icon-stocks', fields: ['Name', 'Quantity', 'Price'] },
    { id: 'MF', name: 'Mutual Funds', desc: 'Equity / debt / hybrid schemes', icon: 'fa-layer-group', cls: 'icon-mf', fields: ['Name', 'NAV', 'Quantity'] },
    { id: 'GOLD', name: 'Gold', desc: 'Digital, physical, SGB', icon: 'fa-coins', cls: 'icon-gold', fields: ['Amount', 'Grams'] },
    { id: 'PPF', name: 'PPF', desc: 'Public Provident Fund deposits', icon: 'fa-piggy-bank', cls: 'icon-ppf', fields: ['Amount', 'Date'] },
    { id: 'FDRD', name: 'FD / RD', desc: 'Fixed & recurring deposits', icon: 'fa-vault', cls: 'icon-fd', fields: ['Amount', 'Bank', 'Interest', 'Maturity'] },
  ];

  readonly banks = [
    'HDFC Bank', 'ICICI Bank', 'State Bank of India', 'Axis Bank', 'Kotak Mahindra Bank',
    'Bank of Baroda', 'Punjab National Bank', 'IndusInd Bank', 'Yes Bank', 'IDFC First Bank',
  ];

  environment = environment;

  readonly addingDate = new Date().toISOString().slice(0, 10);

  step: 1 | 2 = 1;
  selectedType: AssetTypeId | null = null;
  profiles: Profile[] = [];
  selectedProfileId = '';
  instruments: Instrument[] = [];
  recentTransactions: Transaction[] = [];
  saving = false;
  priceFetching = false;
  errors: Record<string, string> = {};
  modalMode: AddInvestmentModalMode = 'create';
  modalData: AddInvestmentModalPayload | null = null;

  stockQuery = '';
  stockDropdownOpen = false;
  mfQuery = '';
  mfDropdownOpen = false;

  stockForm: StockForm = this.defaultStock();
  mfForm: MfForm = this.defaultMf();
  goldForm: GoldForm = this.defaultGold();
  ppfForm: PpfForm = this.defaultPpf();
  fdRdForm: FdRdForm = this.defaultFdRd();

  private destroy$ = new Subject<void>();

  constructor(
    private profileService: ProfileService,
    private instrumentService: InstrumentService,
    private transactionService: TransactionService,
    private assetService: AssetService,
    private modalService: ModalService,
    private toastr: ToastrService,
    private http: HttpClient,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.profileService.list().pipe(takeUntil(this.destroy$)).subscribe({
      next: (data) => {
        this.profiles = data ?? [];
        if (this.profiles.length) {
          this.selectedProfileId = this.profiles[0].id;
          this.loadRecentTransactions();
        }
      }
    });

    this.instrumentService.listInstruments().pipe(takeUntil(this.destroy$)).subscribe({
      next: (data) => (this.instruments = data ?? [])
    });

    // Handle initial state from Router
    const navigation = this.router.getCurrentNavigation();
    const stateData = navigation?.extras.state?.['data'] || history.state?.['data'];
    
    if (stateData) {
      this.modalData = this.parseModalPayload(stateData);
      this.modalMode =
        this.modalData?.mode === 'edit' || !!this.modalData?.transaction
          ? 'edit'
          : this.modalData?.holdingId
            ? 'add-to-holding'
            : 'create';
      
      this.resetForModalOpen();
      this.applyModalContext();
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  @HostListener('document:click')
  closeDropdowns(): void {
    this.stockDropdownOpen = false;
    this.mfDropdownOpen = false;
  }

  loadRecentTransactions(): void {
    if (!this.selectedProfileId) return;
    this.transactionService.list(this.selectedProfileId, 1, 5)
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: (data) => (this.recentTransactions = data?.items ?? []) });
  }

  onProfileChange(): void {
    this.loadRecentTransactions();
  }

  pickType(type: AssetTypeConfig): void {
    if (this.isInstrumentSelectionLocked) return;
    this.selectedType = type.id;
    this.resetCurrentForm();
    this.errors = {};
    this.step = 2;
  }

  changeType(): void {
    if (this.isInstrumentSelectionLocked) return;
    this.selectedType = null;
    this.step = 1;
  }

  get selectedTypeConfig(): AssetTypeConfig | undefined {
    return this.assetTypes.find(t => t.id === this.selectedType);
  }

  getTxAssetConfig(tx: Transaction): AssetTypeConfig | undefined {
    const typeId = this.mapHoldingAssetType(tx.assetTypeName);
    return this.assetTypes.find(t => t.id === typeId);
  }

  get isHoldingContext(): boolean {
    return this.modalMode === 'add-to-holding' && !!this.modalData;
  }

  get isEditContext(): boolean {
    return this.modalMode === 'edit' && !!this.modalData;
  }

  get isInstrumentSelectionLocked(): boolean {
    return this.isHoldingContext || this.isEditContext;
  }

  get filteredStocks(): Instrument[] {
    const q = this.stockQuery.trim().toLowerCase();
    const stocks = this.instruments.filter(i =>
      (i.assetTypeName || '').toLowerCase().includes('equity') ||
      (i.assetTypeName || '').toLowerCase().includes('stock')
    );
    if (!q) return stocks.slice(0, 6);
    return stocks.filter(i =>
      i.symbol.toLowerCase().includes(q) || i.name.toLowerCase().includes(q)
    ).slice(0, 8);
  }

  get filteredMfs(): Instrument[] {
    const q = this.mfQuery.trim().toLowerCase();
    const mfs = this.instruments.filter(i =>
      (i.assetTypeName || '').toLowerCase().includes('mutual') ||
      (i.assetTypeName || '').toLowerCase().includes('fund')
    );
    if (!q) return mfs.slice(0, 6);
    return mfs.filter(i =>
      i.symbol.toLowerCase().includes(q) || i.name.toLowerCase().includes(q)
    ).slice(0, 8);
  }

  selectStock(inst: Instrument): void {
    if (this.isInstrumentSelectionLocked) return;
    this.stockForm.name = inst.name;
    this.stockForm.symbol = inst.symbol;
    this.stockQuery = inst.symbol;
    this.stockDropdownOpen = false;
    this.fetchStockPrice(inst.symbol, this.stockForm.exchange);
  }

  onSymbolBlur(): void {
    const sym = this.stockForm.symbol.trim().toUpperCase();
    if (sym) this.fetchStockPrice(sym, this.stockForm.exchange);
  }

  onExchangeChange(): void {
    const sym = this.stockForm.symbol.trim().toUpperCase();
    if (sym) this.fetchStockPrice(sym, this.stockForm.exchange);
  }

  private fetchStockPrice(symbol: string, exchange: string): void {
    const url = `${environment.apiUrl}/market/live-price?symbol=${encodeURIComponent(symbol)}&exchange=${exchange}`;
    this.priceFetching = true;
    this.http.get<{ lastPrice: number, companyName?: string }>(url).pipe(
      finalize(() => (this.priceFetching = false)),
      takeUntil(this.destroy$)
    ).subscribe({
      next: (res) => {
        if (res?.lastPrice != null) {
          this.stockForm.price = String(res.lastPrice);
          if (res.companyName) {
            this.stockForm.name = res.companyName;
            this.stockQuery = res.companyName;
          }
          this.toastr.success(`₹${res.lastPrice}`, res.companyName || 'Price fetched', { timeOut: 2000 });
        }
      },
      error: () => { /* silent — user enters manually */ }
    });
  }

  selectMf(inst: Instrument): void {
    if (this.isInstrumentSelectionLocked) return;
    this.mfForm.schemeName = inst.name;
    this.mfForm.schemeCode = inst.symbol;
    this.mfQuery = inst.name;
    this.mfDropdownOpen = false;
  }

  get stockTotal(): number {
    return (Number(this.stockForm.quantity) || 0) * (Number(this.stockForm.price) || 0)
      + (Number(this.stockForm.charges) || 0);
  }

  get stockAvgCost(): number {
    const qty = Number(this.stockForm.quantity) || 0;
    return qty > 0 ? this.stockTotal / qty : 0;
  }

  get mfTotal(): number {
    return (Number(this.mfForm.units) || 0) * (Number(this.mfForm.nav) || 0);
  }

  get goldRatePerGram(): number {
    const g = Number(this.goldForm.grams) || 0;
    const a = Number(this.goldForm.amount) || 0;
    return g > 0 ? a / g : 0;
  }

  get fdTenureYears(): number {
    if (!this.fdRdForm.startDate || !this.fdRdForm.maturityDate) return 0;
    const ms = new Date(this.fdRdForm.maturityDate).getTime()
      - new Date(this.fdRdForm.startDate).getTime();
    return Math.max(0, ms / (1000 * 60 * 60 * 24 * 365.25));
  }

  get fdMaturityValue(): number {
    if (this.fdRdForm.subtype === 'RD') {
      return (Number(this.fdRdForm.amount) || 0) * (Number(this.fdRdForm.tenureMonths) || 0);
    }
    const p = Number(this.fdRdForm.amount) || 0;
    const r = (Number(this.fdRdForm.ratePercent) || 0) / 100;
    const yrs = this.fdTenureYears;
    return p > 0 && r > 0 && yrs > 0 ? p * Math.pow(1 + r, yrs) : 0;
  }

  deriveFY(dateStr: string): string {
    if (!dateStr) return '—';
    try {
      const d = new Date(dateStr);
      const y = d.getMonth() >= 3 ? d.getFullYear() : d.getFullYear() - 1;
      return `FY ${String(y).slice(-2)}–${String(y + 1).slice(-2)}`;
    } catch {
      return '—';
    }
  }

  formatINR(n: number): string {
    if (!isFinite(n) || n === 0) return '—';
    return '₹' + n.toLocaleString('en-IN', { maximumFractionDigits: 2 });
  }

  validate(): Record<string, string> {
    const e: Record<string, string> = {};
    const pos = (x: string) => Number(x) > 0;
    switch (this.selectedType) {
      case 'STOCK':
        if (!this.stockForm.name) e['name'] = 'Enter stock name';
        if (!this.stockForm.symbol) e['symbol'] = 'Enter symbol';
        if (!pos(this.stockForm.quantity)) e['quantity'] = 'Enter quantity';
        if (!pos(this.stockForm.price)) e['price'] = 'Enter buy price';
        if (!this.stockForm.date) e['date'] = 'Pick a date';
        break;
      case 'MF':
        if (!this.mfForm.schemeName) e['schemeName'] = 'Enter scheme name';
        if (!this.mfForm.schemeCode) e['schemeCode'] = 'Enter scheme code';
        if (!pos(this.mfForm.nav)) e['nav'] = 'Enter NAV';
        if (!pos(this.mfForm.units)) e['units'] = 'Enter units';
        if (!this.mfForm.date) e['date'] = 'Pick a date';
        break;
      case 'GOLD':
        if (!pos(this.goldForm.amount)) e['amount'] = 'Enter amount';
        if (!pos(this.goldForm.grams)) e['grams'] = 'Enter quantity in grams';
        if (!this.goldForm.date) e['date'] = 'Pick a date';
        break;
      case 'PPF':
        if (!this.ppfForm.openedOn) e['openedOn'] = 'Enter account opening date';
        if (!pos(this.ppfForm.currentRatePercent)) e['currentRatePercent'] = 'Enter interest rate';
        if (!pos(this.ppfForm.amount)) e['amount'] = 'Enter deposit amount';
        if (!this.ppfForm.date) e['date'] = 'Pick a date';
        break;
      case 'FDRD':
        if (!pos(this.fdRdForm.amount)) e['amount'] = 'Enter amount';
        if (!this.fdRdForm.bank) e['bank'] = 'Choose a bank';
        if (!pos(this.fdRdForm.ratePercent)) e['ratePercent'] = 'Enter interest rate';
        if (!this.fdRdForm.startDate) e['startDate'] = 'Pick start date';
        if (this.fdRdForm.subtype === 'FD' && !this.fdRdForm.maturityDate) e['maturityDate'] = 'Pick maturity date';
        if (this.fdRdForm.subtype === 'RD' && !pos(this.fdRdForm.tenureMonths)) e['tenureMonths'] = 'Enter tenure in months';
        break;
    }
    return e;
  }

  submit(addAnother = false): void {
    if (!this.selectedProfileId) {
      this.toastr.error('Select a profile first');
      return;
    }
    const errs = this.validate();
    this.errors = errs;
    if (Object.keys(errs).length > 0) {
      this.toastr.error('Please fill all required fields');
      return;
    }
    const obs = this.buildApiCall();
    if (!obs) return;
    this.saving = true;
    obs.pipe(finalize(() => (this.saving = false)), takeUntil(this.destroy$)).subscribe({
      next: () => {
        const verb = this.isEditContext ? 'updated' : 'saved';
        this.toastr.success(`${this.selectedTypeConfig?.name ?? 'Investment'} ${verb} successfully`);
        if (!this.isEditContext && addAnother) {
          this.resetCurrentForm();
          this.errors = {};
          this.applyModalContext();
        } else {
          this.closeModal();
        }
      },
      error: (err) => this.toastr.error(err?.error?.message || 'Save failed')
    });
  }

  closeModal(): void {
    // Navigate back or to dashboard
    if (window.history.length > 1) {
      window.history.back();
    } else {
      this.router.navigate(['/dashboard']);
    }
  }

  private buildApiCall(): Observable<AssetIngestResponse> | null {
    const pid = this.selectedProfileId;
    const instrumentId = this.modalData?.instrumentId || '';
    const canUpdate = this.isEditContext && instrumentId;
    switch (this.selectedType) {
      case 'STOCK':
        return (canUpdate ? this.assetService.updateStock(pid, instrumentId, {
          name: this.stockForm.name,
          symbol: this.stockForm.symbol.toUpperCase(),
          exchange: this.stockForm.exchange || 'NSE',
          isin: this.stockForm.isin || undefined,
          quantity: Number(this.stockForm.quantity),
          price: Number(this.stockForm.price),
          date: this.stockForm.date,
          notes: this.stockForm.notes || undefined
        } as AddStockRequest) : this.assetService.addStock(pid, {
          name: this.stockForm.name,
          symbol: this.stockForm.symbol.toUpperCase(),
          exchange: this.stockForm.exchange || 'NSE',
          isin: this.stockForm.isin || undefined,
          quantity: Number(this.stockForm.quantity),
          price: Number(this.stockForm.price),
          date: this.stockForm.date,
          notes: this.stockForm.notes || undefined
        } as AddStockRequest));
      case 'MF':
        return (canUpdate ? this.assetService.updateMutualFund(pid, instrumentId, {
          schemeName: this.mfForm.schemeName,
          schemeCode: this.mfForm.schemeCode,
          isin: this.mfForm.isin || undefined,
          units: Number(this.mfForm.units),
          navPerUnit: Number(this.mfForm.nav),
          date: this.mfForm.date,
          notes: this.mfForm.folio || undefined
        } as AddMutualFundRequest) : this.assetService.addMutualFund(pid, {
          schemeName: this.mfForm.schemeName,
          schemeCode: this.mfForm.schemeCode,
          isin: this.mfForm.isin || undefined,
          units: Number(this.mfForm.units),
          navPerUnit: Number(this.mfForm.nav),
          date: this.mfForm.date,
          notes: this.mfForm.folio || undefined
        } as AddMutualFundRequest));
      case 'GOLD': {
        const g = Number(this.goldForm.grams) || 0;
        const a = Number(this.goldForm.amount) || 0;
        const req = {
          form: this.goldForm.subtype,
          purity: this.goldForm.purity,
          weightGrams: g,
          ratePerGram: g > 0 ? a / g : 0,
          makingChargesInr: 0,
          date: this.goldForm.date,
          notes: this.goldForm.source || undefined
        } as AddGoldRequest;
        return canUpdate ? this.assetService.updateGold(pid, instrumentId, req) : this.assetService.addGold(pid, req);
      }
      case 'PPF':
        return (canUpdate ? this.assetService.updatePpf(pid, instrumentId, {
          accountNo: this.ppfForm.accountNo || '',
          openedOn: this.ppfForm.openedOn,
          currentRatePercent: Number(this.ppfForm.currentRatePercent),
          initialContribution: Number(this.ppfForm.amount),
          contributionDate: this.ppfForm.date,
          notes: this.ppfForm.notes || undefined
        } as AddPpfRequest) : this.assetService.addPpf(pid, {
          accountNo: '',
          openedOn: this.ppfForm.openedOn,
          currentRatePercent: Number(this.ppfForm.currentRatePercent),
          initialContribution: Number(this.ppfForm.amount),
          contributionDate: this.ppfForm.date,
          notes: this.ppfForm.notes || undefined
        } as AddPpfRequest));
      case 'FDRD':
        if (this.fdRdForm.subtype === 'RD') {
          const req = {
            bank: this.fdRdForm.bank,
            accountNo: this.fdRdForm.accountNo || '',
            monthlyAmount: Number(this.fdRdForm.amount),
            ratePercent: Number(this.fdRdForm.ratePercent),
            startDate: this.fdRdForm.startDate,
            tenureMonths: Number(this.fdRdForm.tenureMonths),
            notes: this.fdRdForm.notes || undefined
          } as AddRecurringDepositRequest;
          return canUpdate ? this.assetService.updateRecurringDeposit(pid, instrumentId, req) : this.assetService.addRecurringDeposit(pid, req);
        }
        const req = {
          bank: this.fdRdForm.bank,
          accountNo: this.fdRdForm.accountNo || '',
          principal: Number(this.fdRdForm.amount),
          ratePercent: Number(this.fdRdForm.ratePercent),
          compounding: this.fdRdForm.compounding || 'Quarterly',
          payoutFrequency: 'OnMaturity',
          startDate: this.fdRdForm.startDate,
          maturityDate: this.fdRdForm.maturityDate,
          prematurePenaltyPct: 0,
          notes: this.fdRdForm.notes || undefined
        } as AddFixedDepositRequest;
        return canUpdate ? this.assetService.updateFixedDeposit(pid, instrumentId, req) : this.assetService.addFixedDeposit(pid, req);
      default:
        return null;
    }
  }

  private resetCurrentForm(): void {
    switch (this.selectedType) {
      case 'STOCK': this.stockForm = this.defaultStock(); this.stockQuery = ''; break;
      case 'MF': this.mfForm = this.defaultMf(); this.mfQuery = ''; break;
      case 'GOLD': this.goldForm = this.defaultGold(); break;
      case 'PPF': this.ppfForm = this.defaultPpf(); break;
      case 'FDRD': this.fdRdForm = this.defaultFdRd(); break;
    }
  }

  private today(): string {
    return new Date().toISOString().slice(0, 10);
  }

  private defaultStock(): StockForm {
    return { name: '', symbol: '', exchange: 'NSE', isin: '', quantity: '', price: '', date: this.today(), charges: '', notes: '' };
  }

  private defaultMf(): MfForm {
    return { schemeName: '', schemeCode: '', isin: '', mode: 'Lumpsum', units: '', nav: '', date: this.today(), folio: '' };
  }

  private defaultGold(): GoldForm {
    return { subtype: 'Digital', amount: '', grams: '', purity: '24K', date: this.today(), source: '' };
  }

  private defaultPpf(): PpfForm {
    return { accountNo: '', openedOn: '', currentRatePercent: '7.1', amount: '', date: this.today(), notes: '' };
  }

  private defaultFdRd(): FdRdForm {
    return { subtype: 'FD', bank: '', accountNo: '', amount: '', ratePercent: '', compounding: 'Quarterly', startDate: this.today(), maturityDate: '', tenureMonths: '', notes: '' };
  }

  private resetForModalOpen(): void {
    this.step = 1;
    this.selectedType = null;
    this.errors = {};
    this.stockDropdownOpen = false;
    this.mfDropdownOpen = false;
  }

  private parseModalPayload(data: unknown): AddInvestmentModalPayload | null {
    if (!data || typeof data !== 'object') return null;
    const maybe = data as Partial<AddInvestmentModalPayload>;
    if (typeof maybe.source !== 'string' || maybe.source.trim().length === 0) return null;
    const payload: AddInvestmentModalPayload = { source: maybe.source };
    if (typeof maybe.holdingId === 'string' && maybe.holdingId.trim().length > 0) payload.holdingId = maybe.holdingId;
    if (typeof maybe.profileId === 'string' && maybe.profileId.trim().length > 0) payload.profileId = maybe.profileId;
    if (typeof maybe.instrumentId === 'string' && maybe.instrumentId.trim().length > 0) payload.instrumentId = maybe.instrumentId;
    if (typeof maybe.assetTypeName === 'string' && maybe.assetTypeName.trim().length > 0) payload.assetTypeName = maybe.assetTypeName;
    if (typeof maybe.instrumentName === 'string' && maybe.instrumentName.trim().length > 0) payload.instrumentName = maybe.instrumentName;
    if (typeof maybe.instrumentSymbol === 'string' && maybe.instrumentSymbol.trim().length > 0) payload.instrumentSymbol = maybe.instrumentSymbol;
    if (typeof maybe.quantity === 'number') payload.quantity = maybe.quantity;
    if (typeof maybe.price === 'number') payload.price = maybe.price;
    if ((maybe as any).mode === 'edit') payload.mode = 'edit';
    if ((maybe as any).instrument && typeof (maybe as any).instrument === 'object') payload.instrument = (maybe as any).instrument as Instrument;
    if ((maybe as any).transaction && typeof (maybe as any).transaction === 'object') payload.transaction = (maybe as any).transaction as Transaction;
    if ((maybe as any).asset) payload.asset = (maybe as any).asset;
    return payload;
  }

  private applyModalContext(): void {
    if (!this.modalData) return;
    if (this.isEditContext) {
      this.applyEditModalContext();
      return;
    }
    if (!this.isHoldingContext) return;
    this.applyHoldingModalContext();
  }

  private applyHoldingModalContext(): void {
    if (!this.modalData) return;
    if (this.modalData.profileId) {
      this.selectedProfileId = this.modalData.profileId;
      this.loadRecentTransactions();
    }

    const assetTypeId = this.mapHoldingAssetType(this.modalData.assetTypeName);
    if (!assetTypeId) return;

    this.selectedType = assetTypeId;
    this.step = 2;
    this.resetCurrentForm();

    const q = this.modalData.quantity != null ? String(this.modalData.quantity) : '';
    const p = this.modalData.price != null ? String(this.modalData.price) : '';

    switch (assetTypeId) {
      case 'STOCK':
        this.stockForm.name = this.modalData.instrumentName || '';
        this.stockForm.symbol = this.modalData.instrumentSymbol || '';
        this.stockForm.quantity = q;
        this.stockForm.price = p;
        this.stockQuery = this.modalData.instrumentName || this.modalData.instrumentSymbol || '';
        break;
      case 'MF':
        this.mfForm.schemeName = this.modalData.instrumentName || '';
        this.mfForm.schemeCode = this.modalData.instrumentSymbol || '';
        this.mfForm.units = q;
        this.mfForm.nav = p;
        this.mfQuery = this.modalData.instrumentName || this.modalData.instrumentSymbol || '';
        break;
      case 'GOLD':
        this.goldForm.subtype = this.resolveGoldSubtype(this.modalData.instrumentName);
        this.goldForm.grams = q;
        this.goldForm.amount = String((this.modalData.quantity || 0) * (this.modalData.price || 0));
        break;
      case 'FDRD':
        this.fdRdForm.subtype = this.resolveDepositSubtype(this.modalData.assetTypeName);
        this.fdRdForm.amount = String((this.modalData.quantity || 0) * (this.modalData.price || 0));
        break;
    }
  }

  private applyEditModalContext(): void {
    if (!this.modalData) return;

    if (this.modalData.profileId) {
      this.selectedProfileId = this.modalData.profileId;
      this.loadRecentTransactions();
    }

    const assetTypeId = this.mapHoldingAssetType(this.modalData.assetTypeName || this.modalData.instrument?.assetTypeName);
    if (!assetTypeId) return;

    this.selectedType = assetTypeId;
    this.step = 2;
    this.resetCurrentForm();

    const tx = this.modalData.transaction;
    const inst = this.modalData.instrument;
    const date = (s: string | undefined) => (s || '').slice(0, 10);

    switch (assetTypeId) {
      case 'STOCK': {
        const stock = this.modalData.asset as Partial<AddStockRequest> | undefined;
        this.stockForm.name = stock?.name || this.modalData.instrumentName || inst?.name || '';
        this.stockForm.symbol = stock?.symbol || this.modalData.instrumentSymbol || inst?.symbol || '';
        this.stockForm.exchange = stock?.exchange || 'NSE';
        this.stockForm.isin = stock?.isin || '';
        if (tx) {
          this.stockForm.quantity = String(tx.quantity ?? '');
          this.stockForm.price = String(tx.price ?? '');
          this.stockForm.date = date(tx.transactionDate);
          this.stockForm.notes = tx.notes || '';
        } else if (stock) {
          this.stockForm.quantity = stock.quantity != null ? String(stock.quantity) : '';
          this.stockForm.price = stock.price != null ? String(stock.price) : '';
          this.stockForm.date = (stock.date || this.today()).slice(0, 10);
          this.stockForm.notes = (stock as any).notes || '';
        } else if (this.modalData.quantity != null || this.modalData.price != null) {
          this.stockForm.quantity = this.modalData.quantity != null ? String(this.modalData.quantity) : '';
          this.stockForm.price = this.modalData.price != null ? String(this.modalData.price) : '';
        }
        this.stockQuery = this.stockForm.name || this.stockForm.symbol;
        break;
      }
      case 'MF': {
        const mf = this.modalData.asset as Partial<AddMutualFundRequest> | undefined;
        this.mfForm.schemeName = mf?.schemeName || this.modalData.instrumentName || inst?.name || '';
        this.mfForm.schemeCode = mf?.schemeCode || this.modalData.instrumentSymbol || inst?.symbol || '';
        this.mfForm.isin = mf?.isin || '';
        if (tx) {
          this.mfForm.units = String(tx.quantity ?? '');
          this.mfForm.nav = String(tx.price ?? '');
          this.mfForm.date = date(tx.transactionDate);
          this.mfForm.folio = tx.notes || '';
        } else if (mf) {
          this.mfForm.units = mf.units != null ? String(mf.units) : '';
          this.mfForm.nav = mf.navPerUnit != null ? String(mf.navPerUnit) : '';
          this.mfForm.date = (mf.date || this.today()).slice(0, 10);
          this.mfForm.folio = (mf as any).notes || '';
        } else if (this.modalData.quantity != null || this.modalData.price != null) {
          this.mfForm.units = this.modalData.quantity != null ? String(this.modalData.quantity) : '';
          this.mfForm.nav = this.modalData.price != null ? String(this.modalData.price) : '';
        }
        this.mfQuery = this.mfForm.schemeName || this.mfForm.schemeCode;
        break;
      }
      case 'GOLD': {
        const gold = this.modalData.asset as Partial<AddGoldRequest> | undefined;
        this.goldForm.subtype = gold?.form || this.resolveGoldSubtype(this.modalData.instrumentName || inst?.name);
        this.goldForm.purity = gold?.purity || '24K';
        if (tx) {
          this.goldForm.grams = String(tx.quantity ?? '');
          this.goldForm.amount = String(tx.amount ?? '');
          this.goldForm.date = date(tx.transactionDate);
          this.goldForm.source = tx.notes || '';
        } else if (gold) {
          const total = (gold.weightGrams || 0) * (gold.ratePerGram || 0) + (gold.makingChargesInr || 0);
          this.goldForm.grams = gold.weightGrams != null ? String(gold.weightGrams) : '';
          this.goldForm.amount = total ? String(total) : '';
          this.goldForm.date = (gold.date || this.today()).slice(0, 10);
          this.goldForm.source = (gold as any).notes || '';
        } else if (this.modalData.quantity != null || this.modalData.price != null) {
          this.goldForm.grams = this.modalData.quantity != null ? String(this.modalData.quantity) : '';
          this.goldForm.amount = String((this.modalData.quantity || 0) * (this.modalData.price || 0));
        }
        break;
      }
      case 'PPF': {
        const ppf = this.modalData.asset as Partial<AddPpfRequest> | undefined;
        this.ppfForm.accountNo = ppf?.accountNo || '';
        this.ppfForm.openedOn = ppf?.openedOn ? (ppf.openedOn as any).slice(0, 10) : '';
        this.ppfForm.currentRatePercent = ppf?.currentRatePercent != null ? String(ppf.currentRatePercent) : this.ppfForm.currentRatePercent;
        if (tx) {
          this.ppfForm.amount = String(tx.amount ?? '');
          this.ppfForm.date = date(tx.transactionDate);
          this.ppfForm.notes = tx.notes || '';
        } else if (ppf) {
          this.ppfForm.amount = ppf.initialContribution != null ? String(ppf.initialContribution) : '';
          this.ppfForm.date = ppf.contributionDate ? (ppf.contributionDate as any).slice(0, 10) : this.today();
          this.ppfForm.notes = (ppf as any).notes || '';
        } else if (this.modalData.quantity != null || this.modalData.price != null) {
          this.ppfForm.amount = String((this.modalData.quantity || 0) * (this.modalData.price || 0));
        }
        break;
      }
      case 'FDRD': {
        const maybe = this.modalData.asset as Partial<AddFixedDepositRequest & AddRecurringDepositRequest> | undefined;
        // Infer subtype either from asset payload, assetTypeName, or transaction notes/instrument name.
        this.fdRdForm.subtype =
          (maybe && (maybe as any).monthlyAmount != null) ? 'RD' :
          (maybe && (maybe as any).principal != null) ? 'FD' :
          this.resolveDepositSubtype(this.modalData.assetTypeName || this.modalData.instrument?.assetTypeName);

        if (this.fdRdForm.subtype === 'RD') {
          const rd = this.modalData.asset as Partial<AddRecurringDepositRequest> | undefined;
          this.fdRdForm.bank = rd?.bank || '';
          this.fdRdForm.accountNo = rd?.accountNo || '';
          this.fdRdForm.ratePercent = rd?.ratePercent != null ? String(rd.ratePercent) : '';
          if (tx) {
            this.fdRdForm.amount = String(tx.amount ?? '');
            this.fdRdForm.startDate = date(tx.transactionDate);
            this.fdRdForm.notes = tx.notes || '';
          } else if (rd) {
            this.fdRdForm.amount = rd.monthlyAmount != null ? String(rd.monthlyAmount) : '';
            this.fdRdForm.startDate = rd.startDate ? (rd.startDate as any).slice(0, 10) : this.today();
            this.fdRdForm.tenureMonths = rd.tenureMonths != null ? String(rd.tenureMonths) : '';
            this.fdRdForm.notes = (rd as any).notes || '';
          } else if (this.modalData.quantity != null || this.modalData.price != null) {
            this.fdRdForm.amount = String((this.modalData.quantity || 0) * (this.modalData.price || 0));
          }
        } else {
          const fd = this.modalData.asset as Partial<AddFixedDepositRequest> | undefined;
          this.fdRdForm.bank = fd?.bank || '';
          this.fdRdForm.accountNo = fd?.accountNo || '';
          this.fdRdForm.ratePercent = fd?.ratePercent != null ? String(fd.ratePercent) : '';
          this.fdRdForm.compounding = fd?.compounding || this.fdRdForm.compounding;
          if (tx) {
            this.fdRdForm.amount = String(tx.amount ?? '');
            this.fdRdForm.startDate = date(tx.transactionDate);
            this.fdRdForm.notes = tx.notes || '';
          } else if (fd) {
            this.fdRdForm.amount = fd.principal != null ? String(fd.principal) : '';
            this.fdRdForm.startDate = fd.startDate ? (fd.startDate as any).slice(0, 10) : this.today();
            this.fdRdForm.maturityDate = fd.maturityDate ? (fd.maturityDate as any).slice(0, 10) : '';
            this.fdRdForm.notes = (fd as any).notes || '';
          } else if (this.modalData.quantity != null || this.modalData.price != null) {
            this.fdRdForm.amount = String((this.modalData.quantity || 0) * (this.modalData.price || 0));
          }
        }
        break;
      }
    }
  }

  private mapHoldingAssetType(assetTypeName: string | undefined): AssetTypeId | null {
    const normalized = (assetTypeName || '').toLowerCase();
    if (!normalized) return null;
    if (normalized.includes('equity') || normalized.includes('stock')) return 'STOCK';
    if (normalized.includes('mutual') || normalized.includes('fund')) return 'MF';
    if (normalized.includes('gold')) return 'GOLD';
    if (normalized.includes('ppf')) return 'PPF';
    if (normalized.includes('fixed') || normalized.includes('fd') || normalized.includes('recurring') || normalized.includes('rd')) return 'FDRD';
    return null;
  }

  private resolveGoldSubtype(instrumentName: string | undefined): string {
    const normalized = (instrumentName || '').toLowerCase();
    if (normalized.includes('physical')) return 'Physical';
    if (normalized.includes('sgb')) return 'SGB';
    return 'Digital';
  }

  private resolveDepositSubtype(assetTypeName: string | undefined): string {
    const normalized = (assetTypeName || '').toLowerCase();
    return normalized.includes('recurring') || normalized.includes('rd') ? 'RD' : 'FD';
  }
}
