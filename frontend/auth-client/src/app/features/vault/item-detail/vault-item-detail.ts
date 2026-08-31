import { Component, OnInit, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpErrorResponse, HttpEventType, HttpResponse } from '@angular/common/http';
import { filter, firstValueFrom } from 'rxjs';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import {
  decryptFileBytes,
  decryptItemPayload,
  encryptFileBytes,
  encryptItemPayload,
  generateItemKey,
  importPublicKeySpki,
  wrapRawBytes,
} from '../../../core/crypto/webcrypto.util';
import { wrapAesKeyForRecipient } from '../../../core/crypto/hybrid-kem.util';
import { VaultItemAttachmentDto, VaultItemPayload, VaultShareDto } from '../../../core/models/vault.models';
import { VaultService } from '../../../core/services/vault.service';
import { VaultSessionService } from '../../../core/services/vault-session.service';
import { relativeTime } from '../../../core/utils/relative-time.util';
import { CopyButton } from '../../../shared/copy-button/copy-button';
import { ItemRevisionsDialog } from '../../../shared/item-revisions-dialog/item-revisions-dialog';
import { ShareItemDialog, ShareItemDialogResult } from '../../../shared/share-item-dialog/share-item-dialog';
import { extractErrorMessage } from '../../auth/auth-error.util';
import { ALLOWED_ATTACHMENT_EXTENSIONS, VAULT_ITEM_TYPES } from '../item-create/vault-item-create';
import { ConfirmDialog } from '../../../shared/confirm-dialog/confirm-dialog';

