import { Component, OnInit, Signal, computed, inject, linkedSignal, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { debounceTime, firstValueFrom } from 'rxjs';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';

import { decryptItemPayload } from '../../../core/crypto/webcrypto.util';
import { DecryptedVaultItem, VaultItemPayload } from '../../../core/models/vault.models';
import { PaginationService } from '../../../core/services/pagination.service';
import { VaultService } from '../../../core/services/vault.service';
import { VaultSessionService } from '../../../core/services/vault-session.service';
import { relativeTime } from '../../../core/utils/relative-time.util';
import { Paginator } from '../../../shared/paginator/paginator';
import { SearchQueryBar } from '../../../components/search-query-bar/search-query-bar';
import { extractErrorMessage } from '../../auth/auth-error.util';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';

/**
 * Lists the items in a shared vault so a holder can pick one to open. Listing itself is never
 * logged as a "view" - only opening an individual item (SharedVaultItemDetail) is, since that's
 * the point the owner would actually consider their data looked at.
 */
@Component({
  selector: 'app-shared-vault-view',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatTableModule,
    Paginator,
    SearchQueryBar,
  ],
  templateUrl: './shared-vault-view.html',
  styleUrl: './shared-vault-view.scss',
})
export class SharedVaultView implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly vaultService = inject(VaultService);
  private readonly vaultSession = inject(VaultSessionService);
  private readonly pagination = inject(PaginationService);

  readonly ownerUserId = this.route.snapshot.paramMap.get('ownerUserId') ?? '';

  /** Captured once the share is resolved, so it can be unwrapped after a pending code challenge is cleared. */
  private wrappedUvk: string | null = null;

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly ownerName = signal('');
  readonly items = signal<DecryptedVaultItem[]>([]);
  readonly searchQuery = signal<string | null>(null);
  private readonly debouncedSearchQuery = toSignal(
    toObservable(this.searchQuery).pipe(debounceTime(200)),
    { initialValue: null },
  );
  readonly filteredItems: Signal<DecryptedVaultItem[]> = computed(() => {
    const query = this.debouncedSearchQuery()?.toLowerCase() ?? '';
    if (!query) return this.items();
    return this.items().filter((item) => item.label.toLowerCase().includes(query));
  });
  // Editing the query re-filters the list under the paginator, so send the user back to page 1
  // rather than leaving them stranded past the end of a now-shorter list.
  readonly pageIndex = linkedSignal({ source: this.searchQuery, computation: () => 0 });

  readonly pagedItems = this.pagination.slice(this.filteredItems, this.pageIndex);

  /** Set once the backend says a fresh emailed code is required to view this vault this session. */
  readonly challengeToken = signal<string | null>(null);
  readonly verifying = signal(false);
  readonly codeError = signal<string | null>(null);
  readonly resendMessage = signal<string | null>(null);

  readonly codeForm = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
  });

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
      this.wrappedUvk = share.wrappedUvk;

      const challenge = await firstValueFrom(this.vaultService.requestSharedVaultAccess(this.ownerUserId));
      if (challenge.requiresCode && challenge.challengeToken) {
        this.challengeToken.set(challenge.challengeToken);
        this.loading.set(false);
        return;
      }

      await this.loadVaultContents();
    } catch (err) {
      this.errorMessage.set(extractErrorMessage(err as HttpErrorResponse, 'Could not load this vault.'));
      this.loading.set(false);
    }
  }

  async submitCode(): Promise<void> {
    const token = this.challengeToken();
    if (!token || this.codeForm.invalid) {
      this.codeForm.markAllAsTouched();
      return;
    }

    this.verifying.set(true);
    this.codeError.set(null);

    try {
      await firstValueFrom(
        this.vaultService.verifySharedVaultAccess({ challengeToken: token, code: this.codeForm.getRawValue().code }),
      );
      this.challengeToken.set(null);
      this.loading.set(true);
      await this.loadVaultContents();
    } catch (err) {
      this.codeError.set(extractErrorMessage(err as HttpErrorResponse, 'Incorrect code.'));
    } finally {
      this.verifying.set(false);
    }
  }

  resendCode(): void {
    const token = this.challengeToken();
    if (!token) {
      return;
    }

    this.codeError.set(null);
    this.resendMessage.set(null);
    this.vaultService.resendSharedVaultAccessCode({ challengeToken: token }).subscribe({
      next: () => this.resendMessage.set('A new code has been sent.'),
      error: (err) => this.codeError.set(extractErrorMessage(err, 'Could not resend code.')),
    });
  }

  readonly displayedColumns = ['record', 'type'];

  openItem(itemId: string): void {
    this.router.navigateByUrl(`/vault/shared-with-me/${this.ownerUserId}/items/${itemId}`);
  }

  updatedLabel(item: DecryptedVaultItem): string {
    return `Updated ${relativeTime(item.updatedAt)}`;
  }

  private async loadVaultContents(): Promise<void> {
    try {
      const uvk = await this.vaultSession.unwrapSharedUvk(this.wrappedUvk!);
      const dtos = await firstValueFrom(this.vaultService.getSharedItems(this.ownerUserId));
      const decrypted = await Promise.all(
        dtos.map(async (dto) => {
          // Legacy items (not yet migrated to per-item keys) were encrypted directly with the
          // UVK; a migrated item's payload is encrypted with its own key, wrapped under that same UVK.
          const itemKey = dto.wrappedItemKey ? await this.vaultSession.unwrapItemKeyViaVaultShare(dto.wrappedItemKey, uvk) : uvk;
          const payload = await decryptItemPayload<VaultItemPayload>(itemKey, dto.ciphertextBlob);
          return { ...payload, id: dto.id, createdAt: dto.createdAt, updatedAt: dto.updatedAt };
        }),
      );
      this.items.set(decrypted);
      this.pageIndex.set(0);
    } catch (err) {
      this.errorMessage.set(extractErrorMessage(err as HttpErrorResponse, 'Could not load this vault.'));
    } finally {
      this.loading.set(false);
    }
  }
}
