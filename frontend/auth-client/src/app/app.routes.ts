import { Routes } from '@angular/router';

import { authGuard } from './core/guards/auth.guard';
import { desktopLandingGuard } from './core/guards/desktop-landing.guard';
import { roleGuard } from './core/guards/role.guard';
import { subscriptionGuard } from './core/guards/subscription.guard';
import { vaultSetupGuard } from './core/guards/vault-setup.guard';
import { vaultUnlockedGuard } from './core/guards/vault-unlocked.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    title: 'Emergency Vault',
    canActivate: [desktopLandingGuard],
    loadComponent: () => import('./features/landing/landing').then((m) => m.Landing),
  },
  {
    path: 'login',
    title: 'Log In - Emergency Vault',
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
  },
  {
    path: 'register',
    title: 'Sign Up - Emergency Vault',
    loadComponent: () => import('./features/auth/register/register').then((m) => m.Register),
  },
  {
    path: 'forgot-password',
    title: 'Forgot Password - Emergency Vault',
    loadComponent: () =>
      import('./features/auth/forgot-password/forgot-password').then((m) => m.ForgotPassword),
  },
  {
    path: 'reset-password',
    title: 'Reset Password - Emergency Vault',
    loadComponent: () => import('./features/auth/reset-password/reset-password').then((m) => m.ResetPassword),
  },
  {
    path: 'confirm-email',
    title: 'Confirm Email - Emergency Vault',
    loadComponent: () => import('./features/auth/confirm-email/confirm-email').then((m) => m.ConfirmEmail),
  },
  {
    path: 'unlock-account',
    title: 'Unlock Account - Emergency Vault',
    loadComponent: () =>
      import('./features/auth/unlock-account/unlock-account').then((m) => m.UnlockAccount),
  },
  {
    path: 'profile',
    title: 'Profile - Emergency Vault',
    canActivate: [authGuard, roleGuard('User')],
    loadComponent: () => import('./features/profile/profile').then((m) => m.Profile),
  },
  {
    path: 'billing',
    title: 'Billing - Emergency Vault',
    canActivate: [authGuard],
    loadComponent: () => import('./features/billing/billing').then((m) => m.Billing),
  },
  {
    path: 'password-generator',
    title: 'Password Generator - Emergency Vault',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/password-generator/password-generator').then((m) => m.PasswordGenerator),
  },
  {
    path: 'admin',
    title: 'Admin - Emergency Vault',
    canActivate: [authGuard, roleGuard('Admin')],
    loadComponent: () => import('./features/admin/admin-home/admin-home').then((m) => m.AdminHome),
  },
  {
    path: 'admin/billing-settings',
    title: 'Billing Settings - Emergency Vault',
    canActivate: [authGuard, roleGuard('Admin')],
    loadComponent: () =>
      import('./features/admin/billing-settings/billing-settings').then((m) => m.BillingSettings),
  },
  {
    path: 'admin/subscriptions',
    title: 'Subscriptions - Emergency Vault',
    canActivate: [authGuard, roleGuard('Admin')],
    loadComponent: () =>
      import('./features/admin/subscriptions/admin-subscriptions').then((m) => m.AdminSubscriptions),
  },
  {
    path: 'vault/setup',
    title: 'Set Up Vault - Emergency Vault',
    canActivate: [authGuard, subscriptionGuard],
    loadComponent: () => import('./features/vault/setup/vault-setup').then((m) => m.VaultSetup),
  },
  {
    path: 'vault/unlock',
    title: 'Unlock Vault - Emergency Vault',
    canActivate: [authGuard, subscriptionGuard, vaultSetupGuard],
    loadComponent: () => import('./features/vault/unlock/vault-unlock').then((m) => m.VaultUnlock),
  },
  {
    path: 'vault/recover',
    title: 'Recover Vault - Emergency Vault',
    canActivate: [authGuard, subscriptionGuard, vaultSetupGuard],
    loadComponent: () => import('./features/vault/recover/vault-recover').then((m) => m.VaultRecover),
  },
  {
    path: 'vault/items/new',
    title: 'New Item - Emergency Vault',
    canActivate: [authGuard, subscriptionGuard, vaultUnlockedGuard],
    loadComponent: () => import('./features/vault/item-create/vault-item-create').then((m) => m.VaultItemCreate),
  },
  {
    path: 'vault/items/:id',
    title: 'Item Details - Emergency Vault',
    canActivate: [authGuard, subscriptionGuard, vaultUnlockedGuard],
    loadComponent: () => import('./features/vault/item-detail/vault-item-detail').then((m) => m.VaultItemDetail),
  },
  {
    path: 'vault/sharing',
    title: 'Sharing - Emergency Vault',
    canActivate: [authGuard, subscriptionGuard, vaultUnlockedGuard],
    loadComponent: () => import('./features/vault/sharing/vault-sharing').then((m) => m.VaultSharing),
  },
  {
    path: 'vault/shared-with-me',
    title: 'Shared With Me - Emergency Vault',
    canActivate: [authGuard, subscriptionGuard, vaultUnlockedGuard],
    loadComponent: () =>
      import('./features/vault/shared-with-me/shared-with-me-list').then((m) => m.SharedWithMeList),
  },
  {
    path: 'vault/shared-with-me/:ownerUserId',
    title: 'Shared Vault - Emergency Vault',
    canActivate: [authGuard, subscriptionGuard, vaultUnlockedGuard],
    loadComponent: () =>
      import('./features/vault/shared-with-me/shared-vault-view').then((m) => m.SharedVaultView),
  },
  {
    path: 'vault/shared-with-me/:ownerUserId/items/:itemId',
    title: 'Shared Item Details - Emergency Vault',
    canActivate: [authGuard, subscriptionGuard, vaultUnlockedGuard],
    loadComponent: () =>
      import('./features/vault/shared-with-me/shared-vault-item-detail').then((m) => m.SharedVaultItemDetail),
  },
  {
    path: 'vault',
    title: 'My Vault - Emergency Vault',
    canActivate: [authGuard, subscriptionGuard, vaultUnlockedGuard],
    loadComponent: () => import('./features/vault/item-list/vault-item-list').then((m) => m.VaultItemList),
  },
  { path: '**', redirectTo: 'vault' },
];
