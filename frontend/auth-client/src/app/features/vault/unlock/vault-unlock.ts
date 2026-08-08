import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { VaultSessionService } from '../../../core/services/vault-session.service';

@Component({
  selector: 'app-vault-unlock',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './vault-unlock.html',
  styleUrl: './vault-unlock.scss',
})
export class VaultUnlock {
  private readonly fb = inject(FormBuilder);
  private readonly vaultSession = inject(VaultSessionService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

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
      .unlockWithPassword(this.form.getRawValue().password)
      .then(() => this.router.navigateByUrl('/vault'))
      .catch(() => {
        this.loading.set(false);
        // A wrong password fails via a WebCrypto GCM auth-tag mismatch, which carries no useful
        // message of its own - this screen is only reachable when a vault definitely exists
        // (vaultSetupGuard), so a generic "incorrect password" is the overwhelmingly likely cause.
        this.errorMessage.set('Incorrect password.');
      });
  }
}
