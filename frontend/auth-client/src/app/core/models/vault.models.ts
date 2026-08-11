// Mirrors backend/src/AuthSystem.Application/Dtos/VaultDtos.cs. All wrapped-key/ciphertext
// fields here are opaque base64 blobs to this app's HTTP layer - only core/crypto/webcrypto.util.ts
// and vault-session.service.ts ever look inside them.

export interface VaultSetupRequest {
  publicKey: string;
  kdfSalt: string;
  kdfIterations: number;
  privateKeyWrappedByMasterKey: string;
  privateKeyWrappedByRecoveryKey: string;
  selfWrappedUvk: string;
  mlKemPublicKey: string;
  mlKemSecretKeyWrappedByMasterKey: string;
  mlKemSecretKeyWrappedByRecoveryKey: string;
}

export interface VaultStatusDto {
  hasVault: boolean;
}

export interface VaultUnlockBundleDto {
  kdfSalt: string;
  kdfIterations: number;
  privateKeyWrappedByMasterKey: string;
  privateKeyWrappedByRecoveryKey: string;
  wrappedUvkSelf: string;
  /** Null for a vault that predates the hybrid RSA + ML-KEM scheme and hasn't been upgraded. */
  mlKemPublicKey: string | null;
  mlKemSecretKeyWrappedByMasterKey: string | null;
  mlKemSecretKeyWrappedByRecoveryKey: string | null;
}

export interface VaultRecoverRequest {
  newKdfSalt: string;
  newKdfIterations: number;
  newPrivateKeyWrappedByMasterKey: string;
  newMlKemSecretKeyWrappedByMasterKey?: string;
}

export interface VaultResetRecoveryBundleDto {
  privateKeyWrappedByRecoveryKey: string;
  mlKemSecretKeyWrappedByRecoveryKey: string | null;
}

export interface CreateVaultItemRequest {
  ciphertextBlob: string;
  wrappedItemKey: string;
}

export interface UpdateVaultItemRequest {
  ciphertextBlob: string;
  /** Only set once, by the owner, to backfill a legacy item onto per-item-key encryption. */
  wrappedItemKey?: string;
}

export interface VaultItemDto {
  id: string;
  ciphertextBlob: string;
  /** Null for a legacy item not yet migrated to per-item keys. This item's own key, wrapped under
   * the OWNER's UVK - useless without that UVK, so it's safe on every response regardless of caller. */
  wrappedItemKey: string | null;
  /** Only set when the caller's access came via an item-scoped share: that share's own copy of
   * this item's key, wrapped directly for the caller's public key. */
  itemShareWrappedKey: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface VaultUserLookupDto {
  userId: string;
  firstName: string;
  lastName: string;
  publicKey: string;
  /** Null if this user's vault predates the hybrid RSA + ML-KEM scheme - sharing falls back to legacy RSA-only for them. */
  mlKemPublicKey: string | null;
}

export interface CreateVaultShareRequest {
  holderEmail: string;
  wrappedKey: string;
  /** Null = whole-vault share. Set = scoped to just that one item. */
  vaultItemId: string | null;
  /** Ignored (forced false) server-side unless vaultItemId is set. */
  canEdit: boolean;
}

/** Mirrors AuthSystem.Application.Dtos.VaultShareStatus - serialized as a string via JsonStringEnumConverter. */
export type VaultShareStatus = 'Pending' | 'Active' | 'CodeExpired' | 'Failed';

export interface VaultShareDto {
  id: string;
  holderUserId: string;
  holderName: string;
  holderEmail: string;
  vaultItemId: string | null;
  canEdit: boolean;
  grantedAt: string;
  status: VaultShareStatus;
}

export interface VaultSharedWithMeDto {
  id: string;
  ownerUserId: string;
  ownerName: string;
  ownerEmail: string;
  /** Null until the holder has verified the emailed code. */
  wrappedUvk: string | null;
  grantedAt: string;
  status: VaultShareStatus;
}

/** An item-scoped share held by the current user - the holder's-eye view of a single shared item. */
export interface VaultItemShareDto {
  shareId: string;
  itemId: string;
  ownerUserId: string;
  ownerName: string;
  ownerEmail: string;
  canEdit: boolean;
  grantedAt: string;
  status: VaultShareStatus;
}

export interface VerifyVaultShareRequest {
  code: string;
}

export interface SharedVaultAccessChallengeDto {
  requiresCode: boolean;
  challengeToken: string | null;
}

export interface VerifySharedVaultAccessRequest {
  challengeToken: string;
  code: string;
}

export interface ResendSharedVaultAccessRequest {
  challengeToken: string;
}

/** A single labeled field within a vault item (e.g. { name: 'username', value: 'mike' }). */
export interface VaultItemField {
  name: string;
  value: string;
}

/** The plaintext shape encrypted into VaultItemDto.ciphertextBlob - the server never sees this. */
export interface VaultItemPayload {
  label: string;
  itemType: string;
  fields: VaultItemField[];
}

/** A vault item after client-side decryption, ready for display. */
export interface DecryptedVaultItem extends VaultItemPayload {
  id: string;
  createdAt: string;
  updatedAt: string;
}

/** An encrypted file attached to a vault item. FileName/ContentType/SizeBytes are cleartext (a
 * deliberate, narrow exception to the vault's zero-knowledge model) so the server can enforce the
 * PDF/DOCX allowlist and the client can label a download without decrypting first; the file's
 * bytes themselves are only ever fetched and decrypted on demand. */
export interface VaultItemAttachmentDto {
  id: string;
  vaultItemId: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  iv: string;
  createdAt: string;
}

/** One event in a vault item's view history - a non-owner viewed it via the shared-vault flow. */
export interface VaultItemViewDto {
  viewerUserId: string;
  viewerName: string;
  viewerEmail: string;
  viewedAt: string;
}
