import { PERMISSIONS } from '@core/config/permission-keys';
import type { AuthService } from '@core/services/auth.service';

/** True when the user may validate journal entries and initial budgets (delegated firm only). */
export function canValidateAccountingEntries(auth: AuthService): boolean {
  return auth.isAccountingFirm()
    && auth.isDelegatedMode()
    && auth.hasPermission(PERMISSIONS.accounting.validate);
}
