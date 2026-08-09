import { IDBFactory } from 'fake-indexeddb';

import { generateUvk, generateVaultKeypair } from './webcrypto.util';
import { clearVaultKeys, loadVaultKeys, saveVaultKeys } from './vault-key-store';

describe('vault-key-store', () => {
  beforeEach(() => {
    // Fresh in-memory database per test - fake-indexeddb/auto installs one shared instance.
    globalThis.indexedDB = new IDBFactory();
  });

  it('returns null when nothing has been saved', async () => {
    await expect(loadVaultKeys()).resolves.toBeNull();
  });

  it('round-trips a private key, UVK, and a non-null mlKemSecretKey', async () => {
    const { privateKey } = await generateVaultKeypair();
    const uvk = await generateUvk();
    const mlKemSecretKey = crypto.getRandomValues(new Uint8Array(2400));

    await saveVaultKeys({ privateKey, uvk, mlKemSecretKey });
    const loaded = await loadVaultKeys();

    expect(loaded).not.toBeNull();
    expect(loaded!.privateKey.type).toBe('private');
    expect(loaded!.uvk.type).toBe('secret');
    // Array.from(...) sidesteps a Vitest/fake-indexeddb quirk comparing typed arrays that round-
    // tripped through structured clone by constructor identity rather than byte content.
    expect(Array.from(loaded!.mlKemSecretKey!)).toEqual(Array.from(mlKemSecretKey));
  });

  it('round-trips a null mlKemSecretKey (legacy, non-upgraded vault)', async () => {
    const { privateKey } = await generateVaultKeypair();
    const uvk = await generateUvk();

    await saveVaultKeys({ privateKey, uvk, mlKemSecretKey: null });
    const loaded = await loadVaultKeys();

    expect(loaded!.mlKemSecretKey).toBeNull();
  });

  it('clears a saved session', async () => {
    const { privateKey } = await generateVaultKeypair();
    const uvk = await generateUvk();
    await saveVaultKeys({ privateKey, uvk, mlKemSecretKey: null });

    await clearVaultKeys();

    await expect(loadVaultKeys()).resolves.toBeNull();
  });
});
