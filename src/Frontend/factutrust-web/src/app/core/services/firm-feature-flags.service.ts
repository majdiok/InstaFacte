import { Injectable, signal } from '@angular/core';

const STORAGE_KEY = 'ft.firm.featureFlags';

export interface FirmFeatureFlags {
  /** KPI fiscaux V2 sur le tableau de bord cabinet */
  fiscalOpsV2: boolean;
  /** Module gouvernance (dossier permanent, dirigeants, pilotage) */
  firmGovernance: boolean;
  /** Journalisation console des erreurs cabinet (dev / support) */
  consoleFirmErrors: boolean;
}

const DEFAULT_FLAGS: FirmFeatureFlags = {
  fiscalOpsV2: true,
  firmGovernance: true,
  consoleFirmErrors: false
};

@Injectable({ providedIn: 'root' })
export class FirmFeatureFlagsService {
  private readonly _flags = signal<FirmFeatureFlags>(this.load());

  readonly flags = this._flags.asReadonly();

  isEnabled<K extends keyof FirmFeatureFlags>(key: K): boolean {
    return this._flags()[key];
  }

  setFlag<K extends keyof FirmFeatureFlags>(key: K, value: FirmFeatureFlags[K]): void {
    const next = { ...this._flags(), [key]: value };
    this._flags.set(next);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    } catch {
      /* ignore */
    }
  }

  reset(): void {
    this._flags.set({ ...DEFAULT_FLAGS });
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      /* ignore */
    }
  }

  private load(): FirmFeatureFlags {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return { ...DEFAULT_FLAGS };
      const parsed = JSON.parse(raw) as Partial<FirmFeatureFlags>;
      return { ...DEFAULT_FLAGS, ...parsed };
    } catch {
      return { ...DEFAULT_FLAGS };
    }
  }
}
