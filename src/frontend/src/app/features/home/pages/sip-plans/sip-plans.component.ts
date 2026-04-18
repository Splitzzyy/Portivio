import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';
import {
  CreateSIPPlanRequest,
  Instrument,
  Profile,
  SIPPlan,
  UpdateSIPPlanRequest
} from '../../../../core/models/portfolio.model';
import { InstrumentService } from '../../../../core/services/instrument.service';
import { ProfileService } from '../../../../core/services/profile.service';
import { SipPlanService } from '../../../../core/services/sip-plan.service';

interface SipForm {
  instrumentId: string;
  amount: number;
  sipDay: number;
  startDate: string;
  endDate: string;
}

@Component({
  selector: 'app-sip-plans',
  templateUrl: './sip-plans.component.html',
  styleUrls: ['../shared-page.scss']
})
export class SipPlansComponent implements OnInit, OnDestroy {
  profiles: Profile[] = [];
  instruments: Instrument[] = [];
  plans: SIPPlan[] = [];

  selectedProfileId: string | null = null;
  activeOnly = false;

  loading = false;
  saving = false;
  error: string | null = null;

  showForm = false;
  editingId: string | null = null;
  form: SipForm = this.emptyForm();

  private destroy$ = new Subject<void>();

  constructor(
    private profileService: ProfileService,
    private instrumentService: InstrumentService,
    private sipService: SipPlanService,
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

  private emptyForm(): SipForm {
    const today = new Date();
    const end = new Date(today);
    end.setFullYear(end.getFullYear() + 5);
    return {
      instrumentId: '',
      amount: 0,
      sipDay: 1,
      startDate: today.toISOString().slice(0, 10),
      endDate: end.toISOString().slice(0, 10)
    };
  }

  onProfileChange(): void {
    this.showForm = false;
    this.editingId = null;
    this.fetch();
  }

  fetch(): void {
    if (!this.selectedProfileId) return;
    this.loading = true;
    this.error = null;
    this.sipService.list(this.selectedProfileId, this.activeOnly || undefined)
      .pipe(takeUntil(this.destroy$), finalize(() => (this.loading = false)))
      .subscribe({
        next: (data) => (this.plans = data ?? []),
        error: (err) => (this.error = err?.error?.message || 'Failed to load SIP plans.')
      });
  }

  openCreate(): void {
    this.editingId = null;
    this.form = { ...this.emptyForm(), instrumentId: this.instruments[0]?.id || '' };
    this.showForm = true;
  }

  openEdit(sip: SIPPlan): void {
    this.editingId = sip.id;
    this.form = {
      instrumentId: sip.instrumentId,
      amount: sip.amount,
      sipDay: sip.sipDay,
      startDate: (sip.startDate || '').slice(0, 10),
      endDate: (sip.endDate || '').slice(0, 10)
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
    if (this.form.sipDay < 1 || this.form.sipDay > 28) {
      this.toastr.error('SIP day must be between 1 and 28');
      return;
    }
    this.saving = true;
    const basePayload = {
      amount: Number(this.form.amount),
      sipDay: Number(this.form.sipDay),
      startDate: new Date(this.form.startDate).toISOString(),
      endDate: new Date(this.form.endDate).toISOString()
    };

    const req$ = this.editingId
      ? this.sipService.update(this.selectedProfileId, this.editingId, basePayload as UpdateSIPPlanRequest)
      : this.sipService.create(this.selectedProfileId, {
          ...basePayload,
          instrumentId: this.form.instrumentId
        } as CreateSIPPlanRequest);

    req$.pipe(takeUntil(this.destroy$), finalize(() => (this.saving = false)))
      .subscribe({
        next: () => {
          this.toastr.success(this.editingId ? 'SIP updated' : 'SIP created');
          this.cancelForm();
          this.fetch();
        },
        error: (err) => this.toastr.error(err?.error?.message || 'Save failed')
      });
  }

  toggleActive(sip: SIPPlan): void {
    if (!this.selectedProfileId) return;
    const op$ = sip.isActive
      ? this.sipService.deactivate(this.selectedProfileId, sip.id)
      : this.sipService.activate(this.selectedProfileId, sip.id);
    op$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toastr.success(sip.isActive ? 'SIP deactivated' : 'SIP activated');
        this.fetch();
      },
      error: (err) => this.toastr.error(err?.error?.message || 'Action failed')
    });
  }

  delete(sip: SIPPlan): void {
    if (!this.selectedProfileId) return;
    if (!confirm(`Delete SIP plan for ${sip.instrumentSymbol}?`)) return;
    this.sipService.delete(this.selectedProfileId, sip.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.toastr.success('SIP deleted'); this.fetch(); },
        error: (err) => this.toastr.error(err?.error?.message || 'Delete failed')
      });
  }

  formatCurrency(n: number): string {
    return '₹' + Number(n || 0).toLocaleString('en-IN', { maximumFractionDigits: 2 });
  }

  formatDate(s: string): string {
    return new Date(s).toLocaleDateString('en-IN', { year: 'numeric', month: 'short', day: 'numeric' });
  }
}
