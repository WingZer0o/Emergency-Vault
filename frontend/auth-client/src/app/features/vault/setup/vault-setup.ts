import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatCheckboxModule } from '@angular/material/checkbox';

import { VaultSessionService } from '../../../core/services/vault-session.service';
import { extractErrorMessage } from '../../auth/auth-error.util';

@Component({
  selector: 'app-vault-setup',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatCheckboxModule,
  ],
  templateUrl: './vault-setup.html',
  styleUrl: './vault-setup.scss',
})
export class VaultSetup {
  private readonly fb = inject(FormBuilder);
  private readonly vaultSession = inject(VaultSessionService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly recoveryKey = signal<string | null>(null);
  readonly savedConfirmed = signal(false);

  readonly form = this.fb.nonNullable.group({
    password: ['', [Validators.required]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    this.vaultSession
      .setupVault(this.form.getRawValue().password)
      .then((recoveryKey) => {
        this.loading.set(false);
        this.recoveryKey.set(recoveryKey);
      })
      .catch((err) => {
        this.loading.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Could not set up your vault.'));
      });
  }

  finish(): void {
    this.router.navigateByUrl('/vault');
  }
}
