import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';
import {
  MODULES_REQUIRED_BY_FIRST_SEGMENT,
  PERMISSIONS_ALL_REQUIRED_BY_FIRST_SEGMENT,
  POS_REQUIRED_MODULES,
  POS_REQUIRED_PERMISSIONS_ALL
} from '@core/config/layout-module-policy';

function pathWithoutQuery(url: string): string {
  const q = url.indexOf('?');
  return q >= 0 ? url.slice(0, q) : url;
}

/**
 * Ensures the user has all required {@link AppModule}s for the target URL.
 * Must run after {@link authGuard} and {@link warehouseSelectedGuard}.
 */
export const moduleGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const path = pathWithoutQuery(state.url);

  if (path === '/settings/profile' || path.startsWith('/settings/profile/')) {
    return true;
  }

  if (auth.isAccountingFirm() && !auth.isDelegatedMode()) {
    const segments = path.split('/').filter(Boolean);
    const first = segments[0];
    if (first && first !== 'firm' && first !== 'documentation' && first !== 'access-denied') {
      return router.createUrlTree(['/firm/dashboard']);
    }
  }

  const segments = path.split('/').filter(Boolean);
  const first = segments[0];
  if (!first) {
    return true;
  }

  const required = MODULES_REQUIRED_BY_FIRST_SEGMENT[first];
  if (!required) {
    return true;
  }

  for (const m of required) {
    if (!auth.hasModule(m)) {
      router.navigate(['/access-denied'], { queryParams: { returnUrl: state.url } });
      return false;
    }
  }

  const needPerms = PERMISSIONS_ALL_REQUIRED_BY_FIRST_SEGMENT[first];
  if (needPerms?.length && !auth.hasAllPermissions(needPerms)) {
    router.navigate(['/access-denied'], { queryParams: { returnUrl: state.url } });
    return false;
  }

  return true;
};

/** Used on the standalone <c>/pos</c> branch. */
export const posModuleGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  for (const m of POS_REQUIRED_MODULES) {
    if (!auth.hasModule(m)) {
      router.navigate(['/access-denied'], { queryParams: { returnUrl: state.url } });
      return false;
    }
  }
  if (!auth.hasAllPermissions(POS_REQUIRED_PERMISSIONS_ALL)) {
    router.navigate(['/access-denied'], { queryParams: { returnUrl: state.url } });
    return false;
  }
  return true;
};
