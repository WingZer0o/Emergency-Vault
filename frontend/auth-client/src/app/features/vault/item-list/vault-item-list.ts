import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { decryptItemPayload } from '../../../core/crypto/webcrypto.util';
import { DecryptedVaultItem, VaultItemPayload } from '../../../core/models/vault.models';
import { VaultService } from '../../../core/services/vault.service';
import { VaultSessionService } from '../../../core/services/vault-session.service';
import { extractErrorMessage } from '../../auth/auth-error.util';

@Component({
  selector: 'app-vault-item-list',
  imports: [RouterLink, MatCardModule, MatButtonModule, MatIconModule, MatListModule, MatProgressSpinnerModule],
  templateUrl: './vault-item-list.html',
  styleUrl: './vault-item-list.scss',
})
export class VaultItemList implements OnInit {
  private readonly vaultService = inject(VaultService);
  private readonly vaultSession = inject(VaultSessionService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly items = signal<DecryptedVaultItem[]>([]);

  ngOnInit(): void {
    this.loadItems();
  }

  deleteItem(id: string, event: Event): void {
    event.stopPropagation();
    this.vaultService.deleteItem(id).subscribe({
      next: () => this.items.update((items) => items.filter((item) => item.id !== id)),
      error: (err) => this.errorMessage.set(extractErrorMessage(err, 'Could not delete item.')),
    });
  }

  openItem(id: string): void {
    this.router.navigateByUrl(`/vault/items/${id}`);
  }

  private loadItems(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.vaultService.getItems().subscribe({
      next: async (dtos) => {
        try {
          const decrypted = await Promise.all(
            dtos.map(async (dto) => {
              const payload = await decryptItemPayload<VaultItemPayload>(this.vaultSession.uvk, dto.ciphertextBlob);
              return { ...payload, id: dto.id, createdAt: dto.createdAt, updatedAt: dto.updatedAt };
            }),
          );
          this.items.set(decrypted);
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
