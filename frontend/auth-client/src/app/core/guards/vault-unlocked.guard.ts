import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { VaultService } from '../services/vault.service';
import { VaultSessionService } from '../services/vault-session.service';

/** Guards the vault's actual content routes: no vault -> setup, vault but locked -> unlock, otherwise allow. */
export const vaultUnlockedGuard: CanActivateFn = async () => {
  const vaultSession = inject(VaultSessionService);
  if (vaultSession.isUnlocked()) {
    return true;
  }

  const vaultService = inject(VaultService);
  const router = inject(Router);

  const status = await firstValueFrom(vaultService.getStatus());
  return router.createUrlTree(status.hasVault ? ['/vault/unlock'] : ['/vault/setup']);
};
