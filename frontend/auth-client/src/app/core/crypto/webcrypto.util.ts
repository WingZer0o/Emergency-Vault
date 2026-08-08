// Pure, stateless WebCrypto helpers backing the zero-knowledge vault. The server only ever stores
// what these functions produce as opaque base64 blobs - it never sees a key or plaintext capable
// of decrypting vault contents. Uses native wrapKey/unwrapKey wherever a CryptoKey is being
// wrapped, so an unwrapped key can stay non-extractable in memory rather than round-tripping
// through raw bytes in JS.

const AES_ALGO = 'AES-GCM';
const RSA_ALGO = { name: 'RSA-OAEP', hash: 'SHA-256' } as const;
const IV_LENGTH_BYTES = 12;
const SALT_LENGTH_BYTES = 16;
const RECOVERY_KEY_LENGTH_BYTES = 32;
const RSA_MODULUS_LENGTH = 3072;

/** PBKDF2-SHA256 iterations for deriving a Master Key from a password, per current OWASP guidance. */
export const DEFAULT_KDF_ITERATIONS = 600_000;

function toBase64(bytes: ArrayBuffer | Uint8Array<ArrayBuffer>): string {
  const arr = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
  let binary = '';
  for (const byte of arr) {
    binary += String.fromCharCode(byte);
  }
  return btoa(binary);
}

function fromBase64(base64: string): Uint8Array<ArrayBuffer> {
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes;
}

function concatBytes(a: Uint8Array<ArrayBuffer>, b: Uint8Array<ArrayBuffer>): Uint8Array<ArrayBuffer> {
  const result = new Uint8Array(a.length + b.length);
  result.set(a, 0);
  result.set(b, a.length);
  return result;
}

/** Splits a base64(IV || ciphertext) blob back into its two parts. */
function splitIvAndCiphertext(blobB64: string): { iv: Uint8Array<ArrayBuffer>; ciphertext: Uint8Array<ArrayBuffer> } {
  const combined = fromBase64(blobB64);
  return { iv: combined.slice(0, IV_LENGTH_BYTES), ciphertext: combined.slice(IV_LENGTH_BYTES) };
}

export function generateSaltBase64(): string {
  return toBase64(crypto.getRandomValues(new Uint8Array(SALT_LENGTH_BYTES)));
}

/** Derives the password-based Master Key. Never stored, never extractable - exists only transiently to unwrap the private key. */
export async function deriveMasterKey(password: string, saltB64: string, iterations: number): Promise<CryptoKey> {
  const keyMaterial = await crypto.subtle.importKey(
    'raw',
    new TextEncoder().encode(password),
    'PBKDF2',
    false,
    ['deriveKey'],
  );

  return crypto.subtle.deriveKey(
    { name: 'PBKDF2', salt: fromBase64(saltB64), iterations, hash: 'SHA-256' },
    keyMaterial,
    { name: AES_ALGO, length: 256 },
    false,
    ['wrapKey', 'unwrapKey'],
  );
}

/** Generates the user's RSA-OAEP keypair. Private key is extractable this one time only, so it can be wrapped for storage. */
export async function generateVaultKeypair(): Promise<CryptoKeyPair> {
  return (await crypto.subtle.generateKey(
    { ...RSA_ALGO, modulusLength: RSA_MODULUS_LENGTH, publicExponent: new Uint8Array([1, 0, 1]) },
    true,
    ['wrapKey', 'unwrapKey'],
  )) as CryptoKeyPair;
}

/** Generates the User Vault Key. Stays extractable for the whole unlocked session, since granting a new share requires wrapping it again. */
export async function generateUvk(): Promise<CryptoKey> {
  return crypto.subtle.generateKey({ name: AES_ALGO, length: 256 }, true, ['encrypt', 'decrypt']);
}

export async function wrapPrivateKey(privateKey: CryptoKey, wrappingKey: CryptoKey): Promise<string> {
  const iv = crypto.getRandomValues(new Uint8Array(IV_LENGTH_BYTES));
  const wrapped = await crypto.subtle.wrapKey('pkcs8', privateKey, wrappingKey, { name: AES_ALGO, iv });
  return toBase64(concatBytes(iv, new Uint8Array(wrapped)));
}

