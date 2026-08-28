import { Component, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpEventType, HttpResponse } from '@angular/common/http';
import { filter, firstValueFrom } from 'rxjs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { encryptFileBytes, encryptItemPayload, generateItemKey, wrapRawBytes } from '../../../core/crypto/webcrypto.util';
import { VaultItemAttachmentDto, VaultItemPayload } from '../../../core/models/vault.models';
import { VaultService } from '../../../core/services/vault.service';
import { VaultSessionService } from '../../../core/services/vault-session.service';
import { extractErrorMessage } from '../../auth/auth-error.util';
import { checkPwnedCount } from '../../../core/utils/pwned-password.util';

export const VAULT_ITEM_TYPES = ['Financial Institution Login', 'Insurance', 'Medical', 'Legal Documents', 'Documents', 'Other'];

/** Server-enforced too (see VaultService.UploadAttachmentAsync) - this is just an early,
 * friendlier rejection before the user waits on an upload that would fail anyway. */
export const ALLOWED_ATTACHMENT_EXTENSIONS = ['.pdf', '.docx'];

@Component({
  selector: 'app-vault-item-create',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './vault-item-create.html',
  styleUrl: './vault-item-create.scss',
})
export class VaultItemCreate {
  private readonly fb = inject(FormBuilder);
  private readonly vaultService = inject(VaultService);
  private readonly vaultSession = inject(VaultSessionService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly itemTypes = VAULT_ITEM_TYPES;
  readonly stagedFiles = signal<File[]>([]);
  readonly uploadingLabel = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    label: ['', [Validators.required]],
    itemType: [VAULT_ITEM_TYPES[0], [Validators.required]],
    fields: this.fb.array([this.createFieldGroup()]),
  });

  constructor() {
    // Fields are optional - an item can be just a label plus attachments, e.g. a scanned
    // document with nothing worth storing as structured key/value data. The array above starts
    // with one group only so TypeScript can infer its element type; clear it back out here.
    this.fields.clear();
    this.fields.push(this.createFieldGroup('Username'));
    this.fields.push(this.createFieldGroup('Password'));
  }

  get fields(): FormArray {
    return this.form.controls.fields;
  }

  addField(): void {
    this.fields.push(this.createFieldGroup());
  }

  removeField(index: number): void {
    this.fields.removeAt(index);
  }

  addFiles(fileList: FileList | null): void {
    if (!fileList) {
      return;
    }

    const files = Array.from(fileList);
    const rejected = files.filter((file) => !this.hasAllowedExtension(file.name));
    if (rejected.length > 0) {
      this.errorMessage.set('Only .pdf and .docx files are allowed.');
      return;
    }

    const accepted = files.filter((file) => this.hasAllowedExtension(file.name));
    this.stagedFiles.update((current) => [...current, ...accepted]);
  }

  removeStagedFile(index: number): void {
    this.stagedFiles.update((current) => current.filter((_, i) => i !== index));
  }

  async submit(): Promise<void> {
    const password = this.form.controls.fields.controls.find(control => control.controls.name.value === 'Password' || control.controls.name.value.toLowerCase().includes('pass'))?.controls.value.value || '';
    if (this.form.invalid || (await checkPwnedCount(password)) > 0) {
      this.errorMessage.set('The password you entered has been found in a known data breach. Please choose a different password.');
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    const raw = this.form.getRawValue();
    const payload: VaultItemPayload = { label: raw.label, itemType: raw.itemType, fields: raw.fields };

    this.encryptForCreate(payload)
      .then(({ ciphertextBlob, wrappedItemKey, itemKey }) => {
        this.vaultService.createItem({ ciphertextBlob, wrappedItemKey }).subscribe({
          next: async (item) => {
            try {
              await this.uploadStagedFiles(item.id, itemKey);
              this.router.navigateByUrl('/vault');
            } catch {
              this.loading.set(false);
              this.uploadingLabel.set(null);
              this.errorMessage.set('Item was saved, but one or more attachments failed to upload.');
            }
          },
          error: (err) => {
            this.loading.set(false);
            this.errorMessage.set(extractErrorMessage(err, 'Could not save item.'));
          },
        });
      })
      .catch(() => {
        this.loading.set(false);
        this.errorMessage.set('Could not encrypt item.');
      });
  }

  /** Every item gets its own key (IK): the payload is encrypted with IK, and IK itself is wrapped
   * under the owner's UVK so it can be recovered later - never encrypted directly with the UVK. */
  private async encryptForCreate(
    payload: VaultItemPayload,
  ): Promise<{ ciphertextBlob: string; wrappedItemKey: string; itemKey: CryptoKey }> {
    const itemKey = await generateItemKey();
    const rawItemKey = new Uint8Array(await crypto.subtle.exportKey('raw', itemKey));
    const [ciphertextBlob, wrappedItemKey] = await Promise.all([
      encryptItemPayload(itemKey, payload),
      wrapRawBytes(rawItemKey, this.vaultSession.uvk),
    ]);
    return { ciphertextBlob, wrappedItemKey, itemKey };
  }

  private async uploadStagedFiles(itemId: string, itemKey: CryptoKey): Promise<void> {
    for (const file of this.stagedFiles()) {
      this.uploadingLabel.set(file.name);
      const buffer = await file.arrayBuffer();
      const { ivB64, ciphertext } = await encryptFileBytes(itemKey, buffer);
      await firstValueFrom(
        this.vaultService
          .uploadAttachment(itemId, file.name, file.type, ivB64, file.size, ciphertext)
          .pipe(filter((event): event is HttpResponse<VaultItemAttachmentDto> => event.type === HttpEventType.Response)),
      );
    }
    this.uploadingLabel.set(null);
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