@Component({
  selector: 'app-vault-item-detail',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatListModule,
    MatProgressSpinnerModule,
    CopyButton
],
  templateUrl: './vault-item-detail.html',
  styleUrl: './vault-item-detail.scss',
})
export class VaultItemDetail implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly vaultService = inject(VaultService);
  private readonly vaultSession = inject(VaultSessionService);
  private readonly dialog = inject(MatDialog);

  private readonly itemId = this.route.snapshot.paramMap.get('id') ?? '';

  /** This item's own key, resolved once in ngOnInit - either unwrapped from wrappedItemKey, or
   * (for a legacy item not yet migrated) the vault's own UVK directly. Used for every subsequent
   * decrypt/encrypt on this item's payload and attachments. */
  private itemKey: CryptoKey | null = null;
  /** Mirrors the loaded item's wrappedItemKey - null until this item has been migrated to a per-item key. */
  private wrappedItemKey: string | null = null;

  readonly itemTypes = VAULT_ITEM_TYPES;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly editing = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly revealedFields = signal<ReadonlySet<number>>(new Set());
  readonly attachments = signal<VaultItemAttachmentDto[]>([]);
  readonly uploadingLabel = signal<string | null>(null);
  readonly downloadingId = signal<string | null>(null);
  readonly itemShares = signal<VaultShareDto[]>([]);
  readonly sharingItem = signal(false);
  readonly successMessage = signal<string | null>(null);
  readonly createdAt = signal<string | null>(null);
  readonly updatedAt = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    label: ['', [Validators.required]],
    itemType: [VAULT_ITEM_TYPES[0], [Validators.required]],
    fields: this.fb.array([this.createFieldGroup()]),
  });

  get fields(): FormArray {
    return this.form.controls.fields;
  }

  createdUpdatedLabel(): string {
    const created = this.createdAt();
    const updated = this.updatedAt();
    if (!created || !updated) return '';
    return created === updated ? `Created ${relativeTime(created)}` : `Updated ${relativeTime(updated)}`;
  }

  ngOnInit(): void {
    this.vaultService.getItem(this.itemId).subscribe({
      next: async (dto) => {
        try {
          this.wrappedItemKey = dto.wrappedItemKey;
          this.itemKey = dto.wrappedItemKey
            ? await this.vaultSession.unwrapOwnedItemKey(dto.wrappedItemKey)
            : this.vaultSession.uvk;
          const payload = await decryptItemPayload<VaultItemPayload>(this.itemKey, dto.ciphertextBlob);
          this.form.patchValue({ label: payload.label, itemType: payload.itemType });
          this.fields.clear();
          payload.fields.forEach((field) => this.fields.push(this.createFieldGroup(field.name, field.value)));
          this.createdAt.set(dto.createdAt);
          this.updatedAt.set(dto.updatedAt);
          this.loading.set(false);
        } catch {
          this.loading.set(false);
          this.errorMessage.set('Could not decrypt this item.');
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Could not load this item.'));
      },
    });

    this.vaultService.getAttachments(this.itemId).subscribe({
      next: (attachments) => this.attachments.set(attachments),
      error: (err) => this.errorMessage.set(extractErrorMessage(err, 'Could not load attachments.')),
    });

    this.loadItemShares();
  }

  addField(): void {
    this.fields.push(this.createFieldGroup());
  }

  removeField(index: number): void {
    this.fields.removeAt(index);
  }

  isRevealed(index: number): boolean {
    return this.revealedFields().has(index);
  }

  toggleReveal(index: number): void {
    this.revealedFields.update((current) => {
      const next = new Set(current);
      if (next.has(index)) {
        next.delete(index);
      } else {
        next.add(index);
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

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    const raw = this.form.getRawValue();
    const payload: VaultItemPayload = { label: raw.label, itemType: raw.itemType, fields: raw.fields };

    this.encryptForSave(payload)
      .then(({ ciphertextBlob, wrappedItemKey }) => {
        this.vaultService.updateItem(this.itemId, { ciphertextBlob, wrappedItemKey }).subscribe({
          next: () => {
            this.saving.set(false);
            this.editing.set(false);
          },
          error: (err) => {
            this.saving.set(false);
            this.errorMessage.set(extractErrorMessage(err, 'Could not save changes.'));
          },
        });
      })
      .catch(() => {
        this.saving.set(false);
        this.errorMessage.set('Could not encrypt item.');
      });
  }

  /** Opportunistically migrates a still-legacy item onto its own per-item key, as long as it has
   * no attachments yet - an item with attachments only migrates via the "Share this item" flow,
   * which re-encrypts the attachments too (a payload-migrated-but-attachments-still-legacy item
   * would break the "everything under one key" invariant item-scoped sharing depends on). */
  private async encryptForSave(payload: VaultItemPayload): Promise<{ ciphertextBlob: string; wrappedItemKey: string | undefined }> {
    if (this.wrappedItemKey === null && this.attachments().length === 0) {
      const itemKey = await generateItemKey();
      const rawItemKey = new Uint8Array(await crypto.subtle.exportKey('raw', itemKey));
      const [ciphertextBlob, wrappedItemKey] = await Promise.all([
        encryptItemPayload(itemKey, payload),
        wrapRawBytes(rawItemKey, this.vaultSession.uvk),
      ]);
      this.itemKey = itemKey;
      this.wrappedItemKey = wrappedItemKey;
      return { ciphertextBlob, wrappedItemKey };
    }

    const ciphertextBlob = await encryptItemPayload(this.itemKey!, payload);
    return { ciphertextBlob, wrappedItemKey: undefined };
  }

  deleteItem(): void {
    this.dialog.open(ConfirmDialog, {
      data: {
        title: 'Delete Item',
        message: 'Are you sure you want to delete this item?',
        confirmText: 'Delete',
        cancelText: 'Cancel',
      },
    }).afterClosed().subscribe((confirmed: boolean) => {
      if (confirmed) {
        this.vaultService.deleteItem(this.itemId).subscribe({
          next: () => this.router.navigateByUrl('/vault'),
          error: (err) => this.errorMessage.set(extractErrorMessage(err, 'Could not delete item.')),
        });
      }
    });
  }

  async shareItem(): Promise<void> {
    const dialogRef = this.dialog.open<ShareItemDialog, unknown, ShareItemDialogResult>(ShareItemDialog, {
      data: { itemLabel: this.form.controls.label.value || 'this item' },
    });
    const result = await firstValueFrom(dialogRef.afterClosed());
    if (!result) {
      return;
    }

    this.sharingItem.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      // Only a still-legacy item needs migrating onto its own key first - an item already sharing
      // individually or edited since this feature shipped already has one.
      if (this.wrappedItemKey === null) {
        await this.migrateToItemKey();
      }

      const lookup = await firstValueFrom(this.vaultService.lookupUser(result.email));
      const publicKey = await importPublicKeySpki(lookup.publicKey);
      const wrappedKey = await wrapAesKeyForRecipient(this.itemKey!, publicKey, lookup.mlKemPublicKey);
      await firstValueFrom(
        this.vaultService.createShare({
          holderEmail: result.email,
          wrappedKey,
          vaultItemId: this.itemId,
          canEdit: result.canEdit,
        }),
      );

      this.loadItemShares();
      this.successMessage.set(`Shared with ${lookup.firstName} ${lookup.lastName} - pending verification.`);
    } catch (err) {
      this.errorMessage.set(
        err instanceof HttpErrorResponse ? extractErrorMessage(err, 'Could not share this item.') : 'Could not share this item.',
      );
    } finally {
      this.sharingItem.set(false);
    }
  }

  viewHistory(): void {
    this.dialog.open(ItemRevisionsDialog, {
      data: { itemId: this.itemId, itemKey: this.itemKey!, uvk: this.vaultSession.uvk },
    });
  }

  revokeItemShare(shareId: string): void {
    this.vaultService.revokeShare(shareId).subscribe({
      next: () => this.itemShares.update((shares) => shares.filter((share) => share.id !== shareId)),
      error: (err) => this.errorMessage.set(extractErrorMessage(err, 'Could not revoke access.')),
    });
  }

  /**
   * One-time migration for a still-legacy item (never had its own key) onto per-item-key
   * encryption, run just before its first individual share. Decrypts the current payload and
   * every attachment with the item's current (legacy UVK) key, re-encrypts everything with a
   * fresh item key, and commits in an order that never leaves the item in a broken state if
   * something fails partway: upload the newly-encrypted attachments first, delete the old ones
   * next, and only then PUT the item with its new ciphertext and wrappedItemKey - the field that
   * flips every other reader's decryption assumption for this item.
   */
  private async migrateToItemKey(): Promise<void> {
    const currentKey = this.itemKey!;
    const raw = this.form.getRawValue();
    const payload: VaultItemPayload = { label: raw.label, itemType: raw.itemType, fields: raw.fields };

    const newItemKey = await generateItemKey();
    const rawNewItemKey = new Uint8Array(await crypto.subtle.exportKey('raw', newItemKey));
    const [ciphertextBlob, wrappedItemKey] = await Promise.all([
      encryptItemPayload(newItemKey, payload),
      wrapRawBytes(rawNewItemKey, this.vaultSession.uvk),
    ]);

    const oldAttachments = this.attachments();
    const newAttachments: VaultItemAttachmentDto[] = [];
    for (const attachment of oldAttachments) {
      const ciphertext = await firstValueFrom(this.vaultService.downloadAttachment(this.itemId, attachment.id));
      const plaintext = await decryptFileBytes(currentKey, attachment.iv, ciphertext);
      const encrypted = await encryptFileBytes(newItemKey, plaintext);
      const response = await firstValueFrom(
        this.vaultService
          .uploadAttachment(this.itemId, attachment.fileName, attachment.contentType, encrypted.ivB64, plaintext.byteLength, encrypted.ciphertext)
          .pipe(filter((e): e is HttpResponse<VaultItemAttachmentDto> => e.type === HttpEventType.Response)),
      );
      if (response.body) {
        newAttachments.push(response.body);
      }
    }

    for (const attachment of oldAttachments) {
      await firstValueFrom(this.vaultService.deleteAttachment(this.itemId, attachment.id));
    }

    await firstValueFrom(this.vaultService.updateItem(this.itemId, { ciphertextBlob, wrappedItemKey }));

    this.itemKey = newItemKey;
    this.wrappedItemKey = wrappedItemKey;
    this.attachments.set(newAttachments);
  }

  private loadItemShares(): void {
    this.vaultService.getItemShares(this.itemId).subscribe({
      next: (shares) => this.itemShares.set(shares),
      // Non-critical - the item itself is still fully usable if this list fails to load.
      error: () => {},
    });
  }

  async addFiles(fileList: FileList | null): Promise<void> {
    if (!fileList) {
      return;
    }

    const files = Array.from(fileList);
    if (files.some((file) => !this.hasAllowedExtension(file.name))) {
      this.errorMessage.set('Only .pdf and .docx files are allowed.');
      return;
    }

    this.errorMessage.set(null);
    for (const file of files) {
      try {
        this.uploadingLabel.set(file.name);
        const buffer = await file.arrayBuffer();
        const { ivB64, ciphertext } = await encryptFileBytes(this.itemKey!, buffer);
        const response = await firstValueFrom(
          this.vaultService
            .uploadAttachment(this.itemId, file.name, file.type, ivB64, file.size, ciphertext)
            .pipe(filter((e): e is HttpResponse<VaultItemAttachmentDto> => e.type === HttpEventType.Response)),
        );
        if (response.body) {
          this.attachments.update((current) => [...current, response.body!]);
        }
      } catch (err) {
        const fallback = `Could not upload ${file.name}.`;
        this.errorMessage.set(err instanceof HttpErrorResponse ? extractErrorMessage(err, fallback) : fallback);
      }
    }
    this.uploadingLabel.set(null);
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

  deleteAttachment(attachment: VaultItemAttachmentDto): void {
    this.dialog.open(ConfirmDialog, {
      data: {
        title: `Delete ${attachment.fileName}?`,
        message: `Are you sure you want to delete ${attachment.fileName}? This action cannot be undone.`,
        confirmText: 'Delete',
        cancelText: 'Cancel',
        danger: true,
      }
    }).afterClosed().subscribe((result: boolean) => {
      if (result) {
        this.vaultService.deleteAttachment(this.itemId, attachment.id).subscribe({
          next: () => this.attachments.update((current) => current.filter((a) => a.id !== attachment.id)),
          error: (err) => this.errorMessage.set(extractErrorMessage(err, `Could not delete ${attachment.fileName}.`)),
        });
      }
    });
  }

  private hasAllowedExtension(fileName: string): boolean {
    const lower = fileName.toLowerCase();
    return ALLOWED_ATTACHMENT_EXTENSIONS.some((ext) => lower.endsWith(ext));
  }

  private createFieldGroup(name = '', value = '') {
    return this.fb.nonNullable.group({
      name: [name, [Validators.required]],
      value: [value, [Validators.required]],
    });
  }
}
