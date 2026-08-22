import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { VaultService } from '../services/vault.service';
import { VaultSessionService } from '../services/vault-session.service';

/** Guards the vault's actual content routes: no vault -> setup, vault but locked -> unlock, otherwise allow.
 * Always confirms with the backend, even when the local session already looks unlocked - a locally
 * cached session (persisted in IndexedDB, see VaultSessionService) can outlive the server-side vault
 * profile, e.g. after a DB reset in dev, so isUnlocked() alone isn't sufficient. */
export const vaultUnlockedGuard: CanActivateFn = async () => {
  const vaultSession = inject(VaultSessionService);
  const vaultService = inject(VaultService);
  const router = inject(Router);

  const status = await firstValueFrom(vaultService.getStatus());
  if (!status.hasVault) {
    if (vaultSession.isUnlocked()) {
      await vaultSession.lock();
    }
    return router.createUrlTree(['/vault/setup']);
  }

  if (vaultSession.isUnlocked()) {
    return true;
  }

  return router.createUrlTree(['/vault/unlock']);
};
