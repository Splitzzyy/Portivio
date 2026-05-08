import { Injectable, OnDestroy, RendererFactory2, signal } from '@angular/core';

export type ThemeMode = 'system' | 'light' | 'dark';
type ResolvedTheme = 'light' | 'dark';

const STORAGE_KEY = 'portivio_theme_v1';
const CYCLE_ORDER: ThemeMode[] = ['system', 'light', 'dark'];

@Injectable({ providedIn: 'root' })
export class ThemeService implements OnDestroy {
  private readonly _mode = signal<ThemeMode>(this.readStoredMode());
  readonly mode = this._mode.asReadonly();

  private readonly renderer = this.rendererFactory.createRenderer(null, null);
  private readonly mediaQuery: MediaQueryList | null;
  private readonly mediaListener: ((e: MediaQueryListEvent) => void) | null;

  constructor(private rendererFactory: RendererFactory2) {
    this.mediaQuery = typeof window !== 'undefined' && window.matchMedia
      ? window.matchMedia('(prefers-color-scheme: dark)')
      : null;
    this.mediaListener = (e: MediaQueryListEvent) => {
      if (this._mode() === 'system') this.applyResolved(e.matches ? 'dark' : 'light');
    };
    this.mediaQuery?.addEventListener?.('change', this.mediaListener);
    this.applyResolved(this.resolve(this._mode()));
  }

  cycle(): void {
    const idx = CYCLE_ORDER.indexOf(this._mode());
    const next = CYCLE_ORDER[(idx + 1) % CYCLE_ORDER.length];
    this.set(next);
  }

  set(mode: ThemeMode): void {
    this._mode.set(mode);
    this.persist(mode);
    this.applyResolved(this.resolve(mode));
  }

  ngOnDestroy(): void {
    if (this.mediaQuery && this.mediaListener) {
      this.mediaQuery.removeEventListener?.('change', this.mediaListener);
    }
  }

  private resolve(mode: ThemeMode): ResolvedTheme {
    if (mode === 'system') return this.mediaQuery?.matches ? 'dark' : 'light';
    return mode;
  }

  private applyResolved(resolved: ResolvedTheme): void {
    if (typeof document === 'undefined') return;
    this.renderer.setAttribute(document.documentElement, 'data-theme', resolved);
  }

  private readStoredMode(): ThemeMode {
    if (typeof localStorage === 'undefined') return 'system';
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw === 'light' || raw === 'dark' || raw === 'system' ? raw : 'system';
  }

  private persist(mode: ThemeMode): void {
    if (typeof localStorage === 'undefined') return;
    localStorage.setItem(STORAGE_KEY, mode);
  }
}
