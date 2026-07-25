import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';

/** Réservé aux FirmManager (ex. écran d'affectation des dossiers). */
export const firmManagerGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  if (auth.isFirmManager()) {
    return true;
  }
  return inject(Router).createUrlTree(['/firm/clients']);
};
