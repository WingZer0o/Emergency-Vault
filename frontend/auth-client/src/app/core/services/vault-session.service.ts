import { Injectable, effect, inject, signal, computed } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import {
  DEFAULT_KDF_ITERATIONS,
  deriveMasterKey,
  exportPublicKeySpki,
  generateRecoveryKey,
  generateSaltBase64,
  generateUvk,
  generateVaultKeypair,
  importRecoveryKey,
  unwrapPrivateKey,
  unwrapUvk,
  wrapPrivateKey,
  wrapUvkForPublicKey,
} from '../crypto/webcrypto.util';
import { clearVaultKeys, loadVaultKeys, saveVaultKeys } from '../crypto/vault-key-store';
import { VaultService } from './vault.service';

/** Auto-lock after this long with no mouse/keyboard/touch/scroll activity, matching how password managers like Bitwarden bound how long an unlocked vault persists unattended. */
export const VAULT_IDLE_TIMEOUT_MS = 20 * 60 * 1000;

const ACTIVITY_EVENTS = ['mousedown', 'keydown', 'scroll', 'touchstart'] as const;

/**
 * Holds the vault's unwrapped key material for the current session. Persisted to IndexedDB (see
 * core/crypto/vault-key-store.ts) so it survives a page refresh - IndexedDB preserves a CryptoKey's
 * non-extractable flag via structured clone, so this never exposes raw key bytes to JS, unlike
 * storing anything in localStorage/sessionStorage would. Cleared on lock() (explicit logout or
 * idle timeout) and on a stale/failed restore attempt.
 */
@Injectable({ providedIn: 'root' })
export class VaultSessionService {
  private readonly vaultService = inject(VaultService);

  private readonly privateKeySig = signal<CryptoKey | null>(null);
  private readonly uvkSig = signal<CryptoKey | null>(null);
  private idleTimer: ReturnType<typeof setTimeout> | null = null;
  private readonly onActivity = () => this.resetIdleTimer();

  readonly isUnlocked = computed(() => this.privateKeySig() !== null && this.uvkSig() !== null);

  constructor() {
    effect(() => {
      if (this.isUnlocked()) {
        this.startIdleTimer();
      } else {
        this.stopIdleTimer();
      }
    });
  }

  /** The unwrapped User Vault Key for the current session. Throws if the vault isn't unlocked - callers should check isUnlocked() or rely on vaultUnlockedGuard. */
  get uvk(): CryptoKey {
    const key = this.uvkSig();
    if (!key) {
      throw new Error('Vault is not unlocked.');
    }
    return key;
  }

  /** Attempts to restore a previously unlocked session from IndexedDB - call once at app startup, before any vault route/guard runs. No-ops if nothing was persisted or storage is unavailable. */
  async restoreFromStorage(): Promise<void> {
    try {
      const stored = await loadVaultKeys();
      if (stored) {
        this.privateKeySig.set(stored.privateKey);
        this.uvkSig.set(stored.uvk);
      }
    } catch {
      // No persisted session, or IndexedDB unavailable (e.g. strict private-browsing mode) - the
      // unlock screen/guards handle this the same as any other locked state.
    }
  }

  /** Sets up a brand-new vault for the current user. Returns the recovery key display string - show it once, it is never stored anywhere. */
  async setupVault(password: string): Promise<string> {
    const { publicKey, privateKey } = await generateVaultKeypair();
    const uvk = await generateUvk();
    const kdfSalt = generateSaltBase64();
    const masterKey = await deriveMasterKey(password, kdfSalt, DEFAULT_KDF_ITERATIONS);
    const recovery = await generateRecoveryKey();

    const [privateKeyWrappedByMasterKey, privateKeyWrappedByRecoveryKey, selfWrappedUvk, publicKeySpki] =
      await Promise.all([
        wrapPrivateKey(privateKey, masterKey),
        wrapPrivateKey(privateKey, recovery.key),
        wrapUvkForPublicKey(uvk, publicKey),
        exportPublicKeySpki(publicKey),
      ]);

    await firstValueFrom(
      this.vaultService.setup({
        publicKey: publicKeySpki,
        kdfSalt,
        kdfIterations: DEFAULT_KDF_ITERATIONS,
        privateKeyWrappedByMasterKey,
        privateKeyWrappedByRecoveryKey,
        selfWrappedUvk,
      }),
    );

    await this.persistSession(privateKey, uvk);

    return recovery.displayValue;
  }

