import { Routes } from '@angular/router';

import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { subscriptionGuard } from './core/guards/subscription.guard';
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
    path: 'profile',
    canActivate: [authGuard, roleGuard('User')],
    loadComponent: () => import('./features/profile/profile').then((m) => m.Profile),
  },
  {
    path: 'billing',
    canActivate: [authGuard],
    loadComponent: () => import('./features/billing/billing').then((m) => m.Billing),
  },
  {
    path: 'password-generator',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/password-generator/password-generator').then((m) => m.PasswordGenerator),
  },
  {
    path: 'admin',
    canActivate: [authGuard, roleGuard('Admin')],
    loadComponent: () => import('./features/admin/admin-home/admin-home').then((m) => m.AdminHome),
  },
  {
    path: 'admin/billing-settings',
    canActivate: [authGuard, roleGuard('Admin')],
    loadComponent: () =>
      import('./features/admin/billing-settings/billing-settings').then((m) => m.BillingSettings),
  },
  {
    path: 'admin/subscriptions',
    canActivate: [authGuard, roleGuard('Admin')],
    loadComponent: () =>
      import('./features/admin/subscriptions/admin-subscriptions').then((m) => m.AdminSubscriptions),
  },
  {
    path: 'vault/setup',
    canActivate: [authGuard, subscriptionGuard],
    loadComponent: () => import('./features/vault/setup/vault-setup').then((m) => m.VaultSetup),
  },
  {
    path: 'vault/unlock',
    canActivate: [authGuard, subscriptionGuard, vaultSetupGuard],
    loadComponent: () => import('./features/vault/unlock/vault-unlock').then((m) => m.VaultUnlock),
  },
  {
    path: 'vault/recover',
    canActivate: [authGuard, subscriptionGuard, vaultSetupGuard],
    loadComponent: () => import('./features/vault/recover/vault-recover').then((m) => m.VaultRecover),
  },
  {
    path: 'vault/items/new',
    canActivate: [authGuard, subscriptionGuard, vaultUnlockedGuard],
    loadComponent: () => import('./features/vault/item-create/vault-item-create').then((m) => m.VaultItemCreate),
  },
  {
    path: 'vault/items/:id',
    canActivate: [authGuard, subscriptionGuard, vaultUnlockedGuard],
    loadComponent: () => import('./features/vault/item-detail/vault-item-detail').then((m) => m.VaultItemDetail),
  },
  {
    path: 'vault/sharing',
    canActivate: [authGuard, subscriptionGuard, vaultUnlockedGuard],
    loadComponent: () => import('./features/vault/sharing/vault-sharing').then((m) => m.VaultSharing),
  },
  {
    path: 'vault/shared-with-me',
    canActivate: [authGuard, subscriptionGuard, vaultUnlockedGuard],
    loadComponent: () =>
      import('./features/vault/shared-with-me/shared-with-me-list').then((m) => m.SharedWithMeList),
  },
  {
    path: 'vault/shared-with-me/:ownerUserId',
    canActivate: [authGuard, subscriptionGuard, vaultUnlockedGuard],
    loadComponent: () =>
      import('./features/vault/shared-with-me/shared-vault-view').then((m) => m.SharedVaultView),
  },
  {
    path: 'vault/shared-with-me/:ownerUserId/items/:itemId',
    canActivate: [authGuard, subscriptionGuard, vaultUnlockedGuard],
    loadComponent: () =>
      import('./features/vault/shared-with-me/shared-vault-item-detail').then((m) => m.SharedVaultItemDetail),
  },
  {
    path: 'vault',
    canActivate: [authGuard, subscriptionGuard, vaultUnlockedGuard],
    loadComponent: () => import('./features/vault/item-list/vault-item-list').then((m) => m.VaultItemList),
  },
  { path: '**', redirectTo: 'dashboard' },
];
