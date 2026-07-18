import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import {
  canAccessInAppDocumentation,
  documentationRedirectUrlTree,
  isDocumentationRoute
} from '@core/config/documentation-access.config';
import { AuthService } from '@core/services/auth.service';

function pathWithoutQuery(url: string): string {
  const q = url.indexOf('?');
  return q >= 0 ? url.slice(0, q) : url;
}

export const documentationAccessGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const path = pathWithoutQuery(state.url);

  if (!isDocumentationRoute(path)) {
    return true;
  }

  if (canAccessInAppDocumentation(auth)) {
    return true;
  }

  return documentationRedirectUrlTree(auth, router);
};
