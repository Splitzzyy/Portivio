import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';
import {
  Holding,
  Instrument,
  Profile,
  UpsertHoldingRequest
} from '../../../../core/models/portfolio.model';
import { HoldingService } from '../../../../core/services/holding.service';
import { InstrumentService } from '../../../../core/services/instrument.service';
import { ProfileService } from '../../../../core/services/profile.service';

@Component({
  selector: 'app-holdings',
  templateUrl: './holdings.component.html',
  styleUrls: ['../shared-page.scss']
})
export class HoldingsComponent implements OnInit, OnDestroy {
  profiles: Profile[] = [];
  instruments: Instrument[] = [];
  holdings: Holding[] = [];

  selectedProfileId: string | null = null;
  activeAssetType = '';
  loading = false;
  saving = false;
  error: string | null = null;

  showForm = false;
  editingId: string | null = null;
  form: UpsertHoldingRequest = { instrumentId: '', quantity: 0, avgPrice: 0, currentPrice: 0 };

  private destroy$ = new Subject<void>();

  constructor(
    private profileService: ProfileService,
    private instrumentService: InstrumentService,
    private holdingService: HoldingService,
    private toastr: ToastrService
  ) {}

  ngOnInit(): void {
    this.profileService.list().pipe(takeUntil(this.destroy$)).subscribe({
      next: (data) => {
        this.profiles = data ?? [];
        if (this.profiles.length && !this.selectedProfileId) {
          this.selectedProfileId = this.profiles[0].id;
          this.fetchHoldings();
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

  get availableTypes(): string[] {
    const seen = new Set<string>();
    for (const h of this.holdings) {
      if (h.assetTypeName) seen.add(h.assetTypeName);
    }
    return Array.from(seen).sort();
  }

  get filteredHoldings(): Holding[] {
    if (!this.activeAssetType) return this.holdings;
    return this.holdings.filter(h => h.assetTypeName === this.activeAssetType);
  }

  onProfileChange(): void {
    this.showForm = false;
    this.editingId = null;
    this.activeAssetType = '';
    this.fetchHoldings();
  }

  fetchHoldings(): void {
    if (!this.selectedProfileId) return;
    this.loading = true;
    this.error = null;
    this.holdingService.list(this.selectedProfileId)
      .pipe(takeUntil(this.destroy$), finalize(() => (this.loading = false)))
      .subscribe({
        next: (data) => (this.holdings = data ?? []),
        error: (err) => (this.error = err?.error?.message || 'Failed to load holdings.')
      });
  }

  openCreate(): void {
    this.editingId = null;
    this.form = { instrumentId: this.instruments[0]?.id || '', quantity: 0, avgPrice: 0, currentPrice: 0 };
    this.showForm = true;
  }

  openEdit(h: Holding): void {
    this.editingId = h.id;
    this.form = {
      instrumentId: h.instrumentId,
      quantity: h.quantity,
      avgPrice: h.avgPrice,
      currentPrice: h.currentPrice
    };
    this.showForm = true;
  }

  cancelForm(): void {
    this.showForm = false;
    this.editingId = null;
  }

  submit(): void {
    if (!this.selectedProfileId) return;
    if (!this.form.instrumentId) {
      this.toastr.error('Select an instrument');
      return;
    }
    this.saving = true;
    this.holdingService.upsert(this.selectedProfileId, {
      instrumentId: this.form.instrumentId,
      quantity: Number(this.form.quantity),
      avgPrice: Number(this.form.avgPrice),
      currentPrice: Number(this.form.currentPrice)
    })
      .pipe(takeUntil(this.destroy$), finalize(() => (this.saving = false)))
      .subscribe({
        next: () => {
          this.toastr.success(this.editingId ? 'Holding updated' : 'Holding saved');
          this.cancelForm();
          this.fetchHoldings();
        },
        error: (err) => this.toastr.error(err?.error?.message || 'Save failed')
      });
  }

  delete(h: Holding): void {
    if (!this.selectedProfileId) return;
    if (!confirm(`Delete holding of ${h.instrumentSymbol}?`)) return;
    this.holdingService.delete(this.selectedProfileId, h.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.toastr.success('Holding deleted'); this.fetchHoldings(); },
        error: (err) => this.toastr.error(err?.error?.message || 'Delete failed')
      });
  }

  countByType(type: string): number {
    return this.holdings.filter(h => h.assetTypeName === type).length;
  }

  formatNumber(n: number, digits = 2): string {
    return Number(n || 0).toLocaleString('en-IN', { maximumFractionDigits: digits });
  }

  formatCurrency(n: number): string {
    return '₹' + this.formatNumber(n);
  }
}
