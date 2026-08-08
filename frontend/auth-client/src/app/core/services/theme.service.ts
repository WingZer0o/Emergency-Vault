import { Injectable, effect, signal } from '@angular/core';

export type ThemeMode = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'theme-mode';

/**
 * Controls which Material color scheme is active by setting `color-scheme` on the document
 * root. Angular Material's M3 theme tokens resolve via CSS `light-dark()`, so flipping this one
 * property is enough to swap every component's colors with no reload. Preference is persisted to
 * localStorage; `index.html` applies the stored value inline before Angular bootstraps to avoid a
 * flash of the wrong theme.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly modeSig = signal<ThemeMode>(readStoredMode());

  readonly mode = this.modeSig.asReadonly();

  constructor() {
    effect(() => {
      const mode = this.modeSig();
      localStorage.setItem(STORAGE_KEY, mode);
      document.documentElement.style.colorScheme = mode === 'system' ? 'light dark' : mode;
    });
  }

  setMode(mode: ThemeMode): void {
    this.modeSig.set(mode);
  }
}

function readStoredMode(): ThemeMode {
  const stored = localStorage.getItem(STORAGE_KEY);
  return stored === 'light' || stored === 'dark' || stored === 'system' ? stored : 'system';
}
