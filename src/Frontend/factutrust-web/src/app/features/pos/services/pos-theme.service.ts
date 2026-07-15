import { Injectable, signal, computed } from '@angular/core';

export type PosThemeId = 'default' | 'christmas' | 'ramadan' | 'sales';

const STORAGE_KEY = 'factutrust_pos_theme';

@Injectable({
  providedIn: 'root'
})
export class PosThemeService {
  private readonly themeId = signal<PosThemeId>(this.loadStored());

  readonly currentTheme = computed(() => this.themeId());
  readonly themeClass = computed(() => `pos-theme--${this.themeId()}`);

  setTheme(id: PosThemeId): void {
    this.themeId.set(id);
    try {
      localStorage.setItem(STORAGE_KEY, id);
    } catch {
      // ignore
    }
    this.applyToDocument(id);
  }

  private loadStored(): PosThemeId {
    try {
      const v = localStorage.getItem(STORAGE_KEY);
      if (v === 'christmas' || v === 'ramadan' || v === 'sales') return v;
    } catch {
      // ignore
    }
    return 'default';
  }

  private applyToDocument(id: PosThemeId): void {
    if (typeof document === 'undefined') return;
    const root = document.documentElement;
    root.classList.remove('pos-theme--default', 'pos-theme--christmas', 'pos-theme--ramadan', 'pos-theme--sales');
    root.classList.add(`pos-theme--${id}`);
  }

  init(): void {
    this.applyToDocument(this.themeId());
  }
}
