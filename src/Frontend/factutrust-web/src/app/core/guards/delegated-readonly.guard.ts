import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';
import { isDelegatedReadOnlyRoute } from '@core/config/firm-navigation.registry';

/** Route suffixes that create or mutate data — blocked in delegated firm mode. */
const DELEGATED_WRITE_SUFFIXES = ['/new', '/edit'];

function pathWithoutQuery(url: string): string {
  const q = url.indexOf('?');
  return q >= 0 ? url.slice(0, q) : url;
}

/**
 * Blocks write routes for accounting firm users operating in delegated read-only context.
 */
export const delegatedReadonlyGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isAccountingFirm() || !auth.isDelegatedMode()) {
    return true;
  }

  const path = pathWithoutQuery(state.url);

  if (!isDelegatedReadOnlyRoute(path)) {
    return true;
  }

  const isWrite =
    DELEGATED_WRITE_SUFFIXES.some(s => path.endsWith(s) || path.includes(s + '/')) ||
    path.includes('/credit-note');

  if (!isWrite) {
    return true;
  }

  return router.createUrlTree(['/access-denied'], {
    queryParams: { returnUrl: state.url, reason: 'delegated-readonly' }
  });
};
