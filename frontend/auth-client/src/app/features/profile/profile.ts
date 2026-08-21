import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatListModule } from '@angular/material/list';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatRadioChange, MatRadioModule } from '@angular/material/radio';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { AuthService } from '../../core/services/auth.service';
import { VaultSessionService } from '../../core/services/vault-session.service';
import { UserProfile } from '../../core/models/auth.models';
import { TwoFactorMethodName } from '../../core/models/auth.models';
import { MatButtonModule } from '@angular/material/button';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { extractErrorMessage } from '../auth/auth-error.util';

@Component({
  selector: 'app-profile',
  imports: [
    MatCardModule,
    MatListModule,
    MatButtonModule,
    MatChipsModule,
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
  private readonly vaultSession = inject(VaultSessionService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly users = signal<UserProfile[] | null>(null);
  readonly usersLoading = signal(false);

  readonly twoFactorMethod = signal<TwoFactorMethodName>('None');
  readonly twoFactorLoading = signal(true);
  readonly twoFactorSaving = signal(false);
  readonly twoFactorError = signal<string | null>(null);

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

    const request = method === 'Email' ? this.authService.enableTwoFactor() : this.authService.disableTwoFactor();
    request.subscribe({
      next: () => {
        this.twoFactorMethod.set(method);
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
      next: () => {
        this.twoFactorMethod.set('Totp');
        this.twoFactorSaving.set(false);
        this.cancelTotpEnrollment();
      },
      error: (err) => {
        this.twoFactorSaving.set(false);
        this.totpError.set(extractErrorMessage(err, 'Incorrect code.'));
      },
    });
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
      next: ({ method }) => {
        this.twoFactorMethod.set(method);
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
