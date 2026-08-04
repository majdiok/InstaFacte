import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';

function pathWithoutQuery(url: string): string {
  const q = url.indexOf('?');
  return q >= 0 ? url.slice(0, q) : url;
}

/** Réservé aux FirmManager (responsable cabinet). */
export const firmManagerGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  if (auth.isFirmManager()) {
    return true;
  }
  return inject(Router).createUrlTree(['/access-denied'], {
    queryParams: { returnUrl: pathWithoutQuery(state.url) }
  });
};
