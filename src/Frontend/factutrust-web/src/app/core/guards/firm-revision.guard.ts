import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { FirmFeatureFlagsService } from '@core/services/firm-feature-flags.service';

/**
 * Garde du réviseur de portefeuille. Fail-closed : sans le drapeau, la route redirige vers le
 * tableau de bord cabinet plutôt que d'ouvrir un écran dont l'API répondrait 503.
 *
 * Le drapeau front et `Features:AccountingFirms:FirmRevisionEnabled` côté API se basculent
 * ENSEMBLE : front actif avec back éteint donne un écran vide et incompréhensible.
 */
export const firmRevisionFeatureGuard: CanActivateFn = () => {
  const flags = inject(FirmFeatureFlagsService);
  if (flags.isEnabled('firmRevision')) {
    return true;
  }
  return inject(Router).createUrlTree(['/firm/dashboard']);
};
