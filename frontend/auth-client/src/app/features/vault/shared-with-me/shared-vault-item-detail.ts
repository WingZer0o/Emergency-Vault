import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { decryptFileBytes, decryptItemPayload, encryptItemPayload } from '../../../core/crypto/webcrypto.util';
import { DecryptedVaultItem, VaultItemAttachmentDto, VaultItemDto, VaultItemPayload } from '../../../core/models/vault.models';
import { VaultService } from '../../../core/services/vault.service';
import { VaultSessionService } from '../../../core/services/vault-session.service';
import { relativeTime } from '../../../core/utils/relative-time.util';
import { CopyButton } from '../../../shared/copy-button/copy-button';
import { extractErrorMessage } from '../../auth/auth-error.util';
import { VAULT_ITEM_TYPES } from '../item-create/vault-item-create';

/**
 * Opens exactly one item from a vault shared with the current user. Unlike SharedVaultView (the
 * list), loading this page is what the backend logs as the owner-visible "viewed by" event -
 * separating list-browsing from actually opening an item's contents.
 */
@Component({
  selector: 'app-shared-vault-item-detail',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    CopyButton,
  ],
  templateUrl: './shared-vault-item-detail.html',
  styleUrl: './shared-vault-item-detail.scss',
})
export class SharedVaultItemDetail implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly vaultService = inject(VaultService);
  private readonly vaultSession = inject(VaultSessionService);

  readonly ownerUserId = this.route.snapshot.paramMap.get('ownerUserId') ?? '';
  private readonly itemId = this.route.snapshot.paramMap.get('itemId') ?? '';

  /** Captured once the share is resolved, so it can be unwrapped after a pending code challenge is cleared. */
  private wrappedUvk: string | null = null;
  /** Which kind of share granted access - determines how loadItem() resolves this item's key. */
  private accessPath: 'whole-vault' | 'item-share' | null = null;
  /** This item's key, resolved once in loadItem() - reused to decrypt each attachment on download. */
  private itemKey: CryptoKey | null = null;

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly ownerName = signal('');
  readonly item = signal<DecryptedVaultItem | null>(null);
  readonly revealed = signal<ReadonlySet<string>>(new Set());
  readonly attachments = signal<VaultItemAttachmentDto[]>([]);
  readonly downloadingId = signal<string | null>(null);

  /** Set once the backend says a fresh emailed code is required to view this vault this session. */
  readonly challengeToken = signal<string | null>(null);
  readonly verifying = signal(false);
  readonly codeError = signal<string | null>(null);
  readonly resendMessage = signal<string | null>(null);

  readonly codeForm = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
  });

  /** True only for a CanEdit item-scoped share - a whole-vault share is always read-only. */
  readonly canEdit = signal(false);
  readonly editing = signal(false);
  readonly saving = signal(false);
  readonly itemTypes = VAULT_ITEM_TYPES;

  readonly form = this.fb.nonNullable.group({
    label: ['', [Validators.required]],
    itemType: [VAULT_ITEM_TYPES[0], [Validators.required]],
    fields: this.fb.array([this.createFieldGroup()]),
  });

  get fields(): FormArray {
    return this.form.controls.fields;
  }

  createdUpdatedLabel(): string {
    const current = this.item();
    if (!current) return '';
    return current.createdAt === current.updatedAt
      ? `Created ${relativeTime(current.createdAt)}`
      : `Updated ${relativeTime(current.updatedAt)}`;
  }

  async ngOnInit(): Promise<void> {
    try {
      // Prefer a whole-vault share if the holder has one; otherwise fall back to an item-scoped
      // share for this exact item. Only one of wrappedUvk/accessPath ends up set, depending on which.
      const shares = await firstValueFrom(this.vaultService.getSharedWithMe());
      const wholeVaultShare = shares.find((s) => s.ownerUserId === this.ownerUserId);

      if (wholeVaultShare) {
        this.ownerName.set(wholeVaultShare.ownerName);
        if (!wholeVaultShare.wrappedUvk) {
          // Not verified yet - the "Shared with me" list has the code-entry UI for this.
          this.router.navigateByUrl('/vault/shared-with-me');
          return;
        }
        this.accessPath = 'whole-vault';
        this.wrappedUvk = wholeVaultShare.wrappedUvk;
      } else {
        const itemShares = await firstValueFrom(this.vaultService.getItemSharesHeldByMe());
        const itemShare = itemShares.find((s) => s.ownerUserId === this.ownerUserId && s.itemId === this.itemId);
        if (!itemShare) {
          this.errorMessage.set("You don't have access to this item.");
          this.loading.set(false);
          return;
        }
        if (itemShare.status !== 'Active') {
          // Not verified yet - same as above, the "Shared with me" list has the code-entry UI.
          this.router.navigateByUrl('/vault/shared-with-me');
          return;
        }
        this.ownerName.set(itemShare.ownerName);
        this.accessPath = 'item-share';
        this.canEdit.set(itemShare.canEdit);
      }

      const challenge = await firstValueFrom(this.vaultService.requestSharedVaultAccess(this.ownerUserId));
      if (challenge.requiresCode && challenge.challengeToken) {
        this.challengeToken.set(challenge.challengeToken);
        this.loading.set(false);
        return;
      }

      await this.loadItem();
    } catch (err) {
      this.errorMessage.set(extractErrorMessage(err as HttpErrorResponse, 'Could not load this item.'));
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
      await this.loadItem();
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

  startEditing(): void {
    this.editing.set(true);
  }

  cancelEditing(): void {
    this.editing.set(false);
  }

  addField(): void {
    this.fields.push(this.createFieldGroup());
  }

  removeField(index: number): void {
    this.fields.removeAt(index);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    const raw = this.form.getRawValue();
    const payload: VaultItemPayload = { label: raw.label, itemType: raw.itemType, fields: raw.fields };

    encryptItemPayload(this.itemKey!, payload)
      .then((ciphertextBlob) => firstValueFrom(this.vaultService.updateItem(this.itemId, { ciphertextBlob })))
      .then(() => {
        this.item.update((current) => ({ ...current!, ...payload }));
        this.saving.set(false);
        this.editing.set(false);
      })
      .catch((err) => {
        this.saving.set(false);
        this.errorMessage.set(
          err instanceof HttpErrorResponse ? extractErrorMessage(err, 'Could not save changes.') : 'Could not encrypt item.',
        );
      });
  }

  private createFieldGroup(name = '', value = '') {
    return this.fb.nonNullable.group({
      name: [name, [Validators.required]],
      value: [value, [Validators.required]],
    });
  }

  private async loadItem(): Promise<void> {
    try {
      const dto = await firstValueFrom(this.vaultService.getSharedItem(this.ownerUserId, this.itemId));
      this.itemKey = await this.resolveItemKey(dto);
      const payload = await decryptItemPayload<VaultItemPayload>(this.itemKey, dto.ciphertextBlob);
      this.item.set({ ...payload, id: dto.id, createdAt: dto.createdAt, updatedAt: dto.updatedAt });
      this.form.patchValue({ label: payload.label, itemType: payload.itemType });
      this.fields.clear();
      payload.fields.forEach((field) => this.fields.push(this.createFieldGroup(field.name, field.value)));
      this.loadAttachments();
    } catch (err) {
      this.errorMessage.set(extractErrorMessage(err as HttpErrorResponse, 'Could not load this item.'));
    } finally {
      this.loading.set(false);
    }
  }

  private loadAttachments(): void {
    this.vaultService.getAttachments(this.itemId).subscribe({
      next: (attachments) => this.attachments.set(attachments),
      error: (err) => this.errorMessage.set(extractErrorMessage(err, 'Could not load attachments.')),
    });
  }

  async downloadAttachment(attachment: VaultItemAttachmentDto): Promise<void> {
    this.downloadingId.set(attachment.id);
    this.errorMessage.set(null);
    try {
      const ciphertext = await firstValueFrom(this.vaultService.downloadAttachment(this.itemId, attachment.id));
      const plaintext = await decryptFileBytes(this.itemKey!, attachment.iv, ciphertext);
      const blob = new Blob([plaintext], { type: attachment.contentType });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = attachment.fileName;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch {
      this.errorMessage.set(`Could not download ${attachment.fileName}.`);
    } finally {
      this.downloadingId.set(null);
    }
  }

  private async resolveItemKey(dto: VaultItemDto): Promise<CryptoKey> {
    if (this.accessPath === 'item-share') {
      // Always set here by construction - the server only populates itemShareWrappedKey when
      // access came via an item-scoped share. Wrapped directly for this holder, no UVK involved.
      return this.vaultSession.unwrapItemShareKey(dto.itemShareWrappedKey!);
    }

    // Whole-vault path: unwrap the vault's UVK, then (for a migrated item) this item's own key,
    // wrapped under that same UVK - or use the UVK directly for a still-legacy item.
    const uvk = await this.vaultSession.unwrapSharedUvk(this.wrappedUvk!);
    return dto.wrappedItemKey ? this.vaultSession.unwrapItemKeyViaVaultShare(dto.wrappedItemKey, uvk) : uvk;
  }
}
