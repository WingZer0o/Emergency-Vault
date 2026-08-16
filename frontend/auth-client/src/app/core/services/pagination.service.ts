import { Injectable, Signal, computed, effect, signal } from '@angular/core';

const STORAGE_KEY = 'page-size';
const DEFAULT_PAGE_SIZE = 10;

/** Sizes offered in the paginator's dropdown. */
export const PAGE_SIZE_OPTIONS: readonly number[] = [5, 10, 25, 50];

/**
 * Holds the user's preferred page size for every paged list in the app. It's a single app-wide
 * preference rather than one per screen - a user who wants 50 rows at a time wants that
 * everywhere - persisted to localStorage the same way ThemeService persists the color scheme.
 *
 * Inject this alongside the Paginator component when a screen needs `slice()` to derive the
 * current page; the component itself reads the size and writes changes back here.
 */
@Injectable({ providedIn: 'root' })
export class PaginationService {
  private readonly pageSizeSig = signal(readStoredPageSize());

  readonly pageSize = this.pageSizeSig.asReadonly();
  readonly pageSizeOptions = PAGE_SIZE_OPTIONS;

  constructor() {
    effect(() => localStorage.setItem(STORAGE_KEY, String(this.pageSizeSig())));
  }

  /** Ignores anything not on the offered list, so a tampered localStorage value can't take effect. */
  setPageSize(size: number): void {
    if (PAGE_SIZE_OPTIONS.includes(size)) {
      this.pageSizeSig.set(size);
    }
  }

  /**
   * Derives the current page of `source` as a computed signal. Recomputes when the list changes,
   * when the page moves, or when the user picks a different page size.
   */
  slice<T>(source: Signal<readonly T[]>, pageIndex: Signal<number>): Signal<T[]> {
    return computed(() => {
      const size = this.pageSizeSig();
      const start = pageIndex() * size;
      return source().slice(start, start + size);
    });
  }
}

function readStoredPageSize(): number {
  const stored = Number(localStorage.getItem(STORAGE_KEY));
  return PAGE_SIZE_OPTIONS.includes(stored) ? stored : DEFAULT_PAGE_SIZE;
}
