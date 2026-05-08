import { TestBed } from '@angular/core/testing';
import { ThemeService, ThemeMode } from './theme.service';

describe('ThemeService', () => {
  const STORAGE_KEY = 'portivio_theme_v1';
  let mediaListeners: Array<(e: MediaQueryListEvent) => void>;
  let prefersDark: boolean;

  beforeEach(() => {
    localStorage.removeItem(STORAGE_KEY);
    document.documentElement.removeAttribute('data-theme');
    mediaListeners = [];
    prefersDark = false;

    spyOn(window, 'matchMedia').and.callFake((query: string) => ({
      matches: query === '(prefers-color-scheme: dark)' ? prefersDark : false,
      media: query,
      onchange: null,
      addListener: () => undefined,
      removeListener: () => undefined,
      addEventListener: (_: string, listener: EventListenerOrEventListenerObject) =>
        mediaListeners.push(listener as (e: MediaQueryListEvent) => void),
      removeEventListener: () => undefined,
      dispatchEvent: () => true
    } as unknown as MediaQueryList));
  });

  function build(): ThemeService {
    TestBed.configureTestingModule({});
    return TestBed.inject(ThemeService);
  }

  it('defaults to system when no localStorage entry', () => {
    const svc = build();
    expect(svc.mode()).toBe('system');
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
  });

  it('cycle goes system -> light -> dark -> system', () => {
    const svc = build();
    expect(svc.mode()).toBe('system');
    svc.cycle(); expect(svc.mode()).toBe('light');
    svc.cycle(); expect(svc.mode()).toBe('dark');
    svc.cycle(); expect(svc.mode()).toBe('system');
  });

  it('persists choice to localStorage on every cycle', () => {
    const svc = build();
    svc.cycle();
    expect(localStorage.getItem(STORAGE_KEY)).toBe('light');
    svc.cycle();
    expect(localStorage.getItem(STORAGE_KEY)).toBe('dark');
  });

  it('honors prefers-color-scheme when in system mode', () => {
    prefersDark = true;
    const svc = build();
    expect(svc.mode()).toBe('system');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
  });

  it('responds to OS theme change while in system mode', () => {
    const svc = build();
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
    mediaListeners.forEach(l => l({ matches: true } as MediaQueryListEvent));
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
  });

  it('ignores OS theme change after manual override', () => {
    const svc = build();
    svc.set('light');
    mediaListeners.forEach(l => l({ matches: true } as MediaQueryListEvent));
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
  });

  it('reads stored mode on bootstrap', () => {
    localStorage.setItem(STORAGE_KEY, 'dark');
    const svc = build();
    expect(svc.mode()).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
  });
});
