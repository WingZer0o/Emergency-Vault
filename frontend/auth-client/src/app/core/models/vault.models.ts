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
}

export interface VaultRecoverRequest {
  newKdfSalt: string;
  newKdfIterations: number;
  newPrivateKeyWrappedByMasterKey: string;
}

export interface VaultResetRecoveryBundleDto {
  privateKeyWrappedByRecoveryKey: string;
}

export interface CreateVaultItemRequest {
  ciphertextBlob: string;
}

export interface UpdateVaultItemRequest {
  ciphertextBlob: string;
}

export interface VaultItemDto {
  id: string;
  ciphertextBlob: string;
  createdAt: string;
  updatedAt: string;
}

export interface VaultUserLookupDto {
  userId: string;
  firstName: string;
  lastName: string;
  publicKey: string;
}

export interface CreateVaultShareRequest {
  holderEmail: string;
  wrappedUvk: string;
}

/** Mirrors AuthSystem.Application.Dtos.VaultShareStatus - serialized as a string via JsonStringEnumConverter. */
export type VaultShareStatus = 'Pending' | 'Active' | 'CodeExpired' | 'Failed';

export interface VaultShareDto {
  id: string;
  holderUserId: string;
  holderName: string;
  holderEmail: string;
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

export interface VerifyVaultShareRequest {
  code: string;
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
