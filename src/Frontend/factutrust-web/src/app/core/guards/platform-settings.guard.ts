import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/** Accès au hub et sous-pages paramètres plateforme (pas `/settings/profile`). */
export const platformSettingsGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    router.navigate(['/auth/login'], { queryParams: { returnUrl: state.url } });
    return false;
  }

  if (auth.canAccessPlatformSettings()) {
    return true;
  }

  router.navigate(['/access-denied'], { queryParams: { returnUrl: state.url } });
  return false;
};
