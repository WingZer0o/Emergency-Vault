import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { VaultService } from '../services/vault.service';

/** Guards routes that require a vault to already exist (unlock, recover) - sends a user with no vault to set one up first. */
export const vaultSetupGuard: CanActivateFn = async () => {
  const vaultService = inject(VaultService);
  const router = inject(Router);

  const status = await firstValueFrom(vaultService.getStatus());
  return status.hasVault ? true : router.createUrlTree(['/vault/setup']);
};
