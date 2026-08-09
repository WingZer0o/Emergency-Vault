// Thin wrapper around @noble/post-quantum's ML-KEM (FIPS 203) implementation - isolates the one
// non-native crypto dependency in this app from webcrypto.util.ts, which otherwise uses nothing
// but the browser's built-in SubtleCrypto. Native SubtleCrypto doesn't implement ML-KEM yet (still
// a WICG draft, not shipped in any browser), so this is a pure-JS/TS implementation instead - see
// hybrid-kem.util.ts for why the secret key this produces can't get SubtleCrypto's
// non-extractable-key protection the way the RSA private key does.
//
// ML-KEM-768 (Category 3, ~192-bit) is used rather than 512 or 1024: it matches-or-exceeds the
// classical ~128-bit level of the RSA-3072 side of the hybrid scheme, and is the parameter set
// real hybrid deployments have converged on (Chrome/TLS 1.3 X25519MLKEM768, OpenSSH
// mlkem768x25519-sha256). Sizes: public key 1184B, secret key 2400B, ciphertext 1088B, shared
// secret 32B.
import { ml_kem768 } from '@noble/post-quantum/ml-kem.js';

export interface MlKemKeyPair {
  publicKey: Uint8Array<ArrayBuffer>;
  secretKey: Uint8Array<ArrayBuffer>;
}

export interface MlKemEncapsulation {
  ciphertext: Uint8Array<ArrayBuffer>;
  sharedSecret: Uint8Array<ArrayBuffer>;
}

// @noble/post-quantum's TS types resolve its Uint8Array outputs to Uint8Array<ArrayBufferLike>
// (a union that also covers SharedArrayBuffer), which the rest of this codebase's
// Uint8Array<ArrayBuffer> convention can't accept directly - `new Uint8Array(bytes)` copies into a
// freshly allocated, concretely-ArrayBuffer-backed array, which resolves the type at negligible
// cost for these small (<=2.4KB) key sizes.
function toOwnedBytes(bytes: Uint8Array): Uint8Array<ArrayBuffer> {
  return new Uint8Array(bytes);
}

export function generateMlKemKeypair(): MlKemKeyPair {
  const { publicKey, secretKey } = ml_kem768.keygen();
  return { publicKey: toOwnedBytes(publicKey), secretKey: toOwnedBytes(secretKey) };
}

export function encapsulateMlKem(publicKey: Uint8Array): MlKemEncapsulation {
  const { cipherText, sharedSecret } = ml_kem768.encapsulate(publicKey);
  return { ciphertext: toOwnedBytes(cipherText), sharedSecret: toOwnedBytes(sharedSecret) };
}

export function decapsulateMlKem(ciphertext: Uint8Array, secretKey: Uint8Array): Uint8Array<ArrayBuffer> {
  return toOwnedBytes(ml_kem768.decapsulate(ciphertext, secretKey));
}
