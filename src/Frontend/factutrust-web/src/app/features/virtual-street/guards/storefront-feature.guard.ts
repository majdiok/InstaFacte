import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { environment } from '../../../../environments/environment';

/** Hides the public virtual street when disabled in environment (API may still be off server-side). */
export const storefrontFeatureGuard: CanActivateFn = () => {
  if (environment.storefrontEnabled) {
    return true;
  }
  const router = inject(Router);
  return router.createUrlTree(['/']);
};
