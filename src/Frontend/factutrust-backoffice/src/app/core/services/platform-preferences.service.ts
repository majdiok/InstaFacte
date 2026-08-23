import { Injectable, signal } from '@angular/core';

export type TableDensity = 'normal' | 'compact';

export interface PlatformPreferences {
  tableDensity: TableDensity;
  confirmLogout: boolean;
}

const STORAGE_KEY = 'ft_platform_prefs';

const DEFAULT_PREFS: PlatformPreferences = {
  tableDensity: 'normal',
  confirmLogout: false
};

@Injectable({ providedIn: 'root' })
export class PlatformPreferencesService {
  private readonly prefsSignal = signal<PlatformPreferences>(this.read());

  readonly prefs = this.prefsSignal.asReadonly();

  tableDensity(): TableDensity {
    return this.prefsSignal().tableDensity;
  }

  confirmLogout(): boolean {
    return this.prefsSignal().confirmLogout;
  }

  update(partial: Partial<PlatformPreferences>): void {
    const next = { ...this.prefsSignal(), ...partial };
    this.prefsSignal.set(next);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
  }

  reset(): void {
    this.prefsSignal.set({ ...DEFAULT_PREFS });
    localStorage.setItem(STORAGE_KEY, JSON.stringify(DEFAULT_PREFS));
  }

  private read(): PlatformPreferences {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return { ...DEFAULT_PREFS };
    try {
      const parsed = JSON.parse(raw) as Partial<PlatformPreferences>;
      return {
        tableDensity: parsed.tableDensity === 'compact' ? 'compact' : 'normal',
        confirmLogout: Boolean(parsed.confirmLogout)
      };
    } catch {
      return { ...DEFAULT_PREFS };
    }
  }
}
