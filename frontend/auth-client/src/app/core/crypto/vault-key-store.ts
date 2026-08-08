// Persists the vault's unwrapped session keys (private key + UVK) across page refreshes.
// IndexedDB (unlike localStorage/sessionStorage, which only store strings) supports storing
// CryptoKey objects directly via the structured clone algorithm, and critically preserves the
// "non-extractable" flag - so even with this persistence, the raw key bytes are never exposed to
// JS, in storage or otherwise. Only crypto.subtle operations using the key remain possible.
// Cleared explicitly on lock()/logout - see VaultSessionService.

const DB_NAME = 'vault-keys';
const STORE_NAME = 'session';
const RECORD_KEY = 'current';
const DB_VERSION = 1;

export interface StoredVaultKeys {
  privateKey: CryptoKey;
  uvk: CryptoKey;
}

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);
    request.onupgradeneeded = () => {
      request.result.createObjectStore(STORE_NAME);
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

export async function saveVaultKeys(keys: StoredVaultKeys): Promise<void> {
  const db = await openDb();
  try {
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction(STORE_NAME, 'readwrite');
      tx.objectStore(STORE_NAME).put(keys, RECORD_KEY);
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
  } finally {
    db.close();
  }
}

export async function loadVaultKeys(): Promise<StoredVaultKeys | null> {
  const db = await openDb();
  try {
    return await new Promise<StoredVaultKeys | null>((resolve, reject) => {
      const tx = db.transaction(STORE_NAME, 'readonly');
      const req = tx.objectStore(STORE_NAME).get(RECORD_KEY);
      req.onsuccess = () => resolve((req.result as StoredVaultKeys | undefined) ?? null);
      req.onerror = () => reject(req.error);
    });
  } finally {
    db.close();
  }
}

export async function clearVaultKeys(): Promise<void> {
  const db = await openDb();
  try {
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction(STORE_NAME, 'readwrite');
      tx.objectStore(STORE_NAME).delete(RECORD_KEY);
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
  } finally {
    db.close();
  }
}
