import { Component, computed, effect, inject, input, model } from '@angular/core';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';

import { PaginationService } from '../../core/services/pagination.service';

/**
 * App-wide paginator. Wraps mat-paginator so every paged list shares one look, one page-size
 * preference (see PaginationService), and the same "hide it while everything fits on one page"
 * behaviour.
 *
 * Pair it with `PaginationService.slice()` to derive the visible rows:
 *
 * ```ts
 * readonly pageIndex = signal(0);
 * readonly pagedItems = this.pagination.slice(this.items, this.pageIndex);
 * ```
 * ```html
 * <app-paginator [length]="items().length" [(pageIndex)]="pageIndex" />
 * ```
 */
@Component({
  selector: 'app-paginator',
  imports: [MatPaginatorModule],
  templateUrl: './paginator.html',
  styleUrl: './paginator.scss',
})
export class Paginator {
  private readonly pagination = inject(PaginationService);

  /** Total number of items being paged through - not the length of the current page. */
  readonly length = input.required<number>();
  readonly pageIndex = model(0);
  /** Set false to drop the page-size dropdown on a screen where the choice would just be noise. */
  readonly showPageSize = input(true);

  readonly pageSize = this.pagination.pageSize;
  readonly pageSizeOptions = this.pagination.pageSizeOptions;

  constructor() {
    // Keeps the page in range when the list shrinks under us (deleting the only row on the last
    // page) or when a bigger page size collapses the page count. Without this the caller's slice
    // would come back empty and the list would look mysteriously blank.
    effect(() => {
      const lastIndex = Math.max(0, Math.ceil(this.length() / this.pageSize()) - 1);
      if (this.pageIndex() > lastIndex) {
        this.pageIndex.set(lastIndex);
      }
    });
  }

  onPage(event: PageEvent): void {
    // Page-size changes are a user preference, so they persist app-wide rather than staying local
    // to this instance.
    if (event.pageSize !== this.pageSize()) {
      this.pagination.setPageSize(event.pageSize);
    }
    this.pageIndex.set(event.pageIndex);
  }
}
