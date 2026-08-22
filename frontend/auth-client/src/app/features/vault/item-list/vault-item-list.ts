import { Component, OnInit, Signal, computed, inject, linkedSignal, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';

import { decryptItemPayload } from '../../../core/crypto/webcrypto.util';
import { DecryptedVaultItem, VaultItemPayload } from '../../../core/models/vault.models';
import { PaginationService } from '../../../core/services/pagination.service';
import { VaultService } from '../../../core/services/vault.service';
import { VaultSessionService } from '../../../core/services/vault-session.service';
import { relativeTime } from '../../../core/utils/relative-time.util';
import { ItemViewersDialog } from '../../../shared/item-viewers-dialog/item-viewers-dialog';
import { Paginator } from '../../../shared/paginator/paginator';
import { extractErrorMessage } from '../../auth/auth-error.util';
import { ConfirmDialog } from '../../../shared/confirm-dialog/confirm-dialog';
import { SearchQueryBar } from '../../../components/search-query-bar/search-query-bar';

@Component({
  selector: 'app-vault-item-list',
  imports: [
    RouterLink,
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTableModule,
    Paginator,
    SearchQueryBar,
  ],
  templateUrl: './vault-item-list.html',
  styleUrls: ['./vault-item-list.scss'],
})
export class VaultItemList implements OnInit {
  private readonly vaultService = inject(VaultService);
  private readonly vaultSession = inject(VaultSessionService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly pagination = inject(PaginationService);

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);

  readonly items = signal<DecryptedVaultItem[]>([]);
  readonly searchQuery = signal<string | null>(null);
  readonly filteredItems: Signal<DecryptedVaultItem[]> = computed(() => {
    const query = this.searchQuery()?.toLowerCase() ?? '';
    if (!query) return this.items();
    return this.items().filter(item => item.label.toLowerCase().includes(query));
  });
  // Editing the query re-filters the list under the paginator, so send the user back to page 1
  // rather than leaving them stranded past the end of a now-shorter list.
  readonly pageIndex = linkedSignal({ source: this.searchQuery, computation: () => 0 });
  readonly pagedItems = this.pagination.slice(this.filteredItems, this.pageIndex);

  readonly displayedColumns = ['record', 'type', 'actions'];

  ngOnInit(): void {
    this.loadItems();
  }

  updatedLabel(item: DecryptedVaultItem): string {
    return `Updated ${relativeTime(item.updatedAt)}`;
  }

  deleteItem(id: string, event: Event): void {
    event.stopPropagation();
    this.dialog.open(ConfirmDialog, {
      data: {
        title: 'Delete Item',
        message: 'Are you sure you want to delete this item?',
        confirmText: 'Delete',
        cancelText: 'Cancel',
      },
    }).afterClosed().subscribe((confirmed) => {
      if (confirmed) {
        this.vaultService.deleteItem(id).subscribe({
          next: () => this.items.update((items) => items.filter((item) => item.id !== id)),
          error: (err) => this.errorMessage.set(extractErrorMessage(err, 'Could not delete item.')),
        });
      }
    });
  }

  openItem(id: string): void {
    this.router.navigateByUrl(`/vault/items/${id}`);
  }

  viewViewers(itemId: string, event: Event): void {
    event.stopPropagation();
    this.dialog.open(ItemViewersDialog, { data: { itemId } });
  }

  private loadItems(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.vaultService.getItems().subscribe({
      next: async (dtos) => {
        try {
          const decrypted = await Promise.all(
            dtos.map(async (dto) => {
              // Legacy items (not yet migrated to per-item keys) were encrypted directly with the
              // UVK; a migrated item's payload is encrypted with its own key, itself wrapped under the UVK.
              const itemKey = dto.wrappedItemKey ? await this.vaultSession.unwrapOwnedItemKey(dto.wrappedItemKey) : this.vaultSession.uvk;
              const payload = await decryptItemPayload<VaultItemPayload>(itemKey, dto.ciphertextBlob);
              return { ...payload, id: dto.id, createdAt: dto.createdAt, updatedAt: dto.updatedAt };
            }),
          );
          this.items.set(decrypted);
          this.pageIndex.set(0);
        } catch {
          this.errorMessage.set('Could not decrypt your vault items.');
        } finally {
          this.loading.set(false);
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Could not load your vault items.'));
      },
    });
  }
}
