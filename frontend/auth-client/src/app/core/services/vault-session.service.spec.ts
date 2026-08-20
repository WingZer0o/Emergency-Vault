import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { IDBFactory } from 'fake-indexeddb';

import { environment } from '../../../environments/environment';
import {
  deriveMasterKey,
  generateRecoveryKey,
  generateSaltBase64,
  generateUvk,
  generateVaultKeypair,
  wrapPrivateKey,
  wrapUvkForPublicKey,
} from '../crypto/webcrypto.util';
import { VAULT_IDLE_TIMEOUT_MS, VaultSessionService } from './vault-session.service';

const KDF_ITERATIONS = 200_000;
const baseUrl = `/api/vault`;

/**
 * Unlike the unlock flows, setupVault does real RSA-3072 key generation before its HTTP call, so
 * the request isn't dispatched on the same microtask tick - poll instead of assuming it's already
 * queued the instant setupVault() returns its promise.
 */
async function waitForRequest(httpMock: HttpTestingController, url: string, timeoutMs = 5000) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const [match] = httpMock.match(url);
    if (match) {
      return match;
    }
    await new Promise((resolve) => setTimeout(resolve, 5));
  }
  throw new Error(`Timed out waiting for a request to ${url}`);
}

async function buildUnlockBundle(password: string) {
  const { publicKey, privateKey } = await generateVaultKeypair();
  const uvk = await generateUvk();
  const kdfSalt = generateSaltBase64();
  const masterKey = await deriveMasterKey(password, kdfSalt, KDF_ITERATIONS);
  const recovery = await generateRecoveryKey();

  return {
    bundle: {
      kdfSalt,
      kdfIterations: KDF_ITERATIONS,
      privateKeyWrappedByMasterKey: await wrapPrivateKey(privateKey, masterKey),
      privateKeyWrappedByRecoveryKey: await wrapPrivateKey(privateKey, recovery.key),
      wrappedUvkSelf: await wrapUvkForPublicKey(uvk, publicKey),
    },
    recoveryKeyDisplay: recovery.displayValue,
  };
}

function configureTestBed() {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting()],
  });
  return {
    service: TestBed.inject(VaultSessionService),
    httpMock: TestBed.inject(HttpTestingController),
  };
}

