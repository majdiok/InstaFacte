import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';
import { isDelegatedReadOnlyRoute } from '@core/config/firm-navigation.registry';

/** Route suffixes that create or mutate data — blocked in delegated firm mode. */
const DELEGATED_WRITE_SUFFIXES = ['/new', '/edit'];

/** Matches `/credit-note` or `/credit-note/...` but not `/credit-notes` (list). */
const CREDIT_NOTE_WRITE_PATH = /\/credit-note(?:\/|$)/;

function pathWithoutQuery(url: string): string {
  const q = url.indexOf('?');
  return q >= 0 ? url.slice(0, q) : url;
}

/** True for create/edit paths that must stay blocked in delegated read-only mode. */
export function isDelegatedWritePath(path: string): boolean {
  return DELEGATED_WRITE_SUFFIXES.some(s => path.endsWith(s) || path.includes(s + '/'))
    || CREDIT_NOTE_WRITE_PATH.test(path);
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

  if (!isDelegatedWritePath(path)) {
    return true;
  }

  return router.createUrlTree(['/access-denied'], {
    queryParams: { returnUrl: state.url, reason: 'delegated-readonly' }
  });
};
