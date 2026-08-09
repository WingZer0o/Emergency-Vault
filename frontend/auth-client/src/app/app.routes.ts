import { Routes } from '@angular/router';

import { authGuard } from './core/guards/auth.guard';
import { vaultSetupGuard } from './core/guards/vault-setup.guard';
import { vaultUnlockedGuard } from './core/guards/vault-unlocked.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./features/landing/landing').then((m) => m.Landing),
  },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register/register').then((m) => m.Register),
  },
  {
    path: 'forgot-password',
    loadComponent: () =>
      import('./features/auth/forgot-password/forgot-password').then((m) => m.ForgotPassword),
  },
  {
    path: 'reset-password',
    loadComponent: () => import('./features/auth/reset-password/reset-password').then((m) => m.ResetPassword),
  },
  {
    path: 'confirm-email',
    loadComponent: () => import('./features/auth/confirm-email/confirm-email').then((m) => m.ConfirmEmail),
  },
  {
    path: 'unlock-account',
    loadComponent: () =>
      import('./features/auth/unlock-account/unlock-account').then((m) => m.UnlockAccount),
  },
  {
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
  },
  {
    path: 'vault/setup',
    canActivate: [authGuard],
    loadComponent: () => import('./features/vault/setup/vault-setup').then((m) => m.VaultSetup),
  },
  {
    path: 'vault/unlock',
    canActivate: [authGuard, vaultSetupGuard],
    loadComponent: () => import('./features/vault/unlock/vault-unlock').then((m) => m.VaultUnlock),
  },
  {
    path: 'vault/recover',
    canActivate: [authGuard, vaultSetupGuard],
    loadComponent: () => import('./features/vault/recover/vault-recover').then((m) => m.VaultRecover),
  },
  {
    path: 'vault/items/new',
    canActivate: [authGuard, vaultUnlockedGuard],
    loadComponent: () => import('./features/vault/item-create/vault-item-create').then((m) => m.VaultItemCreate),
  },
  {
    path: 'vault/items/:id',
    canActivate: [authGuard, vaultUnlockedGuard],
    loadComponent: () => import('./features/vault/item-detail/vault-item-detail').then((m) => m.VaultItemDetail),
  },
  {
    path: 'vault/sharing',
    canActivate: [authGuard, vaultUnlockedGuard],
    loadComponent: () => import('./features/vault/sharing/vault-sharing').then((m) => m.VaultSharing),
  },
  {
    path: 'vault/shared-with-me',
    canActivate: [authGuard, vaultUnlockedGuard],
    loadComponent: () =>
      import('./features/vault/shared-with-me/shared-with-me-list').then((m) => m.SharedWithMeList),
  },
  {
    path: 'vault/shared-with-me/:ownerUserId',
    canActivate: [authGuard, vaultUnlockedGuard],
    loadComponent: () =>
      import('./features/vault/shared-with-me/shared-vault-view').then((m) => m.SharedVaultView),
  },
  {
    path: 'vault',
    canActivate: [authGuard, vaultUnlockedGuard],
    loadComponent: () => import('./features/vault/item-list/vault-item-list').then((m) => m.VaultItemList),
  },
  { path: '**', redirectTo: 'dashboard' },
];
