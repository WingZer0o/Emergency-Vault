import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '../services/auth.service';
import { BillingService } from '../services/billing.service';

/** Hard paywall: sends a user with no active subscription (and no free-access grant) to /billing. Admins bypass. */
export const subscriptionGuard: CanActivateFn = async () => {
  const authService = inject(AuthService);
  const billingService = inject(BillingService);
  const router = inject(Router);

  if (authService.hasRole('Admin')) {
    return true;
  }

  const status = await firstValueFrom(billingService.getStatus());
  return status.hasAccess ? true : router.createUrlTree(['/billing']);
};