/** A wrong wrappingKey (e.g. bad password) throws here via a GCM auth-tag mismatch - this doubles as the password check. */
export async function unwrapPrivateKey(blobB64: string, wrappingKey: CryptoKey, extractable: boolean): Promise<CryptoKey> {
  const { iv, ciphertext } = splitIvAndCiphertext(blobB64);
  return crypto.subtle.unwrapKey('pkcs8', ciphertext, wrappingKey, { name: AES_ALGO, iv }, RSA_ALGO, extractable, [
    'unwrapKey',
  ]);
}

/** RSA-OAEP wrapping has no IV of its own - the output is pure ciphertext. */
export async function wrapUvkForPublicKey(uvk: CryptoKey, publicKey: CryptoKey): Promise<string> {
  const wrapped = await crypto.subtle.wrapKey('raw', uvk, publicKey, RSA_ALGO);
  return toBase64(wrapped);
}

export async function unwrapUvk(blobB64: string, privateKey: CryptoKey, extractable: boolean): Promise<CryptoKey> {
  return crypto.subtle.unwrapKey(
    'raw',
    fromBase64(blobB64),
    privateKey,
    RSA_ALGO,
    { name: AES_ALGO, length: 256 },
    extractable,
    ['encrypt', 'decrypt'],
  );
}

export async function encryptItemPayload(uvk: CryptoKey, payload: unknown): Promise<string> {
  const iv = crypto.getRandomValues(new Uint8Array(IV_LENGTH_BYTES));
  const plaintext = new TextEncoder().encode(JSON.stringify(payload));
  const ciphertext = await crypto.subtle.encrypt({ name: AES_ALGO, iv }, uvk, plaintext);
  return toBase64(concatBytes(iv, new Uint8Array(ciphertext)));
}

export async function decryptItemPayload<T>(uvk: CryptoKey, blobB64: string): Promise<T> {
  const { iv, ciphertext } = splitIvAndCiphertext(blobB64);
  const plaintext = await crypto.subtle.decrypt({ name: AES_ALGO, iv }, uvk, ciphertext);
  return JSON.parse(new TextDecoder().decode(plaintext)) as T;
}

export async function exportPublicKeySpki(publicKey: CryptoKey): Promise<string> {
  return toBase64(await crypto.subtle.exportKey('spki', publicKey));
}

export async function importPublicKeySpki(base64: string): Promise<CryptoKey> {
  return crypto.subtle.importKey('spki', fromBase64(base64), RSA_ALGO, false, ['wrapKey']);
}

export interface GeneratedRecoveryKey {
  key: CryptoKey;
  /** Grouped hex string shown to the user once at setup - never stored anywhere. */
  displayValue: string;
}

export async function generateRecoveryKey(): Promise<GeneratedRecoveryKey> {
  const raw = crypto.getRandomValues(new Uint8Array(RECOVERY_KEY_LENGTH_BYTES));
  const key = await crypto.subtle.importKey('raw', raw, AES_ALGO, false, ['wrapKey', 'unwrapKey']);
  return { key, displayValue: formatRecoveryKeyForDisplay(raw) };
}

/** Re-imports a recovery key the user typed back in, in the same grouped-hex format `generateRecoveryKey` displayed. */
export async function importRecoveryKey(displayValue: string): Promise<CryptoKey> {
  return crypto.subtle.importKey('raw', parseRecoveryKeyFromDisplay(displayValue), AES_ALGO, false, [
    'wrapKey',
    'unwrapKey',
  ]);
}

function formatRecoveryKeyForDisplay(raw: Uint8Array<ArrayBuffer>): string {
  const hex = Array.from(raw)
    .map((b) => b.toString(16).padStart(2, '0'))
    .join('')
    .toUpperCase();
  return hex.match(/.{1,4}/g)!.join('-');
}

function parseRecoveryKeyFromDisplay(displayValue: string): Uint8Array<ArrayBuffer> {
  const hex = displayValue.replace(/[^0-9a-fA-F]/g, '');
  if (hex.length !== RECOVERY_KEY_LENGTH_BYTES * 2) {
    throw new Error('Invalid recovery key format.');
  }

  const bytes = new Uint8Array(RECOVERY_KEY_LENGTH_BYTES);
  for (let i = 0; i < RECOVERY_KEY_LENGTH_BYTES; i++) {
    bytes[i] = parseInt(hex.substring(i * 2, i * 2 + 2), 16);
  }
  return bytes;
}
