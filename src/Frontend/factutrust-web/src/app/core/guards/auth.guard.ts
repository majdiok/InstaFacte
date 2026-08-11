import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated() && authService.getAccessToken()) {
    return true;
  }

  // Already on login: avoid a redundant navigate that can abort View Transitions
  // (`InvalidStateError: Transition was aborted because of invalid state`).
  const path = state.url.split('?')[0];
  if (path === '/auth/login' || path.startsWith('/auth/login/')) {
    return false;
  }

  // Store the attempted URL for redirecting after login
  void router.navigate(['/auth/login'], {
    queryParams: { returnUrl: state.url }
  });

  return false;
};
