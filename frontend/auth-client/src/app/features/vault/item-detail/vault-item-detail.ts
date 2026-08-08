import { Component, OnInit, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { decryptItemPayload, encryptItemPayload } from '../../../core/crypto/webcrypto.util';
import { VaultItemPayload } from '../../../core/models/vault.models';
import { VaultService } from '../../../core/services/vault.service';
import { VaultSessionService } from '../../../core/services/vault-session.service';
import { extractErrorMessage } from '../../auth/auth-error.util';
import { VAULT_ITEM_TYPES } from '../item-create/vault-item-create';

@Component({
  selector: 'app-vault-item-detail',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
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

  private readonly itemId = this.route.snapshot.paramMap.get('id') ?? '';

  readonly itemTypes = VAULT_ITEM_TYPES;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly editing = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly revealedFields = signal<ReadonlySet<number>>(new Set());

  readonly form = this.fb.nonNullable.group({
    label: ['', [Validators.required]],
    itemType: [VAULT_ITEM_TYPES[0], [Validators.required]],
    fields: this.fb.array([this.createFieldGroup()]),
  });

  get fields(): FormArray {
    return this.form.controls.fields;
  }

  ngOnInit(): void {
    this.vaultService.getItem(this.itemId).subscribe({
      next: async (dto) => {
        try {
          const payload = await decryptItemPayload<VaultItemPayload>(this.vaultSession.uvk, dto.ciphertextBlob);
          this.form.patchValue({ label: payload.label, itemType: payload.itemType });
          this.fields.clear();
          payload.fields.forEach((field) => this.fields.push(this.createFieldGroup(field.name, field.value)));
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

  async copyValue(value: string): Promise<void> {
    await navigator.clipboard.writeText(value);
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

    encryptItemPayload(this.vaultSession.uvk, payload)
      .then((ciphertextBlob) => {
        this.vaultService.updateItem(this.itemId, { ciphertextBlob }).subscribe({
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

  deleteItem(): void {
    this.vaultService.deleteItem(this.itemId).subscribe({
      next: () => this.router.navigateByUrl('/vault'),
      error: (err) => this.errorMessage.set(extractErrorMessage(err, 'Could not delete item.')),
    });
  }

  private createFieldGroup(name = '', value = '') {
    return this.fb.nonNullable.group({
      name: [name, [Validators.required]],
      value: [value, [Validators.required]],
    });
  }
}
