import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';
import {
  Holding,
  Instrument,
  Profile
} from '../../../../core/models/portfolio.model';
import { HoldingService } from '../../../../core/services/holding.service';
import { InstrumentService } from '../../../../core/services/instrument.service';
import { ProfileService } from '../../../../core/services/profile.service';
import { ModalService } from '../../../../core/services/modal.service';

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
  refreshing = false;
  error: string | null = null;

  private destroy$ = new Subject<void>();

  constructor(
    private profileService: ProfileService,
    private instrumentService: InstrumentService,
    private holdingService: HoldingService,
    private toastr: ToastrService,
    private modalService: ModalService
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

  get lastRefreshedAt(): Date | null {
    if (!this.holdings.length) return null;
    const latest = this.holdings
      .map(h => new Date(h.lastUpdated).getTime())
      .filter(t => isFinite(t))
      .reduce((a, b) => Math.max(a, b), 0);
    return latest ? new Date(latest) : null;
  }

  onProfileChange(): void {
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

  trackHoldingById(_: number, h: Holding): string { return h.id; }
  trackByString(_: number, v: string): string { return v; }

  returnPct(h: Holding): number {
    const cost = h.avgPrice * h.quantity;
    if (!cost) return 0;
    return (h.unrealizedPnL / cost) * 100;
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

  assetPillClass(assetTypeName: string | undefined | null): string {
    const name = (assetTypeName || '').toLowerCase();
    if (name.includes('equity') || name.includes('stock')) return 'pill-info';
    if (name.includes('mutual') || name.includes('fund'))  return 'pill-primary';
    if (name.includes('gold'))                              return 'pill-warning';
    if (name.includes('recurring') || name.includes('rd'))  return 'pill-indigo';
    if (name.includes('fixed') || name.includes('fd'))      return 'pill-success';
    if (name.includes('ppf'))                               return 'pill-orange';
    return 'pill-muted';
  }

  refreshPrices(): void {
    if (!this.selectedProfileId || this.refreshing) return;
    this.refreshing = true;
    this.holdingService.refresh(this.selectedProfileId)
      .pipe(takeUntil(this.destroy$), finalize(() => (this.refreshing = false)))
      .subscribe({
        next: (data) => {
          this.holdings = data ?? [];
          this.toastr.success('Holdings refreshed');
        },
        error: (err) => this.toastr.error(err?.error?.message || 'Refresh failed')
      });
  }

  formatNumber(n: number, digits = 2): string {
    return Number(n || 0).toLocaleString('en-IN', { maximumFractionDigits: digits });
  }

  formatCurrency(n: number): string {
    return '₹' + this.formatNumber(n);
  }

  formatRelative(value: string | Date | null | undefined): string {
    if (!value) return '—';
    const then = value instanceof Date ? value.getTime() : new Date(value).getTime();
    if (!isFinite(then)) return '—';
    const diffSec = Math.max(0, Math.floor((Date.now() - then) / 1000));
    if (diffSec < 60) return 'just now';
    const diffMin = Math.floor(diffSec / 60);
    if (diffMin < 60) return `${diffMin}m ago`;
    const diffHr = Math.floor(diffMin / 60);
    if (diffHr < 24) return `${diffHr}h ago`;
    const diffDay = Math.floor(diffHr / 24);
    return diffDay === 1 ? '1 day ago' : `${diffDay} days ago`;
  }

  openAddInvestmentModal(source: string, holding?: Holding): void {
    this.modalService.open(holding ? {
      source,
      holdingId: holding.id,
      profileId: holding.profileId,
      instrumentId: holding.instrumentId,
      assetTypeName: holding.assetTypeName,
      instrumentName: holding.instrumentName,
      instrumentSymbol: holding.instrumentSymbol
    } : { source });
  }
}
