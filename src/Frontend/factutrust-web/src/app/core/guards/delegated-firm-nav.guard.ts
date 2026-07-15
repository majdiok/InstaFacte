import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';
import { isDelegatedFirmBlockedSalesPurchasesRoute } from '@core/config/firm-navigation.registry';

function pathWithoutQuery(url: string): string {
  const q = url.indexOf('?');
  return q >= 0 ? url.slice(0, q) : url;
}

/**
 * Blocks sales/purchases routes outside the delegated firm allowlist
 * (accounting firm operating inside a client dossier).
 */
export const delegatedFirmNavGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isAccountingFirm() || !auth.isDelegatedMode()) {
    return true;
  }

  const path = pathWithoutQuery(state.url);
  if (isDelegatedFirmBlockedSalesPurchasesRoute(path)) {
    return router.createUrlTree(['/access-denied'], {
      queryParams: { returnUrl: state.url, reason: 'delegated-nav-restricted' }
    });
  }

  return true;
};
