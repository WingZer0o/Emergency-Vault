import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { VaultSessionService } from '../../../core/services/vault-session.service';
import { extractErrorMessage } from '../../auth/auth-error.util';

@Component({
  selector: 'app-vault-recover',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './vault-recover.html',
  styleUrl: './vault-recover.scss',
})
export class VaultRecover {
  private readonly fb = inject(FormBuilder);
  private readonly vaultSession = inject(VaultSessionService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    recoveryKey: ['', [Validators.required]],
    currentPassword: ['', [Validators.required]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    const { recoveryKey, currentPassword } = this.form.getRawValue();
    this.vaultSession
      .recoverWithRecoveryKey(recoveryKey, currentPassword)
      .then(() => this.router.navigateByUrl('/vault'))
      .catch((err) => {
        this.loading.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Could not recover vault access. Check your recovery key.'));
      });
  }
}
