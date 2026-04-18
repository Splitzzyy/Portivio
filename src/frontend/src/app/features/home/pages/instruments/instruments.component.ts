import { Component, OnDestroy, OnInit } from '@angular/core';
import { forkJoin, Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';
import {
  AssetType,
  CreateInstrumentRequest,
  Instrument,
  UpdateInstrumentRequest
} from '../../../../core/models/portfolio.model';
import { InstrumentService } from '../../../../core/services/instrument.service';

interface InstrumentForm {
  assetTypeId: string;
  name: string;
  symbol: string;
  currency: string;
}

@Component({
  selector: 'app-instruments',
  templateUrl: './instruments.component.html',
  styleUrls: ['../shared-page.scss']
})
export class InstrumentsComponent implements OnInit, OnDestroy {
  instruments: Instrument[] = [];
  assetTypes: AssetType[] = [];
  filterAssetTypeId: string | '' = '';

  loading = true;
  saving = false;
  savingAssetType = false;
  error: string | null = null;

  showForm = false;
  editingId: string | null = null;
  form: InstrumentForm = { assetTypeId: '', name: '', symbol: '', currency: 'INR' };

  newAssetTypeName = '';

  private destroy$ = new Subject<void>();

  constructor(
    private instrumentService: InstrumentService,
    private toastr: ToastrService
  ) {}

  ngOnInit(): void { this.fetchAll(); }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  fetchAll(): void {
    this.loading = true;
    this.error = null;
    forkJoin({
      instruments: this.instrumentService.listInstruments(this.filterAssetTypeId || undefined),
      assetTypes: this.instrumentService.listAssetTypes()
    })
      .pipe(takeUntil(this.destroy$), finalize(() => (this.loading = false)))
      .subscribe({
        next: ({ instruments, assetTypes }) => {
          this.instruments = instruments ?? [];
          this.assetTypes = assetTypes ?? [];
        },
        error: (err) => (this.error = err?.error?.message || 'Failed to load data.')
      });
  }

  onFilterChange(): void {
    this.loading = true;
    this.instrumentService.listInstruments(this.filterAssetTypeId || undefined)
      .pipe(takeUntil(this.destroy$), finalize(() => (this.loading = false)))
      .subscribe({
        next: (data) => (this.instruments = data ?? []),
        error: (err) => (this.error = err?.error?.message || 'Failed to load instruments.')
      });
  }

  openCreate(): void {
    this.editingId = null;
    this.form = {
      assetTypeId: this.assetTypes[0]?.id || '',
      name: '',
      symbol: '',
      currency: 'INR'
    };
    this.showForm = true;
  }

  openEdit(i: Instrument): void {
    this.editingId = i.id;
    this.form = {
      assetTypeId: i.assetTypeId,
      name: i.name,
      symbol: i.symbol,
      currency: i.currency
    };
    this.showForm = true;
  }

  cancelForm(): void {
    this.showForm = false;
    this.editingId = null;
  }

  submit(): void {
    if (!this.form.name.trim() || !this.form.symbol.trim() || !this.form.currency.trim()) {
      this.toastr.error('Name, symbol, and currency are required');
      return;
    }
    this.saving = true;

    const req$ = this.editingId
      ? this.instrumentService.updateInstrument(this.editingId, {
          name: this.form.name,
          symbol: this.form.symbol,
          currency: this.form.currency
        } as UpdateInstrumentRequest)
      : this.instrumentService.createInstrument({
          assetTypeId: this.form.assetTypeId,
          name: this.form.name,
          symbol: this.form.symbol,
          currency: this.form.currency
        } as CreateInstrumentRequest);

    req$.pipe(takeUntil(this.destroy$), finalize(() => (this.saving = false)))
      .subscribe({
        next: () => {
          this.toastr.success(this.editingId ? 'Instrument updated' : 'Instrument created');
          this.cancelForm();
          this.fetchAll();
        },
        error: (err) => this.toastr.error(err?.error?.message || 'Save failed')
      });
  }

  delete(i: Instrument): void {
    if (!confirm(`Delete instrument "${i.symbol}"? This fails if any holding/transaction references it.`)) return;
    this.instrumentService.deleteInstrument(i.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.toastr.success('Instrument deleted'); this.fetchAll(); },
        error: (err) => this.toastr.error(err?.error?.message || 'Delete failed')
      });
  }

  addAssetType(): void {
    const name = (this.newAssetTypeName || '').trim();
    if (!name) {
      this.toastr.error('Asset type name is required');
      return;
    }
    this.savingAssetType = true;
    this.instrumentService.createAssetType({ name })
      .pipe(takeUntil(this.destroy$), finalize(() => (this.savingAssetType = false)))
      .subscribe({
        next: () => {
          this.toastr.success('Asset type created');
          this.newAssetTypeName = '';
          this.fetchAll();
        },
        error: (err) => this.toastr.error(err?.error?.message || 'Create failed')
      });
  }

  deleteAssetType(at: AssetType): void {
    if (!confirm(`Delete asset type "${at.name}"? Only works if no instruments use it.`)) return;
    this.instrumentService.deleteAssetType(at.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.toastr.success('Asset type deleted'); this.fetchAll(); },
        error: (err) => this.toastr.error(err?.error?.message || 'Delete failed')
      });
  }
}
