import {
  decryptItemPayload,
  deriveMasterKey,
  encryptItemPayload,
  exportPublicKeySpki,
  generateRecoveryKey,
  generateSaltBase64,
  generateUvk,
  generateVaultKeypair,
  importPublicKeySpki,
  importRecoveryKey,
  unwrapPrivateKey,
  unwrapUvk,
  wrapPrivateKey,
  wrapUvkForPublicKey,
} from './webcrypto.util';

describe('webcrypto.util', () => {
  it('derives the same Master Key for the same password, salt, and iterations', async () => {
    const salt = generateSaltBase64();
    const keyA = await deriveMasterKey('correct horse', salt, 200_000);
    const keyB = await deriveMasterKey('correct horse', salt, 200_000);

    // Non-extractable CryptoKeys can't be compared by value directly - prove equivalence by using
    // one to wrap data and the other to unwrap it.
    const { privateKey } = await generateVaultKeypair();
    const wrapped = await wrapPrivateKey(privateKey, keyA);
    await expect(unwrapPrivateKey(wrapped, keyB, false)).resolves.toBeDefined();
  });

  it('round-trips an item payload through encrypt/decrypt', async () => {
    const uvk = await generateUvk();
    const payload = { label: 'Bank login', fields: [{ name: 'username', value: 'mike' }] };

    const blob = await encryptItemPayload(uvk, payload);
    const decrypted = await decryptItemPayload<typeof payload>(uvk, blob);

    expect(decrypted).toEqual(payload);
  });

  it('round-trips a private key through wrap/unwrap under a password-derived key', async () => {
    const salt = generateSaltBase64();
    const masterKey = await deriveMasterKey('hunter2', salt, 200_000);
    const { privateKey, publicKey } = await generateVaultKeypair();

    const wrapped = await wrapPrivateKey(privateKey, masterKey);
    const unwrapped = await unwrapPrivateKey(wrapped, masterKey, false);

    // Prove it's really usable: unwrap a UVK that was wrapped for the original public key.
    const uvk = await generateUvk();
    const wrappedUvk = await wrapUvkForPublicKey(uvk, publicKey);
    await expect(unwrapUvk(wrappedUvk, unwrapped, false)).resolves.toBeDefined();
  });

  it('throws when unwrapping a private key with the wrong Master Key (bad password)', async () => {
    const { privateKey } = await generateVaultKeypair();
    const correctKey = await deriveMasterKey('right-password', generateSaltBase64(), 200_000);
    const wrongKey = await deriveMasterKey('wrong-password', generateSaltBase64(), 200_000);

    const wrapped = await wrapPrivateKey(privateKey, correctKey);

    await expect(unwrapPrivateKey(wrapped, wrongKey, false)).rejects.toThrow();
  });

  it('round-trips a UVK through wrap/unwrap via RSA-OAEP', async () => {
    const { privateKey, publicKey } = await generateVaultKeypair();
    const uvk = await generateUvk();

    const wrapped = await wrapUvkForPublicKey(uvk, publicKey);
    const unwrapped = await unwrapUvk(wrapped, privateKey, true);

    const message = new TextEncoder().encode('hello vault');
    const iv = crypto.getRandomValues(new Uint8Array(12));
    const ciphertext = await crypto.subtle.encrypt({ name: 'AES-GCM', iv }, uvk, message);
    const plaintext = await crypto.subtle.decrypt({ name: 'AES-GCM', iv }, unwrapped, ciphertext);
    expect(new TextDecoder().decode(plaintext)).toBe('hello vault');
  });

  it('round-trips a public key through export/import as SPKI base64', async () => {
    const { publicKey, privateKey } = await generateVaultKeypair();
    const exported = await exportPublicKeySpki(publicKey);
    const imported = await importPublicKeySpki(exported);

    const uvk = await generateUvk();
    const wrapped = await wrapUvkForPublicKey(uvk, imported);
    await expect(unwrapUvk(wrapped, privateKey, false)).resolves.toBeDefined();
  });

  it('generates a recovery key that re-imports to an equivalent usable key', async () => {
    const { key, displayValue } = await generateRecoveryKey();
    const reimported = await importRecoveryKey(displayValue);

    const { privateKey } = await generateVaultKeypair();
    const wrapped = await wrapPrivateKey(privateKey, key);
    await expect(unwrapPrivateKey(wrapped, reimported, false)).resolves.toBeDefined();
  });
});
