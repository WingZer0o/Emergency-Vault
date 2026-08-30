import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from '../services/auth.service';
import { isDesktopApp } from '../utils/desktop.util';

/**
 * The marketing landing page doesn't make sense inside the compiled desktop app - whoever's
 * looking at it already downloaded and installed it. Skip straight to login (or into the app,
 * if a refresh cookie already restored the session).
 */
export const desktopLandingGuard: CanActivateFn = () => {
  if (!isDesktopApp()) {
    return true;
  }

  const authService = inject(AuthService);
  const router = inject(Router);
  return router.createUrlTree([authService.isAuthenticated() ? '/vault' : '/login']);
};
