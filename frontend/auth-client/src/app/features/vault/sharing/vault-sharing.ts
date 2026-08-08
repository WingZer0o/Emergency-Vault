import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { importPublicKeySpki, wrapUvkForPublicKey } from '../../../core/crypto/webcrypto.util';
import { VaultShareDto } from '../../../core/models/vault.models';
import { VaultService } from '../../../core/services/vault.service';
import { VaultSessionService } from '../../../core/services/vault-session.service';
import { extractErrorMessage } from '../../auth/auth-error.util';

@Component({
  selector: 'app-vault-sharing',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatListModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './vault-sharing.html',
  styleUrl: './vault-sharing.scss',
})
export class VaultSharing implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly vaultService = inject(VaultService);
  private readonly vaultSession = inject(VaultSessionService);

  readonly loading = signal(false);
  readonly loadingShares = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly shares = signal<VaultShareDto[]>([]);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  ngOnInit(): void {
    this.loadShares();
  }

  grantAccess(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const email = this.form.getRawValue().email;

    this.vaultService.lookupUser(email).subscribe({
      next: (lookup) => {
        importPublicKeySpki(lookup.publicKey)
          .then((publicKey) => wrapUvkForPublicKey(this.vaultSession.uvk, publicKey))
          .then((wrappedUvk) => {
            this.vaultService.createShare({ holderEmail: email, wrappedUvk }).subscribe({
              next: (share) => {
                this.loading.set(false);
                this.successMessage.set(`Granted access to ${share.holderName}.`);
                this.form.reset();
                this.loadShares();
              },
              error: (err) => {
                this.loading.set(false);
                this.errorMessage.set(extractErrorMessage(err, 'Could not grant access.'));
              },
            });
          })
          .catch(() => {
            this.loading.set(false);
            this.errorMessage.set('Could not wrap the vault key for that user.');
          });
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(
          extractErrorMessage(err, "No user found with that email, or they haven't set up their vault yet."),
        );
      },
    });
  }

  revoke(shareId: string): void {
    this.vaultService.revokeShare(shareId).subscribe({
      next: () => this.shares.update((shares) => shares.filter((share) => share.id !== shareId)),
      error: (err) => this.errorMessage.set(extractErrorMessage(err, 'Could not revoke access.')),
    });
  }

  private loadShares(): void {
    this.loadingShares.set(true);
    this.vaultService.getShares().subscribe({
      next: (shares) => {
        this.shares.set(shares);
        this.loadingShares.set(false);
      },
      error: (err) => {
        this.loadingShares.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Could not load emergency contacts.'));
      },
    });
  }
}
