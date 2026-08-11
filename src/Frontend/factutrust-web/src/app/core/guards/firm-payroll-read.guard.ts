import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';

function pathWithoutQuery(url: string): string {
  const q = url.indexOf('?');
  return q >= 0 ? url.slice(0, q) : url;
}

/**
 * Accès en lecture à la paie interne du cabinet : responsable ou comptable.
 *
 * Le comptable cabinet dispose de `payroll:read` côté serveur, mais le garde responsable posé
 * sur la racine de `/firm/payroll` le renvoyait vers `/access-denied` : la permission existait
 * sans qu'aucune route ne puisse l'exercer. Les routes d'écriture restent réservées au
 * responsable, via `firmManagerGuard`.
 */
export const firmPayrollReadGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  if (auth.isFirmManager() || auth.isFirmAccountant()) {
    return true;
  }
  return inject(Router).createUrlTree(['/access-denied'], {
    queryParams: { returnUrl: pathWithoutQuery(state.url) }
  });
};
