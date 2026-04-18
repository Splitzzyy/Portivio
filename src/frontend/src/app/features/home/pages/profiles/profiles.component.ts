import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';
import {
  CreateProfileRequest,
  Profile,
  UpdateProfileRequest
} from '../../../../core/models/portfolio.model';
import { ProfileService } from '../../../../core/services/profile.service';

@Component({
  selector: 'app-profiles',
  templateUrl: './profiles.component.html',
  styleUrls: ['../shared-page.scss']
})
export class ProfilesComponent implements OnInit, OnDestroy {
  profiles: Profile[] = [];
  loading = true;
  saving = false;
  error: string | null = null;

  showForm = false;
  editingId: string | null = null;
  form: CreateProfileRequest = { name: '', baseCurrency: 'INR', description: '' };

  private destroy$ = new Subject<void>();

  constructor(private profileService: ProfileService, private toastr: ToastrService) {}

  ngOnInit(): void { this.fetch(); }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  fetch(): void {
    this.loading = true;
    this.error = null;
    this.profileService.list()
      .pipe(takeUntil(this.destroy$), finalize(() => (this.loading = false)))
      .subscribe({
        next: (data) => (this.profiles = data ?? []),
        error: (err) => (this.error = err?.error?.message || 'Failed to load profiles.')
      });
  }

  openCreate(): void {
    this.editingId = null;
    this.form = { name: '', baseCurrency: 'INR', description: '' };
    this.showForm = true;
  }

  openEdit(p: Profile): void {
    this.editingId = p.id;
    this.form = { name: p.name, baseCurrency: p.baseCurrency, description: p.description };
    this.showForm = true;
  }

  cancelForm(): void {
    this.showForm = false;
    this.editingId = null;
  }

  submit(): void {
    if (!this.form.name.trim() || !this.form.baseCurrency.trim()) {
      this.toastr.error('Name and base currency are required');
      return;
    }
    this.saving = true;
    const req$ = this.editingId
      ? this.profileService.update(this.editingId, this.form as UpdateProfileRequest)
      : this.profileService.create(this.form);

    req$.pipe(takeUntil(this.destroy$), finalize(() => (this.saving = false)))
      .subscribe({
        next: () => {
          this.toastr.success(this.editingId ? 'Profile updated' : 'Profile created');
          this.cancelForm();
          this.fetch();
        },
        error: (err) => this.toastr.error(err?.error?.message || 'Save failed')
      });
  }

  delete(p: Profile): void {
    if (!confirm(`Delete profile "${p.name}"? This removes its holdings, transactions and SIPs.`)) return;
    this.profileService.delete(p.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toastr.success('Profile deleted');
          this.fetch();
        },
        error: (err) => this.toastr.error(err?.error?.message || 'Delete failed')
      });
  }

  formatDate(date: string): string {
    return new Date(date).toLocaleDateString('en-IN', { year: 'numeric', month: 'short', day: 'numeric' });
  }
}
