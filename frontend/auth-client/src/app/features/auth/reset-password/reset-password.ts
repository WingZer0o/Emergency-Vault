import { Component, OnInit, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import {
  DEFAULT_KDF_ITERATIONS,
  deriveMasterKey,
  generateSaltBase64,
  importRecoveryKey,
  unwrapPrivateKey,
  wrapPrivateKey,
} from '../../../core/crypto/webcrypto.util';
import { VaultRecoverRequest } from '../../../core/models/vault.models';
import { AuthService } from '../../../core/services/auth.service';
import { VaultService } from '../../../core/services/vault.service';
import { extractErrorMessage } from '../auth-error.util';

function passwordsMatchValidator(control: AbstractControl): ValidationErrors | null {
  const password = control.get('newPassword')?.value;
  const confirmPassword = control.get('confirmPassword')?.value;
  return password === confirmPassword ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-reset-password',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './reset-password.html',
  styleUrl: './reset-password.scss',
})
export class ResetPassword implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly vaultService = inject(VaultService);
  private readonly route = inject(ActivatedRoute);

  private readonly userId = this.route.snapshot.queryParamMap.get('userId') ?? '';
  private readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';

  /** Set only if this account has a vault - fetched via the anonymous reset-recovery-bundle endpoint. */
  private privateKeyWrappedByRecoveryKey: string | null = null;

  readonly linkInvalid = !this.userId || !this.token;
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly hasVaultToRecover = signal(false);

  readonly form = this.fb.nonNullable.group(
    {
      newPassword: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]],
      recoveryKey: [''],
    },
    { validators: passwordsMatchValidator },
  );

  ngOnInit(): void {
    if (this.linkInvalid) {
      return;
    }

    // Speculative: only reveals the recovery-key field if this account actually has a vault. A
    // 404 (no vault, or an invalid/expired link - re-checked properly by the reset call itself)
    // is swallowed silently rather than surfaced as an error on this screen.
    this.vaultService.getResetRecoveryBundle(this.userId, this.token).subscribe({
      next: (bundle) => {
        this.privateKeyWrappedByRecoveryKey = bundle.privateKeyWrappedByRecoveryKey;
        this.hasVaultToRecover.set(true);
      },
      error: () => {},
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    const { newPassword, recoveryKey } = this.form.getRawValue();
    this.buildVaultRewrap(newPassword, recoveryKey)
      .then((vaultRewrap) => {
        this.authService.resetPassword({ userId: this.userId, token: this.token, newPassword, vaultRewrap }).subscribe({
          next: (res) => {
            this.loading.set(false);
            this.successMessage.set(res.message);
          },
          error: (err) => {
            this.loading.set(false);
            this.errorMessage.set(extractErrorMessage(err, 'Could not reset password.'));
          },
        });
      })
      .catch(() => {
        this.loading.set(false);
        this.errorMessage.set('Could not verify that recovery key. Check it and try again.');
      });
  }

  /**
   * Unwraps the vault's private key using the recovery key (never the old password, which this
   * flow never has), then re-wraps it under a Master Key derived from the new password - so the
   * vault stays reachable via the normal unlock screen after this reset. Returns undefined (no
   * rewrap attached) when there's no vault to recover or the user left the field blank.
   */
  private async buildVaultRewrap(newPassword: string, recoveryKeyDisplay: string): Promise<VaultRecoverRequest | undefined> {
    if (!this.privateKeyWrappedByRecoveryKey || !recoveryKeyDisplay) {
      return undefined;
    }

    const recoveryKey = await importRecoveryKey(recoveryKeyDisplay);
    const privateKey = await unwrapPrivateKey(this.privateKeyWrappedByRecoveryKey, recoveryKey, true);

    const newKdfSalt = generateSaltBase64();
    const newMasterKey = await deriveMasterKey(newPassword, newKdfSalt, DEFAULT_KDF_ITERATIONS);
    const newPrivateKeyWrappedByMasterKey = await wrapPrivateKey(privateKey, newMasterKey);

    return { newKdfSalt, newKdfIterations: DEFAULT_KDF_ITERATIONS, newPrivateKeyWrappedByMasterKey };
  }
}
