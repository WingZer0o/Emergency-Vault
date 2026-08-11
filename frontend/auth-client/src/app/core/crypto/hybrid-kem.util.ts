// Hybrid RSA-OAEP + ML-KEM key wrapping for the UVK, alongside the legacy pure-RSA-OAEP scheme in
// webcrypto.util.ts. Uses a robust KEM combiner (Giacon-Heuer-Poettering shape): the derived
// wrapping key depends on BOTH the RSA-side secret and the ML-KEM shared secret, and is bound to
// the exact pair of ciphertexts via HKDF `info`, so it stays IND-CCA2-secure as long as EITHER
// underlying algorithm remains unbroken, and can never be confused across envelopes/recipients.
//
// Every WrappedUvk-shaped field is self-describing on the wire: a bare base64 string is legacy v1
// (pure RSA-OAEP, see wrapUvkForPublicKey/unwrapUvk in webcrypto.util.ts); a JSON object is v2
// hybrid (this file). wrapUvkForRecipient/unwrapUvkVersioned dispatch between the two so call
// sites never need to branch on version themselves.
import {
  AES_ALGO,
  RSA_ALGO,
  deriveHybridWrapKey,
  fromBase64,
  toBase64,
  unwrapUvk,
  wrapUvkForPublicKey,
} from './webcrypto.util';
import { decapsulateMlKem, encapsulateMlKem } from './ml-kem.util';

const RSA_SECRET_LENGTH_BYTES = 32;
const IV_LENGTH_BYTES = 12;
const HKDF_SALT = new TextEncoder().encode('AuthSystem-VaultHybridUvkWrap-v2-salt');
const HKDF_INFO_PREFIX = new TextEncoder().encode('AuthSystem-VaultHybridUvkWrap-v2');
const ENVELOPE_ALG = 'RSA-OAEP-3072-SHA256+ML-KEM-768/HKDF-SHA256/AES-256-GCM' as const;

export interface WrappedUvkEnvelopeV2 {
  v: 2;
  alg: typeof ENVELOPE_ALG;
  /** base64 RSA-OAEP ciphertext of the 32-byte RSA-side secret. */
  rsaCt: string;
  /** base64 ML-KEM-768 encapsulation ciphertext (1088 bytes). */
  kemCt: string;
  /** base64, 12-byte AES-GCM IV for the DEM step. */
  iv: string;
  /** base64 AES-256-GCM ciphertext+tag of the raw UVK, wrapped under the combined key. */
  demCt: string;
}

/** A bare base64 blob never starts with `{` - JSON does. Cheap, safe version discriminator. */
export function isHybridWrappedUvk(blob: string): boolean {
  return blob.trimStart().startsWith('{');
}

function concatBytes(a: Uint8Array<ArrayBuffer>, b: Uint8Array<ArrayBuffer>): Uint8Array<ArrayBuffer> {
  const result = new Uint8Array(a.length + b.length);
  result.set(a, 0);
  result.set(b, a.length);
  return result;
}

/** Binds the derived wrap key to this exact pair of ciphertexts - prevents any cross-envelope key
 * confusion. HKDF's `info` parameter is capped at 1024 bytes by the Web Crypto spec, well under
 * kemCt+rsaCt's combined ~1472 bytes, so the pair is bound via a collision-resistant SHA-256
 * digest instead of being concatenated in directly. */
async function buildHkdfInfo(kemCt: Uint8Array<ArrayBuffer>, rsaCt: Uint8Array<ArrayBuffer>): Promise<Uint8Array<ArrayBuffer>> {
  const digest = new Uint8Array(await crypto.subtle.digest('SHA-256', concatBytes(kemCt, rsaCt)));
  return concatBytes(HKDF_INFO_PREFIX, digest);
}