describe('VaultSessionService', () => {
  let service: VaultSessionService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    // Fresh in-memory IndexedDB per test - fake-indexeddb/auto installs one shared instance.
    globalThis.indexedDB = new IDBFactory();
    ({ service, httpMock } = configureTestBed());
  });

  afterEach(() => httpMock.verify());

  it('starts locked', () => {
    expect(service.isUnlocked()).toBe(false);
  });

  it('unlocks with the correct password', async () => {
    const { bundle } = await buildUnlockBundle('correct horse');

    const unlockPromise = service.unlockWithPassword('correct horse');
    httpMock.expectOne(`${baseUrl}/unlock-bundle`).flush(bundle);
    await unlockPromise;

    expect(service.isUnlocked()).toBe(true);
  });

  it('rejects and stays locked with the wrong password', async () => {
    const { bundle } = await buildUnlockBundle('correct horse');

    const unlockPromise = service.unlockWithPassword('wrong password');
    httpMock.expectOne(`${baseUrl}/unlock-bundle`).flush(bundle);

    await expect(unlockPromise).rejects.toThrow();
    expect(service.isUnlocked()).toBe(false);
  });

  it('unlocks with the recovery key', async () => {
    const { bundle, recoveryKeyDisplay } = await buildUnlockBundle('correct horse');

    const unlockPromise = service.unlockWithRecoveryKey(recoveryKeyDisplay);
    httpMock.expectOne(`${baseUrl}/unlock-bundle`).flush(bundle);
    await unlockPromise;

    expect(service.isUnlocked()).toBe(true);
  });

  it('lock() clears the unlocked state', async () => {
    const { bundle } = await buildUnlockBundle('correct horse');

    const unlockPromise = service.unlockWithPassword('correct horse');
    httpMock.expectOne(`${baseUrl}/unlock-bundle`).flush(bundle);
    await unlockPromise;
    expect(service.isUnlocked()).toBe(true);

    service.lock();

    expect(service.isUnlocked()).toBe(false);
  });

  it('setupVault posts the wrapped bundle and unlocks the session', async () => {
    const setupPromise = service.setupVault('a new password');
    const req = await waitForRequest(httpMock, `${baseUrl}/setup`);
    expect(req.request.body.kdfIterations).toBeGreaterThan(0);
    expect(typeof req.request.body.publicKey).toBe('string');
    req.flush({ message: 'ok' });

    const recoveryKeyDisplay = await setupPromise;

    expect(service.isUnlocked()).toBe(true);
    expect(recoveryKeyDisplay).toMatch(/^[0-9A-F-]+$/);
  });

  it('persists an unlocked session to IndexedDB so a fresh instance can restore it (simulating a refresh)', async () => {
    const { bundle } = await buildUnlockBundle('correct horse');
    const unlockPromise = service.unlockWithPassword('correct horse');
    httpMock.expectOne(`${baseUrl}/unlock-bundle`).flush(bundle);
    await unlockPromise;
    expect(service.isUnlocked()).toBe(true);

    // A fresh injector/service instance, same underlying IndexedDB - simulates a page refresh.
    TestBed.resetTestingModule();
    ({ service, httpMock } = configureTestBed());

    expect(service.isUnlocked()).toBe(false);
    await service.restoreFromStorage();
    expect(service.isUnlocked()).toBe(true);
  });

  it('lock() clears the persisted session too, not just in-memory state', async () => {
    const { bundle } = await buildUnlockBundle('correct horse');
    const unlockPromise = service.unlockWithPassword('correct horse');
    httpMock.expectOne(`${baseUrl}/unlock-bundle`).flush(bundle);
    await unlockPromise;

    service.lock();

    TestBed.resetTestingModule();
    ({ service, httpMock } = configureTestBed());
    await service.restoreFromStorage();

    expect(service.isUnlocked()).toBe(false);
  });

  it('auto-locks after the idle timeout with no activity', async () => {
    // Scoped to just setTimeout/clearTimeout - the default fake-timer set also fakes setImmediate,
    // which Node's WebCrypto internals (e.g. RSA key generation) rely on, causing every await
    // below to hang forever.
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    try {
      const { bundle } = await buildUnlockBundle('correct horse');
      const unlockPromise = service.unlockWithPassword('correct horse');
      httpMock.expectOne(`${baseUrl}/unlock-bundle`).flush(bundle);
      await unlockPromise;
      TestBed.flushEffects();
      expect(service.isUnlocked()).toBe(true);

      await vi.advanceTimersByTimeAsync(VAULT_IDLE_TIMEOUT_MS);

      expect(service.isUnlocked()).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });

  it('activity resets the idle timer so it does not auto-lock', async () => {
    // Scoped to just setTimeout/clearTimeout - the default fake-timer set also fakes setImmediate,
    // which Node's WebCrypto internals (e.g. RSA key generation) rely on, causing every await
    // below to hang forever.
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    try {
      const { bundle } = await buildUnlockBundle('correct horse');
      const unlockPromise = service.unlockWithPassword('correct horse');
      httpMock.expectOne(`${baseUrl}/unlock-bundle`).flush(bundle);
      await unlockPromise;
      TestBed.flushEffects();

      // Halfway through the timeout, simulate activity - the timer should restart from there.
      await vi.advanceTimersByTimeAsync(VAULT_IDLE_TIMEOUT_MS / 2);
      document.dispatchEvent(new Event('keydown'));
      await vi.advanceTimersByTimeAsync(VAULT_IDLE_TIMEOUT_MS / 2);

      expect(service.isUnlocked()).toBe(true);
    } finally {
      vi.useRealTimers();
    }
  });
});
