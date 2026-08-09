import { generateUvk, generateVaultKeypair, toBase64, wrapUvkForPublicKey } from './webcrypto.util';
import { generateMlKemKeypair } from './ml-kem.util';
import {
  isHybridWrappedUvk,
  unwrapUvkHybrid,
  unwrapUvkVersioned,
  wrapUvkForRecipient,
  wrapUvkHybrid,
  type WrappedUvkEnvelopeV2,
} from './hybrid-kem.util';

async function exportRawUvkBytes(uvk: CryptoKey): Promise<Uint8Array> {
  return new Uint8Array(await crypto.subtle.exportKey('raw', uvk));
}

describe('hybrid-kem.util', () => {
  it('round-trips a UVK through wrapUvkHybrid/unwrapUvkHybrid', async () => {
    const { publicKey, privateKey } = await generateVaultKeypair();
    const mlKem = generateMlKemKeypair();
    const uvk = await generateUvk();

    const envelope = await wrapUvkHybrid(uvk, publicKey, mlKem.publicKey);
    const unwrapped = await unwrapUvkHybrid(envelope, privateKey, mlKem.secretKey, true);

    expect(await exportRawUvkBytes(unwrapped)).toEqual(await exportRawUvkBytes(uvk));
  });

  it('produces a v2 JSON envelope, not bare base64', async () => {
    const { publicKey } = await generateVaultKeypair();
    const mlKem = generateMlKemKeypair();
    const uvk = await generateUvk();

    const envelope = await wrapUvkHybrid(uvk, publicKey, mlKem.publicKey);
    const parsed = JSON.parse(envelope) as WrappedUvkEnvelopeV2;

    expect(isHybridWrappedUvk(envelope)).toBe(true);
    expect(parsed.v).toBe(2);
    expect(parsed).toEqual(
      expect.objectContaining({ rsaCt: expect.any(String), kemCt: expect.any(String), iv: expect.any(String), demCt: expect.any(String) }),
    );
  });

  it('wrapUvkForRecipient falls back to legacy v1 RSA-only when the recipient has no ML-KEM key', async () => {
    const { publicKey } = await generateVaultKeypair();
    const uvk = await generateUvk();

    const blob = await wrapUvkForRecipient(uvk, publicKey, null);

    expect(isHybridWrappedUvk(blob)).toBe(false);
  });

  it('wrapUvkForRecipient produces a v2 hybrid envelope when the recipient has an ML-KEM key', async () => {
    const { publicKey } = await generateVaultKeypair();
    const mlKem = generateMlKemKeypair();
    const uvk = await generateUvk();

    const blob = await wrapUvkForRecipient(uvk, publicKey, toBase64(mlKem.publicKey));

    expect(isHybridWrappedUvk(blob)).toBe(true);
  });

  it('unwrapUvkVersioned round-trips a legacy v1 blob', async () => {
    const { publicKey, privateKey } = await generateVaultKeypair();
    const uvk = await generateUvk();
    const blob = await wrapUvkForPublicKey(uvk, publicKey);

    const unwrapped = await unwrapUvkVersioned(blob, privateKey, null, true);

    expect(await exportRawUvkBytes(unwrapped)).toEqual(await exportRawUvkBytes(uvk));
  });

  it('unwrapUvkVersioned round-trips a v2 hybrid blob', async () => {
    const { publicKey, privateKey } = await generateVaultKeypair();
    const mlKem = generateMlKemKeypair();
    const uvk = await generateUvk();
    const blob = await wrapUvkHybrid(uvk, publicKey, mlKem.publicKey);

    const unwrapped = await unwrapUvkVersioned(blob, privateKey, mlKem.secretKey, true);

    expect(await exportRawUvkBytes(unwrapped)).toEqual(await exportRawUvkBytes(uvk));
  });

  it('unwrapUvkVersioned throws when given a v2 envelope but no ML-KEM secret key is available', async () => {
    const { publicKey, privateKey } = await generateVaultKeypair();
    const mlKem = generateMlKemKeypair();
    const uvk = await generateUvk();
    const blob = await wrapUvkHybrid(uvk, publicKey, mlKem.publicKey);

    await expect(unwrapUvkVersioned(blob, privateKey, null, true)).rejects.toThrow();
  });

  it('rejects a tampered RSA ciphertext', async () => {
    const { publicKey, privateKey } = await generateVaultKeypair();
    const mlKem = generateMlKemKeypair();
    const uvk = await generateUvk();
    const envelope = JSON.parse(await wrapUvkHybrid(uvk, publicKey, mlKem.publicKey)) as WrappedUvkEnvelopeV2;
    envelope.rsaCt = envelope.rsaCt.slice(0, -4) + (envelope.rsaCt.slice(-4) === 'AAAA' ? 'BBBB' : 'AAAA');

    await expect(unwrapUvkHybrid(JSON.stringify(envelope), privateKey, mlKem.secretKey, true)).rejects.toThrow();
  });

  it('rejects a tampered ML-KEM ciphertext', async () => {
    const { publicKey, privateKey } = await generateVaultKeypair();
    const mlKem = generateMlKemKeypair();
    const uvk = await generateUvk();
    const envelope = JSON.parse(await wrapUvkHybrid(uvk, publicKey, mlKem.publicKey)) as WrappedUvkEnvelopeV2;
    envelope.kemCt = envelope.kemCt.slice(0, -4) + (envelope.kemCt.slice(-4) === 'AAAA' ? 'BBBB' : 'AAAA');

    // Implicit rejection means this decapsulates to a *different* (still 32-byte) shared secret
    // rather than throwing - the resulting wrap key is wrong, so the AES-GCM auth tag on demCt is
    // what actually fails.
    await expect(unwrapUvkHybrid(JSON.stringify(envelope), privateKey, mlKem.secretKey, true)).rejects.toThrow();
  });

  it('rejects a tampered DEM ciphertext', async () => {
    const { publicKey, privateKey } = await generateVaultKeypair();
    const mlKem = generateMlKemKeypair();
    const uvk = await generateUvk();
    const envelope = JSON.parse(await wrapUvkHybrid(uvk, publicKey, mlKem.publicKey)) as WrappedUvkEnvelopeV2;
    envelope.demCt = envelope.demCt.slice(0, -4) + (envelope.demCt.slice(-4) === 'AAAA' ? 'BBBB' : 'AAAA');

    await expect(unwrapUvkHybrid(JSON.stringify(envelope), privateKey, mlKem.secretKey, true)).rejects.toThrow();
  });

  it('wrapping the same UVK twice for the same recipient produces different ciphertexts each time', async () => {
    const { publicKey } = await generateVaultKeypair();
    const mlKem = generateMlKemKeypair();
    const uvk = await generateUvk();

    const first = JSON.parse(await wrapUvkHybrid(uvk, publicKey, mlKem.publicKey)) as WrappedUvkEnvelopeV2;
    const second = JSON.parse(await wrapUvkHybrid(uvk, publicKey, mlKem.publicKey)) as WrappedUvkEnvelopeV2;

    expect(first.iv).not.toEqual(second.iv);
    expect(first.demCt).not.toEqual(second.demCt);
    expect(first.rsaCt).not.toEqual(second.rsaCt);
  });
});
