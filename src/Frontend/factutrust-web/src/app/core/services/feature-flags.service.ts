import { Injectable, computed, signal } from '@angular/core';
import { environment } from '@environments/environment';

export type FeatureFlagKey = keyof typeof environment.featureFlags;

const STORAGE_KEY = 'factutrust:feature-flags-overrides';

interface StoredOverrides {
  [key: string]: boolean;
}

@Injectable({ providedIn: 'root' })
export class FeatureFlagsService {
  private readonly overrides = signal<StoredOverrides>(this.loadOverrides());

  readonly wizardSimplifiedFlow = computed(() => this.read('wizardSimplifiedFlow'));
  readonly pdfPreview = computed(() => this.read('pdfPreview'));

  isEnabled(key: FeatureFlagKey): boolean {
    return this.read(key);
  }

  setOverride(key: FeatureFlagKey, value: boolean | null): void {
    const next = { ...this.overrides() };
    if (value === null) {
      delete next[key];
    } else {
      next[key] = value;
    }
    this.overrides.set(next);
    this.persistOverrides(next);
  }

  clearOverrides(): void {
    this.overrides.set({});
    this.persistOverrides({});
  }

  private read(key: FeatureFlagKey): boolean {
    const override = this.overrides()[key];
    if (typeof override === 'boolean') {
      return override;
    }
    return environment.featureFlags[key] === true;
  }

  private loadOverrides(): StoredOverrides {
    try {
      const raw = typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_KEY) : null;
      if (!raw) return {};
      const parsed = JSON.parse(raw);
      return typeof parsed === 'object' && parsed !== null ? parsed as StoredOverrides : {};
    } catch {
      return {};
    }
  }

  private persistOverrides(overrides: StoredOverrides): void {
    try {
      if (typeof localStorage === 'undefined') return;
      if (Object.keys(overrides).length === 0) {
        localStorage.removeItem(STORAGE_KEY);
      } else {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(overrides));
      }
    } catch {
      /* no-op : private mode or quota exceeded */
    }
  }
}
