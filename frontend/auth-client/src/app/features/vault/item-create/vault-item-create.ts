import { Component, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { encryptItemPayload } from '../../../core/crypto/webcrypto.util';
import { VaultItemPayload } from '../../../core/models/vault.models';
import { VaultService } from '../../../core/services/vault.service';
import { VaultSessionService } from '../../../core/services/vault-session.service';
import { extractErrorMessage } from '../../auth/auth-error.util';

export const VAULT_ITEM_TYPES = ['Bank login', 'Insurance', 'Medical', 'Other'];

@Component({
  selector: 'app-vault-item-create',
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

  readonly form = this.fb.nonNullable.group({
    label: ['', [Validators.required]],
    itemType: [VAULT_ITEM_TYPES[0], [Validators.required]],
    fields: this.fb.array([this.createFieldGroup()]),
  });

  get fields(): FormArray {
    return this.form.controls.fields;
  }

  addField(): void {
    this.fields.push(this.createFieldGroup());
  }

  removeField(index: number): void {
    this.fields.removeAt(index);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    const raw = this.form.getRawValue();
    const payload: VaultItemPayload = { label: raw.label, itemType: raw.itemType, fields: raw.fields };

    encryptItemPayload(this.vaultSession.uvk, payload)
      .then((ciphertextBlob) => {
        this.vaultService.createItem({ ciphertextBlob }).subscribe({
          next: () => this.router.navigateByUrl('/vault'),
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

  private createFieldGroup(name = '', value = '') {
    return this.fb.nonNullable.group({
      name: [name, [Validators.required]],
      value: [value, [Validators.required]],
    });
  }
}
