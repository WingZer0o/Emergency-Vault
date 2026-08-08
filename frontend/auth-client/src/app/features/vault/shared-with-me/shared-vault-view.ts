import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { decryptItemPayload } from '../../../core/crypto/webcrypto.util';
import { DecryptedVaultItem, VaultItemPayload } from '../../../core/models/vault.models';
import { VaultService } from '../../../core/services/vault.service';
import { VaultSessionService } from '../../../core/services/vault-session.service';
import { extractErrorMessage } from '../../auth/auth-error.util';

@Component({
  selector: 'app-shared-vault-view',
  imports: [RouterLink, MatCardModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './shared-vault-view.html',
  styleUrl: './shared-vault-view.scss',
})
export class SharedVaultView implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly vaultService = inject(VaultService);
  private readonly vaultSession = inject(VaultSessionService);

  private readonly ownerUserId = this.route.snapshot.paramMap.get('ownerUserId') ?? '';

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly ownerName = signal('');
  readonly items = signal<DecryptedVaultItem[]>([]);
  readonly revealed = signal<ReadonlySet<string>>(new Set());

  async ngOnInit(): Promise<void> {
    try {
      const shares = await firstValueFrom(this.vaultService.getSharedWithMe());
      const share = shares.find((s) => s.ownerUserId === this.ownerUserId);
      if (!share) {
        this.errorMessage.set("You don't have access to this vault.");
        return;
      }
      this.ownerName.set(share.ownerName);

      if (!share.wrappedUvk) {
        this.router.navigateByUrl('/vault/shared-with-me');
        return;
      }

      const uvk = await this.vaultSession.unwrapSharedUvk(share.wrappedUvk);
      const dtos = await firstValueFrom(this.vaultService.getSharedItems(this.ownerUserId));
      const decrypted = await Promise.all(
        dtos.map(async (dto) => {
          const payload = await decryptItemPayload<VaultItemPayload>(uvk, dto.ciphertextBlob);
          return { ...payload, id: dto.id, createdAt: dto.createdAt, updatedAt: dto.updatedAt };
        }),
      );
      this.items.set(decrypted);
    } catch (err) {
      this.errorMessage.set(extractErrorMessage(err as HttpErrorResponse, 'Could not load this vault.'));
    } finally {
      this.loading.set(false);
    }
  }

  isRevealed(key: string): boolean {
    return this.revealed().has(key);
  }

  toggleReveal(key: string): void {
    this.revealed.update((current) => {
      const next = new Set(current);
      if (next.has(key)) {
        next.delete(key);
      } else {
        next.add(key);
      }
      return next;
    });
  }

  async copyValue(value: string): Promise<void> {
    await navigator.clipboard.writeText(value);
  }
}
