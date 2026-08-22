import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatRadioChange, MatRadioModule } from '@angular/material/radio';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { AuthService } from '../../core/services/auth.service';
import { VaultService } from '../../core/services/vault.service';
import { VaultSessionService } from '../../core/services/vault-session.service';
import { UserProfile } from '../../core/models/auth.models';
import { TwoFactorMethodName } from '../../core/models/auth.models';
import { VaultShareDto, VaultShareStatus } from '../../core/models/vault.models';
import { MatButtonModule } from '@angular/material/button';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { extractErrorMessage } from '../auth/auth-error.util';

@Component({
  selector: 'app-profile',
  imports: [
    RouterLink,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatRadioModule,
    MatFormFieldModule,
    MatInputModule,
    ReactiveFormsModule,
  ],
  templateUrl: './profile.html',
  styleUrls: ['./profile.scss'],
})
export class Profile implements OnInit {
  private readonly http = inject(HttpClient);
  protected readonly authService = inject(AuthService);
  private readonly vaultService = inject(VaultService);
  private readonly vaultSession = inject(VaultSessionService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly users = signal<UserProfile[] | null>(null);
  readonly usersLoading = signal(false);

  /** Whole-vault emergency-access holders - real data already served by VaultService.getShares(),
   * not previously surfaced on this screen. Managed in full on /vault/sharing; this is a summary. */
  readonly holders = signal<VaultShareDto[]>([]);

  readonly twoFactorMethod = signal<TwoFactorMethodName>('None');
  readonly twoFactorLoading = signal(true);
  readonly twoFactorSaving = signal(false);
  readonly twoFactorError = signal<string | null>(null);
  readonly recoveryCodesRemaining = signal(0);

  /** Non-null only right after a fresh batch is generated - the one moment codes are visible in plaintext. */
  readonly recoveryCodes = signal<string[] | null>(null);
  readonly recoveryCodesSaving = signal(false);

  /** True while the "Authenticator app" enrollment panel (QR + confirmation code) is open. */
  readonly totpEnrollmentActive = signal(false);
  readonly totpSecret = signal<string | null>(null);
  readonly totpQrCode = signal<string | null>(null);
  readonly totpError = signal<string | null>(null);

  readonly totpCodeForm = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
  });

  readonly deleteInProgress = signal(false);
  readonly deleteError = signal<string | null>(null);

  ngOnInit(): void {
    this.loadTwoFactorStatus();
    this.loadHolders();
  }

  signInMethodLabel(): string {
    switch (this.twoFactorMethod()) {
      case 'Totp':
        return 'Authenticator app';
      case 'Email':
        return 'Email code';
      default:
        return 'Password only';
    }
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

  private loadHolders(): void {
    this.vaultService.getShares().subscribe({
      next: (shares) => this.holders.set(shares),
      // Non-critical - manage-holders is still fully usable at /vault/sharing if this fails.
      error: () => {},
    });
  }

  onTwoFactorMethodChange(event: MatRadioChange): void {
    const method = event.value as TwoFactorMethodName;
    if (method === this.twoFactorMethod()) {
      return;
    }

    this.twoFactorError.set(null);

    if (method === 'Totp') {
      this.startTotpEnrollment();
      return;
    }

    this.cancelTotpEnrollment();
    this.twoFactorSaving.set(true);

    if (method === 'None') {
      this.authService.disableTwoFactor().subscribe({
        next: () => {
          this.twoFactorMethod.set(method);
          this.recoveryCodesRemaining.set(0);
          this.twoFactorSaving.set(false);
        },
        error: () => {
          this.twoFactorSaving.set(false);
          this.twoFactorError.set('Could not update two-factor authentication. Please try again.');
        },
      });
      return;
    }

    this.authService.enableTwoFactor().subscribe({
      next: ({ recoveryCodes }) => {
        this.twoFactorMethod.set(method);
        this.applyNewRecoveryCodes(recoveryCodes);
        this.twoFactorSaving.set(false);
      },
      error: () => {
        this.twoFactorSaving.set(false);
        this.twoFactorError.set('Could not update two-factor authentication. Please try again.');
      },
    });
  }

  confirmTotpEnrollment(): void {
    if (this.totpCodeForm.invalid) {
      this.totpCodeForm.markAllAsTouched();
      return;
    }

    this.twoFactorSaving.set(true);
    this.totpError.set(null);

    this.authService.confirmTotpEnrollment(this.totpCodeForm.getRawValue().code).subscribe({
      next: ({ recoveryCodes }) => {
        this.twoFactorMethod.set('Totp');
        this.applyNewRecoveryCodes(recoveryCodes);
        this.twoFactorSaving.set(false);
        this.cancelTotpEnrollment();
      },
      error: (err) => {
        this.twoFactorSaving.set(false);
        this.totpError.set(extractErrorMessage(err, 'Incorrect code.'));
      },
    });
  }

  regenerateRecoveryCodes(): void {
    this.confirmDialog
      .confirm({
        title: 'Regenerate recovery codes',
        message: 'Your existing recovery codes will stop working immediately. Make sure you can save the new ones.',
        confirmText: 'Regenerate',
        danger: true,
      })
      .subscribe((confirmed) => {
        if (confirmed) {
          this.doRegenerateRecoveryCodes();
        }
      });
  }

  /** Client-side only - the codes were already persisted server-side, this just downloads the one-time view as a file. */
  downloadRecoveryCodes(): void {
    const codes = this.recoveryCodes();
    if (!codes) {
      return;
    }

    const blob = new Blob([codes.join('\n') + '\n'], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'emergencyvault-recovery-codes.txt';
    link.click();
    URL.revokeObjectURL(url);
  }

  dismissRecoveryCodes(): void {
    this.recoveryCodes.set(null);
  }

  private doRegenerateRecoveryCodes(): void {
    this.recoveryCodesSaving.set(true);
    this.twoFactorError.set(null);

    this.authService.regenerateRecoveryCodes().subscribe({
      next: ({ codes }) => {
        this.recoveryCodesSaving.set(false);
        this.applyNewRecoveryCodes(codes);
      },
      error: () => {
        this.recoveryCodesSaving.set(false);
        this.twoFactorError.set('Could not regenerate recovery codes. Please try again.');
      },
    });
  }

  private applyNewRecoveryCodes(codes: string[] | undefined): void {
    if (codes && codes.length > 0) {
      this.recoveryCodes.set(codes);
      this.recoveryCodesRemaining.set(codes.length);
    }
  }

  cancelTotpEnrollment(): void {
    this.totpEnrollmentActive.set(false);
    this.totpSecret.set(null);
    this.totpQrCode.set(null);
    this.totpError.set(null);
    this.totpCodeForm.reset();
  }

  private startTotpEnrollment(): void {
    this.twoFactorSaving.set(true);
    this.totpError.set(null);

    this.authService.startTotpEnrollment().subscribe({
      next: ({ secret, qrCode }) => {
        this.totpSecret.set(secret);
        this.totpQrCode.set(qrCode);
        this.totpEnrollmentActive.set(true);
        this.twoFactorSaving.set(false);
      },
      error: () => {
        this.twoFactorSaving.set(false);
        this.twoFactorError.set('Could not start authenticator app setup. Please try again.');
      },
    });
  }

  private loadTwoFactorStatus(): void {
    this.twoFactorLoading.set(true);
    this.authService.getTwoFactorStatus().subscribe({
      next: ({ method, recoveryCodesRemaining }) => {
        this.twoFactorMethod.set(method);
        this.recoveryCodesRemaining.set(recoveryCodesRemaining);
        this.twoFactorLoading.set(false);
      },
      error: () => this.twoFactorLoading.set(false),
    });
  }

  public onDeleteProfile(): void {
    this.confirmDialog
      .confirm({
        title: 'Delete profile',
        message:
          'This will permanently delete your profile and all of your vault secrets. This cannot be undone.',
        confirmText: 'Delete profile',
        danger: true,
      })
      .subscribe((confirmed) => {
        if (confirmed) {
          this.deleteProfile();
        }
      });
  }

  private deleteProfile(): void {
    this.deleteInProgress.set(true);
    this.deleteError.set(null);

    this.authService.deleteAccount().subscribe({
      next: () => {
        this.vaultSession.lock().then(() => this.router.navigateByUrl('/login'));
      },
      error: (err) => {
        this.deleteInProgress.set(false);
        this.deleteError.set(extractErrorMessage(err, 'Could not delete profile. Please try again.'));
      },
    });
  }
}
