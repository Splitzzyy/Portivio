import { Component, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';
import { User } from '../../../../core/models/auth.model';
import { AuthService } from '../../../../core/services/auth.service';
import { HomeService } from '../../../../core/services/home.service';
import { environment } from '../../../../../environments/environment';
import {
  HomeProfile,
  HomeResponse,
  HomeTransaction,
  PortfolioSummary
} from '../../../../core/models/portfolio.model';
import { ModalService } from '../../../../core/services/modal.service';

interface AllocationRow {
  name: string;
  value: number;
  percentage: number;
}

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit, OnDestroy {
  environment = environment;
  user: User | null = null;
  home: HomeResponse | null = null;
  summary: PortfolioSummary | null = null;
  profiles: HomeProfile[] = [];
  allocation: AllocationRow[] = [];
  recentTransactions: (HomeTransaction & { profileName: string })[] = [];

  loading = true;
  error: string | null = null;

  private destroy$ = new Subject<void>();
  private readonly allocationColors = ['#6366f1', '#0ea5e9', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6'];

  constructor(
    private authService: AuthService,
    private homeService: HomeService,
    private modalService: ModalService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.user = this.authService.getCurrentUser();
    this.fetchHome();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  fetchHome(): void {
    this.loading = true;
    this.error = null;
    this.homeService.getHome()
      .pipe(takeUntil(this.destroy$), finalize(() => (this.loading = false)))
      .subscribe({
        next: (data) => {
          this.home = data;
          this.summary = data.summary;
          this.profiles = data.profiles ?? [];
          this.allocation = this.buildAllocation(this.profiles);
          this.recentTransactions = this.buildRecentTransactions(this.profiles);
        },
        error: (err) => {
          this.error = err?.error?.message || 'Failed to load portfolio data.';
        }
      });
  }

  private buildAllocation(profiles: HomeProfile[]): AllocationRow[] {
    const totals = new Map<string, number>();
    let grand = 0;
    for (const p of profiles) {
      for (const h of p.holdings) {
        const v = Number(h.marketValue) || 0;
        totals.set(h.assetType, (totals.get(h.assetType) || 0) + v);
        grand += v;
      }
    }
    if (grand <= 0) return [];
    return Array.from(totals.entries())
      .map(([name, value]) => ({ name, value, percentage: Math.round((value / grand) * 100) }))
      .sort((a, b) => b.value - a.value);
  }

  private buildRecentTransactions(profiles: HomeProfile[]): (HomeTransaction & { profileName: string })[] {
    const all: (HomeTransaction & { profileName: string })[] = [];
    for (const p of profiles) {
      for (const tx of p.transactions) {
        all.push({ ...tx, profileName: p.name });
      }
    }
    all.sort((a, b) => new Date(b.transactionDate).getTime() - new Date(a.transactionDate).getTime());
    return all.slice(0, 5);
  }

  allocationColor(index: number): string {
    return this.allocationColors[index % this.allocationColors.length];
  }

  getFirstName(): string {
    const first = (this.user?.name || this.home?.user?.name || '').trim().split(/\s+/)[0];
    return first || 'there';
  }

  getTransactionTypeColor(type: string): string {
    switch (type) {
      case 'BUY': return 'success';
      case 'SELL': return 'danger';
      case 'SIP': return 'primary';
      case 'DIVIDEND': return 'info';
      default: return 'secondary';
    }
  }

  getTransactionTypeIcon(type: string): string {
    switch (type) {
      case 'BUY': return 'fa-arrow-down';
      case 'SELL': return 'fa-arrow-up';
      case 'SIP': return 'fa-repeat';
      case 'DIVIDEND': return 'fa-coins';
      default: return 'fa-exchange-alt';
    }
  }

  formatCurrency(value: number | null | undefined): string {
    const n = Number(value) || 0;
    return '₹' + n.toLocaleString('en-IN', { maximumFractionDigits: 2 });
  }

  formatDate(date: string | Date): string {
    return new Date(date).toLocaleDateString('en-IN', {
      year: 'numeric', month: 'short', day: 'numeric'
    });
  }

  openAddInvestmentModal(source?: string): void {
    const data = { source: source || 'dashboard' };
    this.modalService.open(data);
    this.router.navigate(['/dashboard/add-investment'], { state: { data } });
  }

  get totalInvestment(): number { return this.summary?.totalInvestment ?? 0; }
  get totalMarketValue(): number { return this.summary?.totalMarketValue ?? 0; }
  get totalReturns(): number { return this.summary?.totalUnrealizedPnL ?? 0; }
  get returnPercentage(): number {
    const inv = this.totalInvestment;
    if (!inv) return 0;
    return Math.round((this.totalReturns / inv) * 10000) / 100;
  }
}
