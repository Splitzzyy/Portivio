import { Component, OnInit, OnDestroy, HostListener, ElementRef } from '@angular/core';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { AuthService } from '../../../../core/services/auth.service';
import { ThemeService } from '../../../../core/services/theme.service';
import { ModalService } from '../../../../core/services/modal.service';
import { User } from '../../../../core/models/auth.model';
import { environment } from '../../../../../environments/environment';

/**
 * Authenticated shell: sidebar + header + <router-outlet/>.
 */
@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss']
})
export class HomeComponent implements OnInit, OnDestroy {
  environment = environment;
  sidebarOpen = true;
  user: User | null = null;
  dropdownOpen = false;
  showMobileMenu = false;

  private destroy$ = new Subject<void>();

  constructor(
    private authService: AuthService,
    private router: Router,
    private elementRef: ElementRef,
    public themeService: ThemeService,
    private modalService: ModalService
  ) {}

  get isDarkMode(): boolean {
    return this.themeService.mode() === 'dark';
  }

  toggleTheme(): void {
    const next = this.isDarkMode ? 'light' : 'dark';
    this.themeService.set(next);
  }

  cycleTheme(): void {
    this.themeService.cycle();
  }

  themeIcon(): string {
    switch (this.themeService.mode()) {
      case 'light': return 'fa-sun';
      case 'dark':  return 'fa-moon';
      default:      return 'fa-desktop';
    }
  }

  themeLabel(): string {
    switch (this.themeService.mode()) {
      case 'light': return 'Light theme';
      case 'dark':  return 'Dark theme';
      default:      return 'System theme';
    }
  }

  ngOnInit(): void {
    this.authService.user$
      .pipe(takeUntil(this.destroy$))
      .subscribe(user => (this.user = user));
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /** Close the user dropdown when clicking anywhere outside it. */
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.dropdownOpen) return;
    const target = event.target as HTMLElement;
    if (!this.elementRef.nativeElement.querySelector('.user-dropdown')?.contains(target)) {
      this.dropdownOpen = false;
    }
  }

  /** Close the dropdown / mobile drawer on Escape for keyboard users. */
  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.dropdownOpen) this.dropdownOpen = false;
    if (this.showMobileMenu) this.showMobileMenu = false;
  }

  toggleSidebar(): void {
    this.sidebarOpen = !this.sidebarOpen;
  }

  toggleUserDropdown(event?: Event): void {
    event?.stopPropagation();
    this.dropdownOpen = !this.dropdownOpen;
  }

  toggleMobileMenu(): void {
    this.showMobileMenu = !this.showMobileMenu;
  }

  goToProfile(): void {
    this.dropdownOpen = false;
    this.router.navigate(['/dashboard/my-profile']);
  }

  goToSettings(): void {
    this.dropdownOpen = false;
    // this.router.navigate(['/settings']);
  }

  logout(): void {
    this.dropdownOpen = false;
    this.authService.logout()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => this.router.navigate(['/']),
        error: () => this.router.navigate(['/'])
      });
  }

  openAddInvestmentModal(): void {
    this.router.navigate(['/dashboard/add-investment']);
  }

  /** Derive initials from the single `name` field. Falls back to email. */
  getUserInitials(): string {
    if (!this.user) return '';
    const source = this.user.name?.trim() || this.user.email || '';
    const parts = source.split(/\s+/).filter(Boolean);
    if (parts.length === 0) return '';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }
}
