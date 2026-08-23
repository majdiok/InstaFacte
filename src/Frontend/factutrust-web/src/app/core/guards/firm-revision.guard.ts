import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PERMISSIONS } from '@core/config/permission-keys';
import { AuthService } from '@core/services/auth.service';
import { FirmFeatureFlagsService } from '@core/services/firm-feature-flags.service';

/**
 * Garde du réviseur de portefeuille. Fail-closed : sans le drapeau ou sans la permission, la
 * route redirige vers le tableau de bord cabinet plutôt que d'ouvrir un écran dont l'API
 * répondrait 503 ou 403.
 *
 * Le drapeau front et `Features:AccountingFirms:FirmRevisionEnabled` côté API se basculent
 * ENSEMBLE : front actif avec back éteint donne un écran vide et incompréhensible.
 *
 * La permission est vérifiée en plus du drapeau pour que l'entrée de menu et la route s'ouvrent
 * sur exactement la même condition : un utilisateur qui ne voit pas l'entrée ne peut pas non plus
 * atteindre la page en tapant l'URL. Le back ne délivre `firm:revision:view` qu'à la connexion —
 * un utilisateur connecté avant l'activation du module doit se reconnecter.
 */
export const firmRevisionFeatureGuard: CanActivateFn = () => {
  const flags = inject(FirmFeatureFlagsService);
  const auth = inject(AuthService);
  if (flags.isEnabled('firmRevision') && auth.hasPermission(PERMISSIONS.firmRevision.view)) {
    return true;
  }
  return inject(Router).createUrlTree(['/firm/dashboard']);
};
