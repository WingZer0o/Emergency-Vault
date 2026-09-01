import { Injectable, effect, inject, signal, computed } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import {
  AES_ALGO,
  DEFAULT_KDF_ITERATIONS,
  deriveMasterKey,
  exportPublicKeySpki,
  generateRecoveryKey,
  generateSaltBase64,
  generateUvk,
  generateVaultKeypair,
  importRecoveryKey,
  toBase64,
  unwrapPrivateKey,
  unwrapRawBytes,
  wrapPrivateKey,
  wrapRawBytes,
} from '../crypto/webcrypto.util';
import { unwrapUvkVersioned, wrapUvkHybrid } from '../crypto/hybrid-kem.util';
import { generateMlKemKeypair } from '../crypto/ml-kem.util';
import { clearVaultKeys, loadVaultKeys, saveVaultKeys } from '../crypto/vault-key-store';
import { VaultService } from './vault.service';

/** Auto-lock after this long with no mouse/keyboard/touch/scroll activity, matching how password managers like Bitwarden bound how long an unlocked vault persists unattended. */
export const VAULT_IDLE_TIMEOUT_MS = 20 * 60 * 1000;

const ACTIVITY_EVENTS = ['mousedown', 'keydown', 'scroll', 'touchstart'] as const;

/**
 * Holds the vault's unwrapped key material for the current session. Persisted to IndexedDB (see
 * core/crypto/vault-key-store.ts) so it survives a page refresh - IndexedDB preserves a CryptoKey's
 * non-extractable flag via structured clone, so this never exposes raw key bytes to JS, unlike
 * storing anything in localStorage/sessionStorage would. (The ML-KEM secret key is an exception to
 * that non-extractable guarantee - see vault-key-store.ts.) Cleared on lock() (explicit logout or
 * idle timeout) and on a stale/failed restore attempt.
 */
@Injectable({ providedIn: 'root' })
export class VaultSessionService {
  private readonly vaultService = inject(VaultService);

