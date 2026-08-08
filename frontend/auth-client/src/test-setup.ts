// jsdom (the environment this Vitest runner uses) doesn't implement IndexedDB at all - a known,
// long-standing jsdom limitation, not a Node quirk. vault-key-store.ts depends on it, so without
// this polyfill every spec touching vault persistence would fail with "indexedDB is not defined".
import 'fake-indexeddb/auto';

// Node 22+ defines a global `localStorage`, but it's a non-functional stub unless the process is
// started with `--localstorage-file=<path>` (see https://nodejs.org/api/globals.html#localstorage).
// The Angular/Vitest unit-test runner doesn't set that flag, so `localStorage.setItem` etc. are
// undefined here even though the property exists. Replace it with a working in-memory Storage so
// specs that touch localStorage (e.g. ThemeService) run the same as they would in a real browser.
class MemoryStorage implements Storage {
  private readonly store = new Map<string, string>();

  get length(): number {
    return this.store.size;
  }

  clear(): void {
    this.store.clear();
  }

  getItem(key: string): string | null {
    return this.store.has(key) ? this.store.get(key)! : null;
  }

  key(index: number): string | null {
    return Array.from(this.store.keys())[index] ?? null;
  }

  removeItem(key: string): void {
    this.store.delete(key);
  }

  setItem(key: string, value: string): void {
    this.store.set(key, String(value));
  }
}

function hasWorkingStorage(get: () => Storage): boolean {
  try {
    return typeof get().setItem === 'function';
  } catch {
    return false;
  }
}

if (!hasWorkingStorage(() => localStorage)) {
  Object.defineProperty(globalThis, 'localStorage', {
    value: new MemoryStorage(),
    configurable: true,
    writable: true,
  });
}
