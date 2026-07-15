import { Injectable, inject } from '@angular/core';
import { AccountingFeatureFlagsService } from './accounting-feature-flags.service';

/**
 * Journalisation légère des erreurs métier comptable (opt-in via feature flag localStorage).
 */
@Injectable({ providedIn: 'root' })
export class AccountingMonitoringService {
  private readonly flags = inject(AccountingFeatureFlagsService);

  logError(context: string, err: unknown): void {
    if (!this.flags.isEnabled('consoleAccountingErrors')) return;
    if (typeof console !== 'undefined' && typeof console.warn === 'function') {
      console.warn(`[Accounting:${context}]`, err);
    }
  }
}
