import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';
import {
  CreateTransactionRequest,
  Instrument,
  Profile,
  Transaction,
  UpdateTransactionRequest
} from '../../../../core/models/portfolio.model';
import { InstrumentService } from '../../../../core/services/instrument.service';
import { ProfileService } from '../../../../core/services/profile.service';
import { TransactionService } from '../../../../core/services/transaction.service';

interface TxForm {
  instrumentId: string;
  type: string;
  quantity: number;
  price: number;
  amount: number;
  transactionDate: string;
  notes: string;
}

const TX_TYPES = ['BUY', 'SELL', 'SIP', 'DIVIDEND'];

@Component({
  selector: 'app-transactions',
  templateUrl: './transactions.component.html',
  styleUrls: ['../shared-page.scss']
})
export class TransactionsComponent implements OnInit, OnDestroy {
  profiles: Profile[] = [];
  instruments: Instrument[] = [];
  transactions: Transaction[] = [];
  readonly types = TX_TYPES;

  selectedProfileId: string | null = null;
  page = 1;
  pageSize = 25;

  loading = false;
  saving = false;
  error: string | null = null;

  showForm = false;
  editingId: string | null = null;
  form: TxForm = this.emptyForm();

  private destroy$ = new Subject<void>();

  constructor(
    private profileService: ProfileService,
    private instrumentService: InstrumentService,
    private transactionService: TransactionService,
    private toastr: ToastrService
  ) {}

  ngOnInit(): void {
    this.profileService.list().pipe(takeUntil(this.destroy$)).subscribe({
      next: (data) => {
        this.profiles = data ?? [];
        if (this.profiles.length && !this.selectedProfileId) {
          this.selectedProfileId = this.profiles[0].id;
          this.fetch();
        }
      },
      error: (err) => (this.error = err?.error?.message || 'Failed to load profiles.')
    });

    this.instrumentService.listInstruments().pipe(takeUntil(this.destroy$)).subscribe({
      next: (data) => (this.instruments = data ?? [])
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private emptyForm(): TxForm {
    const today = new Date().toISOString().slice(0, 10);
    return { instrumentId: '', type: 'BUY', quantity: 0, price: 0, amount: 0, transactionDate: today, notes: '' };
  }

  onProfileChange(): void {
    this.page = 1;
    this.showForm = false;
    this.editingId = null;
    this.fetch();
  }

  fetch(): void {
    if (!this.selectedProfileId) return;
    this.loading = true;
    this.error = null;
    this.transactionService.list(this.selectedProfileId, this.page, this.pageSize)
      .pipe(takeUntil(this.destroy$), finalize(() => (this.loading = false)))
      .subscribe({
        next: (data) => (this.transactions = data ?? []),
        error: (err) => (this.error = err?.error?.message || 'Failed to load transactions.')
      });
  }

  openCreate(): void {
    this.editingId = null;
    this.form = { ...this.emptyForm(), instrumentId: this.instruments[0]?.id || '' };
    this.showForm = true;
  }

  openEdit(tx: Transaction): void {
    this.editingId = tx.id;
    this.form = {
      instrumentId: tx.instrumentId,
      type: tx.type,
      quantity: tx.quantity,
      price: tx.price,
      amount: tx.amount,
      transactionDate: (tx.transactionDate || '').slice(0, 10),
      notes: tx.notes || ''
    };
    this.showForm = true;
  }

  cancelForm(): void {
    this.showForm = false;
    this.editingId = null;
  }

  onQuantityOrPriceChange(): void {
    const q = Number(this.form.quantity) || 0;
    const p = Number(this.form.price) || 0;
    if (q && p) this.form.amount = Math.round(q * p * 100) / 100;
  }

  submit(): void {
    if (!this.selectedProfileId) return;
    if (!this.form.instrumentId || !this.form.type) {
      this.toastr.error('Instrument and type are required');
      return;
    }
    this.saving = true;
    const payload = {
      quantity: Number(this.form.quantity),
      price: Number(this.form.price),
      amount: Number(this.form.amount),
      transactionDate: new Date(this.form.transactionDate).toISOString(),
      notes: this.form.notes || ''
    };

    const req$ = this.editingId
      ? this.transactionService.update(this.selectedProfileId, this.editingId, payload as UpdateTransactionRequest)
      : this.transactionService.create(this.selectedProfileId, {
          ...payload,
          instrumentId: this.form.instrumentId,
          type: this.form.type
        } as CreateTransactionRequest);

    req$.pipe(takeUntil(this.destroy$), finalize(() => (this.saving = false)))
      .subscribe({
        next: () => {
          this.toastr.success(this.editingId ? 'Transaction updated' : 'Transaction recorded');
          this.cancelForm();
          this.fetch();
        },
        error: (err) => this.toastr.error(err?.error?.message || 'Save failed')
      });
  }

  delete(tx: Transaction): void {
    if (!this.selectedProfileId) return;
    if (!confirm(`Delete ${tx.type} transaction for ${tx.instrumentSymbol}?`)) return;
    this.transactionService.delete(this.selectedProfileId, tx.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.toastr.success('Transaction deleted'); this.fetch(); },
        error: (err) => this.toastr.error(err?.error?.message || 'Delete failed')
      });
  }

  nextPage(): void {
    if (this.transactions.length < this.pageSize) return;
    this.page++;
    this.fetch();
  }

  prevPage(): void {
    if (this.page <= 1) return;
    this.page--;
    this.fetch();
  }

  typeClass(type: string): string {
    switch (type) {
      case 'BUY': return 'pill-success';
      case 'SELL': return 'pill-danger';
      case 'SIP': return 'pill-primary';
      case 'DIVIDEND': return 'pill-info';
      default: return 'pill-muted';
    }
  }

  formatNumber(n: number, digits = 2): string {
    return Number(n || 0).toLocaleString('en-IN', { maximumFractionDigits: digits });
  }

  formatCurrency(n: number): string {
    return '₹' + this.formatNumber(n);
  }

  formatDate(s: string): string {
    return new Date(s).toLocaleDateString('en-IN', { year: 'numeric', month: 'short', day: 'numeric' });
  }
}