  private readonly privateKeySig = signal<CryptoKey | null>(null);
  private readonly uvkSig = signal<CryptoKey | null>(null);
  /** Null for a vault that hasn't been upgraded to the hybrid PQC scheme (legacy RSA-only). */
  private readonly mlKemSecretKeySig = signal<Uint8Array<ArrayBuffer> | null>(null);
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
        this.mlKemSecretKeySig.set(stored.mlKemSecretKey);
      }
    } catch {
      // No persisted session, or IndexedDB unavailable (e.g. strict private-browsing mode) - the
      // unlock screen/guards handle this the same as any other locked state.
    }
  }

  /** Sets up a brand-new vault for the current user. New vaults always use the hybrid RSA-OAEP +
   * ML-KEM scheme. Returns the recovery key display string - show it once, it is never stored anywhere. */
  async setupVault(password: string): Promise<string> {
    const { publicKey, privateKey } = await generateVaultKeypair();
    const mlKem = generateMlKemKeypair();
    const uvk = await generateUvk();
    const kdfSalt = generateSaltBase64();
    const masterKey = await deriveMasterKey(password, kdfSalt, DEFAULT_KDF_ITERATIONS);
    const recovery = await generateRecoveryKey();

    const [
      privateKeyWrappedByMasterKey,
      privateKeyWrappedByRecoveryKey,
      mlKemSecretKeyWrappedByMasterKey,
      mlKemSecretKeyWrappedByRecoveryKey,
      selfWrappedUvk,
      publicKeySpki,
    ] = await Promise.all([
      wrapPrivateKey(privateKey, masterKey),
      wrapPrivateKey(privateKey, recovery.key),
      wrapRawBytes(mlKem.secretKey, masterKey),
      wrapRawBytes(mlKem.secretKey, recovery.key),
      wrapUvkHybrid(uvk, publicKey, mlKem.publicKey),
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
        mlKemPublicKey: toBase64(mlKem.publicKey),
        mlKemSecretKeyWrappedByMasterKey,
        mlKemSecretKeyWrappedByRecoveryKey,
      }),
    );

    await this.persistSession(privateKey, uvk, mlKem.secretKey);

    return recovery.displayValue;
  }

  /** Unlocks the vault with the account password. Rejects if the password is wrong - a GCM auth-tag mismatch doubles as the password check. */
  async unlockWithPassword(password: string): Promise<void> {
    const bundle = await firstValueFrom(this.vaultService.getUnlockBundle());
    const masterKey = await deriveMasterKey(password, bundle.kdfSalt, bundle.kdfIterations);
    await this.unlockFromBundle(
      masterKey,
      bundle.privateKeyWrappedByMasterKey,
      bundle.mlKemSecretKeyWrappedByMasterKey,
      bundle.wrappedUvkSelf,
    );
  }

  /** Unlocks the vault with the one-time recovery key instead of the password. */
  async unlockWithRecoveryKey(recoveryKeyDisplay: string): Promise<void> {
    const bundle = await firstValueFrom(this.vaultService.getUnlockBundle());
    const recoveryKey = await importRecoveryKey(recoveryKeyDisplay);
    await this.unlockFromBundle(
      recoveryKey,
      bundle.privateKeyWrappedByRecoveryKey,
      bundle.mlKemSecretKeyWrappedByRecoveryKey,
      bundle.wrappedUvkSelf,
    );
  }

  /**
   * Recovers vault access after a password reset that skipped the vault rewrap step: unwraps the
   * private key (and ML-KEM secret key, if this vault has one) with the recovery key, re-wraps
   * them under a Master Key derived from the (new) current password, and persists the new
   * wrapping server-side via /vault/recover.
   */
  async recoverWithRecoveryKey(recoveryKeyDisplay: string, currentPassword: string): Promise<void> {
    const bundle = await firstValueFrom(this.vaultService.getUnlockBundle());
    const recoveryKey = await importRecoveryKey(recoveryKeyDisplay);
    const privateKey = await unwrapPrivateKey(bundle.privateKeyWrappedByRecoveryKey, recoveryKey, true);
    const mlKemSecretKey = bundle.mlKemSecretKeyWrappedByRecoveryKey
      ? await unwrapRawBytes(bundle.mlKemSecretKeyWrappedByRecoveryKey, recoveryKey)
      : null;

    const newKdfSalt = generateSaltBase64();
    const newMasterKey = await deriveMasterKey(currentPassword, newKdfSalt, DEFAULT_KDF_ITERATIONS);
    const newPrivateKeyWrappedByMasterKey = await wrapPrivateKey(privateKey, newMasterKey);
    const newMlKemSecretKeyWrappedByMasterKey = mlKemSecretKey
      ? await wrapRawBytes(mlKemSecretKey, newMasterKey)
      : undefined;

    await firstValueFrom(
      this.vaultService.recover({
        newKdfSalt,
        newKdfIterations: DEFAULT_KDF_ITERATIONS,
        newPrivateKeyWrappedByMasterKey,
        newMlKemSecretKeyWrappedByMasterKey,
      }),
    );

    const uvk = await unwrapUvkVersioned(bundle.wrappedUvkSelf, privateKey, mlKemSecretKey, true);
    await this.persistSession(privateKey, uvk, mlKemSecretKey);
  }

  /**
   * Changes the vault's own password - independent of the account sign-in password. Unwraps the
   * private key (and ML-KEM secret key, if this vault has one) using the Master Key derived from
   * the *current* vault password, re-wraps them under a fresh Master Key derived from the *new*
   * vault password, and persists the new wrapping via /vault/recover. Rejects with a GCM
   * auth-tag mismatch if currentPassword is wrong - callers should treat any rejection here as
   * "current vault password is incorrect" and not touch the account password at all.
   */
  async changeVaultPassword(currentPassword: string, newPassword: string): Promise<void> {
    const bundle = await firstValueFrom(this.vaultService.getUnlockBundle());
    const currentMasterKey = await deriveMasterKey(currentPassword, bundle.kdfSalt, bundle.kdfIterations);
    const privateKey = await unwrapPrivateKey(bundle.privateKeyWrappedByMasterKey, currentMasterKey, true);
    const mlKemSecretKey = bundle.mlKemSecretKeyWrappedByMasterKey
      ? await unwrapRawBytes(bundle.mlKemSecretKeyWrappedByMasterKey, currentMasterKey)
      : null;

    const newKdfSalt = generateSaltBase64();
    const newMasterKey = await deriveMasterKey(newPassword, newKdfSalt, DEFAULT_KDF_ITERATIONS);
    const newPrivateKeyWrappedByMasterKey = await wrapPrivateKey(privateKey, newMasterKey);
    const newMlKemSecretKeyWrappedByMasterKey = mlKemSecretKey
      ? await wrapRawBytes(mlKemSecretKey, newMasterKey)
      : undefined;

    await firstValueFrom(
      this.vaultService.recover({
        newKdfSalt,
        newKdfIterations: DEFAULT_KDF_ITERATIONS,
        newPrivateKeyWrappedByMasterKey,
        newMlKemSecretKeyWrappedByMasterKey,
      }),
    );
  }

  /** Unwraps a UVK that was wrapped for this user as an emergency-access recipient, not their own vault. Non-extractable - a holder never needs to re-share someone else's vault. */
  async unwrapSharedUvk(wrappedUvk: string): Promise<CryptoKey> {
    const privateKey = this.privateKeySig();
    if (!privateKey) {
      throw new Error('Vault is not unlocked.');
    }

    return unwrapUvkVersioned(wrappedUvk, privateKey, this.mlKemSecretKeySig(), false);
  }

  /** Unwraps an item-scoped share's own key - wrapped directly for this holder's public key, never
   * via any UVK, so a holder with only an item-scoped share never touches any other item. Same
   * mechanism as unwrapSharedUvk, under a name that reads clearly at item-share call sites. */
  async unwrapItemShareKey(wrappedKey: string): Promise<CryptoKey> {
    return this.unwrapSharedUvk(wrappedKey);
  }

  /** Unwraps one of the current owner's own items' keys, via their own UVK. Extractable - the
   * owner may need to re-wrap this same key for a new item-scoped share. */
  async unwrapOwnedItemKey(wrappedItemKey: string): Promise<CryptoKey> {
    const raw = await unwrapRawBytes(wrappedItemKey, this.uvk);
    return crypto.subtle.importKey('raw', raw, AES_ALGO, true, ['encrypt', 'decrypt']);
  }

  /** Unwraps an item's key using an already-unwrapped whole-vault-share UVK (not the caller's own
   * vault) - for a holder viewing an item via a whole-vault share. Non-extractable. */
  async unwrapItemKeyViaVaultShare(wrappedItemKey: string, sharedUvk: CryptoKey): Promise<CryptoKey> {
    const raw = await unwrapRawBytes(wrappedItemKey, sharedUvk);
    return crypto.subtle.importKey('raw', raw, AES_ALGO, false, ['encrypt', 'decrypt']);
  }

  /** Locks the vault: clears in-memory key material and the persisted IndexedDB copy. Called on explicit logout and on idle timeout. */
  async lock(): Promise<void> {
    this.mlKemSecretKeySig()?.fill(0);
    this.privateKeySig.set(null);
    this.uvkSig.set(null);
    this.mlKemSecretKeySig.set(null);
    await clearVaultKeys().catch(() => {});
  }

  private async unlockFromBundle(
    wrappingKey: CryptoKey,
    wrappedPrivateKey: string,
    wrappedMlKemSecretKey: string | null,
    wrappedUvkSelf: string,
  ): Promise<void> {
    const privateKey = await unwrapPrivateKey(wrappedPrivateKey, wrappingKey, false);
    const mlKemSecretKey = wrappedMlKemSecretKey
      ? await unwrapRawBytes(wrappedMlKemSecretKey, wrappingKey)
      : null;
    const uvk = await unwrapUvkVersioned(wrappedUvkSelf, privateKey, mlKemSecretKey, true);

    await this.persistSession(privateKey, uvk, mlKemSecretKey);
  }

  private async persistSession(
    privateKey: CryptoKey,
    uvk: CryptoKey,
    mlKemSecretKey: Uint8Array<ArrayBuffer> | null,
  ): Promise<void> {
    this.privateKeySig.set(privateKey);
    this.uvkSig.set(uvk);
    this.mlKemSecretKeySig.set(mlKemSecretKey);

    try {
      await saveVaultKeys({ privateKey, uvk, mlKemSecretKey });
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
