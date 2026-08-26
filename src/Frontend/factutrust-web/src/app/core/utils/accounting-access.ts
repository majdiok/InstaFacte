import { PERMISSIONS } from '@core/config/permission-keys';
import type { AuthService } from '@core/services/auth.service';

/** True when the user may validate journal entries and initial budgets (delegated firm only). */
export function canValidateAccountingEntries(auth: AuthService): boolean {
  return auth.isAccountingFirm()
    && auth.isDelegatedMode()
    && auth.hasPermission(PERMISSIONS.accounting.validate);
}

/** True when the user may delete draft journal entries and their attachments (delegated firm only). */
export function canDeleteDraftAccountingEntries(auth: AuthService): boolean {
  return auth.isAccountingFirm()
    && auth.isDelegatedMode()
    && (auth.isFirmManager() || auth.isFirmAccountant())
    && auth.hasPermission(PERMISSIONS.accounting.delete);
}

/**
 * True when the user may edit a draft journal entry from the Journal (cabinet, delegated
 * dossier). Aligné sur le bouton Valider : réservé au cabinet en mode dossier client.
 * `isFirmDelegatedReadonly` n'est PAS utilisé ici : ce flag vaut true pour tout cabinet
 * délégué (il bloque la trésorerie, pas la comptabilité).
 */
export function canEditDraftAccountingEntries(auth: AuthService): boolean {
  return auth.isAccountingFirm()
    && auth.isDelegatedMode()
    && auth.hasPermission(PERMISSIONS.accounting.create);
}
