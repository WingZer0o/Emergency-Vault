import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { decryptItemPayload } from '../../core/crypto/webcrypto.util';
import { VaultItemPayload } from '../../core/models/vault.models';
import { VaultService } from '../../core/services/vault.service';
import { extractErrorMessage } from '../../features/auth/auth-error.util';

export interface ItemRevisionsDialogData {
  itemId: string;
  /** This item's current key. For a legacy item (no wrappedItemKey) this is the vault's own UVK. */
  itemKey: CryptoKey;
  /** The vault's own UVK - a revision from before this item was migrated onto its own per-item key
   * was encrypted directly with this, not with itemKey. Only tried when it differs from itemKey. */
  uvk: CryptoKey;
}

interface DecryptedRevision {
  id: string;
  changedByName: string;
  changedByEmail: string;
  changedAt: string;
  payload: VaultItemPayload | null;
  revealed: boolean;
}

@Component({
  selector: 'app-item-revisions-dialog',
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatExpansionModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './item-revisions-dialog.html',
  styleUrl: './item-revisions-dialog.scss',
})
export class ItemRevisionsDialog implements OnInit {
  private readonly data = inject<ItemRevisionsDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<ItemRevisionsDialog>);
  private readonly vaultService = inject(VaultService);

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly revisions = signal<DecryptedRevision[]>([]);

  ngOnInit(): void {
    this.vaultService.getItemRevisions(this.data.itemId).subscribe({
      next: async (dtos) => {
        this.revisions.set(await Promise.all(dtos.map((dto) => this.decryptRevision(dto))));
        this.loading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(extractErrorMessage(err, 'Could not load revision history.'));
        this.loading.set(false);
      },
    });
  }

  toggleReveal(id: string): void {
    this.revisions.update((current) => current.map((r) => (r.id === id ? { ...r, revealed: !r.revealed } : r)));
  }

  close(): void {
    this.dialogRef.close();
  }

  /** A revision recorded before this item was migrated onto its own per-item key was encrypted
   * directly with the vault's UVK, not with the item's current key - try itemKey first (the common
   * case) and fall back to uvk, since AES-GCM decryption fails loudly on the wrong key. */
  private async decryptRevision(dto: {
    id: string;
    previousCiphertextBlob: string;
    changedByName: string;
    changedByEmail: string;
    changedAt: string;
  }): Promise<DecryptedRevision> {
    const keysToTry = this.data.itemKey === this.data.uvk ? [this.data.itemKey] : [this.data.itemKey, this.data.uvk];
    for (const key of keysToTry) {
      try {
        const payload = await decryptItemPayload<VaultItemPayload>(key, dto.previousCiphertextBlob);
        return {
          id: dto.id,
          changedByName: dto.changedByName,
          changedByEmail: dto.changedByEmail,
          changedAt: dto.changedAt,
          payload,
          revealed: false,
        };
      } catch {
        // Wrong key for this revision - try the next one.
      }
    }
    return {
      id: dto.id,
      changedByName: dto.changedByName,
      changedByEmail: dto.changedByEmail,
      changedAt: dto.changedAt,
      payload: null,
      revealed: false,
    };
  }
}
