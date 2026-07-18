import { Router, UrlTree } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const DOCUMENTATION_ROUTE_PREFIX = '/documentation';

export function isDocumentationRoute(path: string): boolean {
  const normalized = path.split('?')[0].split('#')[0];
  return (
    normalized === DOCUMENTATION_ROUTE_PREFIX ||
    normalized.startsWith(`${DOCUMENTATION_ROUTE_PREFIX}/`)
  );
}

/** Documentation is disabled for company and accounting firm tenants. */
export function canAccessInAppDocumentation(_auth: AuthService): boolean {
  return false;
}

export function documentationRedirectUrlTree(auth: AuthService, router: Router): UrlTree {
  if (auth.isAccountingFirm() && !auth.isDelegatedMode()) {
    return router.createUrlTree(['/firm/dashboard']);
  }
  return router.createUrlTree(['/dashboard']);
}
