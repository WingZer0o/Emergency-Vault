import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { importPublicKeySpki } from '../../../core/crypto/webcrypto.util';
import { wrapUvkForRecipient } from '../../../core/crypto/hybrid-kem.util';
import { VaultShareDto, VaultShareStatus } from '../../../core/models/vault.models';
import { VaultService } from '../../../core/services/vault.service';
import { VaultSessionService } from '../../../core/services/vault-session.service';
import { extractErrorMessage } from '../../auth/auth-error.util';
import { ConfirmDialog } from '../../../shared/confirm-dialog/confirm-dialog';
import { MatDialog } from '@angular/material/dialog';

@Component({
  selector: 'app-vault-sharing',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './vault-sharing.html',
  styleUrl: './vault-sharing.scss',
})
export class VaultSharing implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly vaultService = inject(VaultService);
  private readonly vaultSession = inject(VaultSessionService);
  private readonly dialog = inject(MatDialog);

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
          .then((publicKey) => wrapUvkForRecipient(this.vaultSession.uvk, publicKey, lookup.mlKemPublicKey))
          .then((wrappedKey) => {
            this.vaultService.createShare({ holderEmail: email, wrappedKey, vaultItemId: null, canEdit: false }).subscribe({
              next: (share) => {
                this.loading.set(false);
                this.successMessage.set(
                  `Invite sent to ${share.holderName} - pending verification. They'll need to enter the code emailed to them before they can access your vault.`,
                );
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
    this.dialog.open(ConfirmDialog, {
      data: {
        title: 'Revoke Access',
        message: 'Are you sure you want to revoke access for this user?',
      },
    }).afterClosed().subscribe((confirmed) => { 
      if (confirmed) {
        this.vaultService.revokeShare(shareId).subscribe({
          next: () => this.shares.update((shares) => shares.filter((share) => share.id !== shareId)),
          error: (err) => this.errorMessage.set(extractErrorMessage(err, 'Could not revoke access.')),
        });
      }
    })
  }

  statusTagClass(status: VaultShareStatus): string {
    switch (status) {
      case 'Active':
        return 'tag-accent';
      case 'Failed':
        return 'tag-outline';
      default:
        return 'tag-neutral';
    }
  }

  statusLabel(status: VaultShareStatus): string {
    switch (status) {
      case 'Active':
        return 'Active';
      case 'Pending':
        return 'Pending verification';
      case 'CodeExpired':
        return 'Code expired - ask them to resend it';
      case 'Failed':
        return 'Failed - revoke and share again';
    }
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
