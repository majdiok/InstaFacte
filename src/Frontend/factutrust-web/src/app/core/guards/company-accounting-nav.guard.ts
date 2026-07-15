import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';
import {
  isAccountingGuardedPath,
  isCompanyAccountingRestricted,
  isCompanyAllowedAccountingPath
} from '@core/config/company-accounting-nav.config';

function pathWithoutQuery(url: string): string {
  const q = url.indexOf('?');
  return q >= 0 ? url.slice(0, q) : url;
}

/**
 * Blocks accounting routes outside the company allowlist
 * (company tenant operating in native mode).
 */
export const companyAccountingNavGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!isCompanyAccountingRestricted(auth)) {
    return true;
  }

  const path = pathWithoutQuery(state.url);
  if (isAccountingGuardedPath(path) && !isCompanyAllowedAccountingPath(path)) {
    return router.createUrlTree(['/access-denied'], {
      queryParams: { returnUrl: state.url, reason: 'company-accounting-restricted' }
    });
  }

  return true;
};