export async function wrapUvkHybrid(
  uvk: CryptoKey,
  recipientRsaPublicKey: CryptoKey,
  recipientMlKemPublicKey: Uint8Array<ArrayBuffer>,
): Promise<string> {
  const rsaSecret = crypto.getRandomValues(new Uint8Array(RSA_SECRET_LENGTH_BYTES));
  const rsaCt = new Uint8Array(await crypto.subtle.encrypt(RSA_ALGO, recipientRsaPublicKey, rsaSecret));
  const { ciphertext: kemCt, sharedSecret: kemSecret } = encapsulateMlKem(recipientMlKemPublicKey);

  const ikm = concatBytes(kemSecret, rsaSecret);
  const info = await buildHkdfInfo(kemCt, rsaCt);
  const wrapKey = await deriveHybridWrapKey(ikm, HKDF_SALT, info);

  const iv = crypto.getRandomValues(new Uint8Array(IV_LENGTH_BYTES));
  const uvkBytes = new Uint8Array(await crypto.subtle.exportKey('raw', uvk));
  const demCt = new Uint8Array(await crypto.subtle.encrypt({ name: AES_ALGO, iv }, wrapKey, uvkBytes));

  ikm.fill(0);
  rsaSecret.fill(0);
  kemSecret.fill(0);
  uvkBytes.fill(0);

  const envelope: WrappedUvkEnvelopeV2 = {
    v: 2,
    alg: ENVELOPE_ALG,
    rsaCt: toBase64(rsaCt),
    kemCt: toBase64(kemCt),
    iv: toBase64(iv),
    demCt: toBase64(demCt),
  };
  return JSON.stringify(envelope);
}

export async function unwrapUvkHybrid(
  envelopeJson: string,
  holderRsaPrivateKey: CryptoKey,
  holderMlKemSecretKey: Uint8Array<ArrayBuffer>,
  extractable: boolean,
): Promise<CryptoKey> {
  const envelope = JSON.parse(envelopeJson) as WrappedUvkEnvelopeV2;
  const rsaCt = fromBase64(envelope.rsaCt);
  const kemCt = fromBase64(envelope.kemCt);

  const rsaSecret = new Uint8Array(await crypto.subtle.decrypt(RSA_ALGO, holderRsaPrivateKey, rsaCt));
  const kemSecret = decapsulateMlKem(kemCt, holderMlKemSecretKey);

  const ikm = concatBytes(kemSecret, rsaSecret);
  const info = await buildHkdfInfo(kemCt, rsaCt);
  const wrapKey = await deriveHybridWrapKey(ikm, HKDF_SALT, info);

  const iv = fromBase64(envelope.iv);
  const demCt = fromBase64(envelope.demCt);
  const uvkBytes = new Uint8Array(await crypto.subtle.decrypt({ name: AES_ALGO, iv }, wrapKey, demCt));

  ikm.fill(0);
  rsaSecret.fill(0);
  kemSecret.fill(0);

  try {
    return await crypto.subtle.importKey('raw', uvkBytes, AES_ALGO, extractable, ['encrypt', 'decrypt']);
  } finally {
    uvkBytes.fill(0);
  }
}

/** Wraps for a recipient using v2 hybrid if they have an ML-KEM public key on file, else falls
 * back to legacy v1 RSA-only - lets a v2 owner keep sharing with holders who haven't upgraded. */
export async function wrapUvkForRecipient(
  uvk: CryptoKey,
  recipientRsaPublicKey: CryptoKey,
  recipientMlKemPublicKeyB64: string | null,
): Promise<string> {
  if (recipientMlKemPublicKeyB64 === null) {
    return wrapUvkForPublicKey(uvk, recipientRsaPublicKey);
  }

  return wrapUvkHybrid(uvk, recipientRsaPublicKey, fromBase64(recipientMlKemPublicKeyB64));
}

/** Detects v1 vs v2 and unwraps accordingly. Throws if the blob is v2 but no ML-KEM secret key is available in-session. */
export async function unwrapUvkVersioned(
  blob: string,
  holderRsaPrivateKey: CryptoKey,
  holderMlKemSecretKey: Uint8Array<ArrayBuffer> | null,
  extractable: boolean,
): Promise<CryptoKey> {
  if (!isHybridWrappedUvk(blob)) {
    return unwrapUvk(blob, holderRsaPrivateKey, extractable);
  }

  if (holderMlKemSecretKey === null) {
    throw new Error('This vault key was wrapped with a hybrid PQC scheme, but no ML-KEM key is available to unwrap it.');
  }

  return unwrapUvkHybrid(blob, holderRsaPrivateKey, holderMlKemSecretKey, extractable);
}

// wrapUvkForRecipient/unwrapUvkVersioned are already generic over any AES-256-GCM CryptoKey -
// nothing about their implementation is UVK-specific, only the naming. These aliases give
// per-item-key share call sites a clearer name without touching any existing UVK call site.
export const wrapAesKeyForRecipient = wrapUvkForRecipient;
export const unwrapAesKeyVersioned = unwrapUvkVersioned;
