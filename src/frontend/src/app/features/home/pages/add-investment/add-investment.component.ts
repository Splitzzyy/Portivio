import { Component, HostListener, OnDestroy, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subject } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { finalize, takeUntil } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';
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

type AddInvestmentModalMode = 'create' | 'add-to-holding';

interface AddInvestmentModalPayload {
  source: string;
  holdingId?: string;
  profileId?: string;
  instrumentId?: string;
  assetTypeName?: string;
  instrumentName?: string;
  instrumentSymbol?: string;
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
  isModalOpen = false;
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
    private http: HttpClient
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

    this.modalService.state$.pipe(takeUntil(this.destroy$)).subscribe((state) => {
      this.isModalOpen = state.isOpen;
      this.modalData = this.parseModalPayload(state.data);
      this.modalMode = this.modalData?.holdingId ? 'add-to-holding' : 'create';
      if (state.isOpen) {
        this.resetForModalOpen();
        this.applyModalContext();
      }
    });
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
    this.selectedType = type.id;
    this.resetCurrentForm();
    this.errors = {};
    this.step = 2;
  }

  changeType(): void {
    if (this.isHoldingContext) return;
    this.selectedType = null;
    this.step = 1;
  }

  get selectedTypeConfig(): AssetTypeConfig | undefined {
    return this.assetTypes.find(t => t.id === this.selectedType);
  }

  get isHoldingContext(): boolean {
    return this.modalMode === 'add-to-holding' && !!this.modalData;
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
        this.toastr.success(`${this.selectedTypeConfig?.name ?? 'Investment'} saved successfully`);
        if (addAnother) {
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
    this.modalService.close();
  }

  private buildApiCall(): Observable<AssetIngestResponse> | null {
    const pid = this.selectedProfileId;
    switch (this.selectedType) {
      case 'STOCK':
        return this.assetService.addStock(pid, {
          name: this.stockForm.name,
          symbol: this.stockForm.symbol.toUpperCase(),
          exchange: this.stockForm.exchange || 'NSE',
          isin: this.stockForm.isin || undefined,
          quantity: Number(this.stockForm.quantity),
          price: Number(this.stockForm.price),
          date: this.stockForm.date,
          notes: this.stockForm.notes || undefined
        } as AddStockRequest);
      case 'MF':
        return this.assetService.addMutualFund(pid, {
          schemeName: this.mfForm.schemeName,
          schemeCode: this.mfForm.schemeCode,
          isin: this.mfForm.isin || undefined,
          units: Number(this.mfForm.units),
          navPerUnit: Number(this.mfForm.nav),
          date: this.mfForm.date,
          notes: this.mfForm.folio || undefined
        } as AddMutualFundRequest);
      case 'GOLD': {
        const g = Number(this.goldForm.grams) || 0;
        const a = Number(this.goldForm.amount) || 0;
        return this.assetService.addGold(pid, {
          form: this.goldForm.subtype,
          purity: this.goldForm.purity,
          weightGrams: g,
          ratePerGram: g > 0 ? a / g : 0,
          makingChargesInr: 0,
          date: this.goldForm.date,
          notes: this.goldForm.source || undefined
        } as AddGoldRequest);
      }
      case 'PPF':
        return this.assetService.addPpf(pid, {
          accountNo: '',
          openedOn: this.ppfForm.openedOn,
          currentRatePercent: Number(this.ppfForm.currentRatePercent),
          initialContribution: Number(this.ppfForm.amount),
          contributionDate: this.ppfForm.date,
          notes: this.ppfForm.notes || undefined
        } as AddPpfRequest);
      case 'FDRD':
        if (this.fdRdForm.subtype === 'RD') {
          return this.assetService.addRecurringDeposit(pid, {
            bank: this.fdRdForm.bank,
            accountNo: this.fdRdForm.accountNo || '',
            monthlyAmount: Number(this.fdRdForm.amount),
            ratePercent: Number(this.fdRdForm.ratePercent),
            startDate: this.fdRdForm.startDate,
            tenureMonths: Number(this.fdRdForm.tenureMonths),
            notes: this.fdRdForm.notes || undefined
          } as AddRecurringDepositRequest);
        }
        return this.assetService.addFixedDeposit(pid, {
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
        } as AddFixedDepositRequest);
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
    return payload;
  }

  private applyModalContext(): void {
    if (!this.isHoldingContext || !this.modalData) return;

    if (this.modalData.profileId) {
      this.selectedProfileId = this.modalData.profileId;
      this.loadRecentTransactions();
    }

    const assetTypeId = this.mapHoldingAssetType(this.modalData.assetTypeName);
    if (!assetTypeId) return;

    this.selectedType = assetTypeId;
    this.step = 2;
    this.resetCurrentForm();

    switch (assetTypeId) {
      case 'STOCK':
        this.stockForm.name = this.modalData.instrumentName || '';
        this.stockForm.symbol = this.modalData.instrumentSymbol || '';
        this.stockQuery = this.modalData.instrumentName || this.modalData.instrumentSymbol || '';
        break;
      case 'MF':
        this.mfForm.schemeName = this.modalData.instrumentName || '';
        this.mfForm.schemeCode = this.modalData.instrumentSymbol || '';
        this.mfQuery = this.modalData.instrumentName || this.modalData.instrumentSymbol || '';
        break;
      case 'GOLD':
        this.goldForm.subtype = this.resolveGoldSubtype(this.modalData.instrumentName);
        break;
      case 'FDRD':
        this.fdRdForm.subtype = this.resolveDepositSubtype(this.modalData.assetTypeName);
        break;
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