  /** Unlocks the vault with the account password. Rejects if the password is wrong - a GCM auth-tag mismatch doubles as the password check. */
  async unlockWithPassword(password: string): Promise<void> {
    const bundle = await firstValueFrom(this.vaultService.getUnlockBundle());
    const masterKey = await deriveMasterKey(password, bundle.kdfSalt, bundle.kdfIterations);
    await this.unlockFromBundle(masterKey, bundle.privateKeyWrappedByMasterKey, bundle.wrappedUvkSelf);
  }

  /** Unlocks the vault with the one-time recovery key instead of the password. */
  async unlockWithRecoveryKey(recoveryKeyDisplay: string): Promise<void> {
    const bundle = await firstValueFrom(this.vaultService.getUnlockBundle());
    const recoveryKey = await importRecoveryKey(recoveryKeyDisplay);
    await this.unlockFromBundle(recoveryKey, bundle.privateKeyWrappedByRecoveryKey, bundle.wrappedUvkSelf);
  }

  /**
   * Recovers vault access after a password reset that skipped the vault rewrap step: unwraps the
   * private key with the recovery key, re-wraps it under a Master Key derived from the (new)
   * current password, and persists the new wrapping server-side via /vault/recover.
   */
  async recoverWithRecoveryKey(recoveryKeyDisplay: string, currentPassword: string): Promise<void> {
    const bundle = await firstValueFrom(this.vaultService.getUnlockBundle());
    const recoveryKey = await importRecoveryKey(recoveryKeyDisplay);
    const privateKey = await unwrapPrivateKey(bundle.privateKeyWrappedByRecoveryKey, recoveryKey, true);

    const newKdfSalt = generateSaltBase64();
    const newMasterKey = await deriveMasterKey(currentPassword, newKdfSalt, DEFAULT_KDF_ITERATIONS);
    const newPrivateKeyWrappedByMasterKey = await wrapPrivateKey(privateKey, newMasterKey);

    await firstValueFrom(
      this.vaultService.recover({
        newKdfSalt,
        newKdfIterations: DEFAULT_KDF_ITERATIONS,
        newPrivateKeyWrappedByMasterKey,
      }),
    );

    const uvk = await unwrapUvk(bundle.wrappedUvkSelf, privateKey, true);
    await this.persistSession(privateKey, uvk);
  }

  /** Unwraps a UVK that was wrapped for this user as an emergency-access recipient, not their own vault. Non-extractable - a holder never needs to re-share someone else's vault. */
  async unwrapSharedUvk(wrappedUvk: string): Promise<CryptoKey> {
    const privateKey = this.privateKeySig();
    if (!privateKey) {
      throw new Error('Vault is not unlocked.');
    }

    return unwrapUvk(wrappedUvk, privateKey, false);
  }

  /** Locks the vault: clears in-memory key material and the persisted IndexedDB copy. Called on explicit logout and on idle timeout. */
  async lock(): Promise<void> {
    this.privateKeySig.set(null);
    this.uvkSig.set(null);
    await clearVaultKeys().catch(() => {});
  }

  private async unlockFromBundle(
    wrappingKey: CryptoKey,
    wrappedPrivateKey: string,
    wrappedUvkSelf: string,
  ): Promise<void> {
    const privateKey = await unwrapPrivateKey(wrappedPrivateKey, wrappingKey, false);
    const uvk = await unwrapUvk(wrappedUvkSelf, privateKey, true);

    await this.persistSession(privateKey, uvk);
  }

  private async persistSession(privateKey: CryptoKey, uvk: CryptoKey): Promise<void> {
    this.privateKeySig.set(privateKey);
    this.uvkSig.set(uvk);

    try {
      await saveVaultKeys({ privateKey, uvk });
    } catch {
      // Best-effort - the session still works for the current tab even if IndexedDB persistence
      // fails (e.g. strict private-browsing mode); it just won't survive a refresh.
    }
  }

  private startIdleTimer(): void {
    for (const event of ACTIVITY_EVENTS) {
      document.addEventListener(event, this.onActivity, { passive: true });
    }
    this.resetIdleTimer();
  }

  private stopIdleTimer(): void {
    for (const event of ACTIVITY_EVENTS) {
      document.removeEventListener(event, this.onActivity);
    }
    if (this.idleTimer !== null) {
      clearTimeout(this.idleTimer);
      this.idleTimer = null;
    }
  }

  private resetIdleTimer(): void {
    if (this.idleTimer !== null) {
      clearTimeout(this.idleTimer);
    }
    this.idleTimer = setTimeout(() => this.lock(), VAULT_IDLE_TIMEOUT_MS);
  }
}
