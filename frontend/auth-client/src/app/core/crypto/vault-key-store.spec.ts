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

  it('round-trips a private key and UVK', async () => {
    const { privateKey } = await generateVaultKeypair();
    const uvk = await generateUvk();

    await saveVaultKeys({ privateKey, uvk });
    const loaded = await loadVaultKeys();

    expect(loaded).not.toBeNull();
    expect(loaded!.privateKey.type).toBe('private');
    expect(loaded!.uvk.type).toBe('secret');
  });

  it('clears a saved session', async () => {
    const { privateKey } = await generateVaultKeypair();
    const uvk = await generateUvk();
    await saveVaultKeys({ privateKey, uvk });

    await clearVaultKeys();

    await expect(loadVaultKeys()).resolves.toBeNull();
  });
});
