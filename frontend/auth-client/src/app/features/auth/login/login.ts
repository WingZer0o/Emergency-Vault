import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { AuthService } from '../../../core/services/auth.service';
import { VaultSessionService } from '../../../core/services/vault-session.service';
import { TwoFactorMethodName } from '../../../core/models/auth.models';
import { extractErrorMessage } from '../auth-error.util';

@Component({
  selector: 'app-login',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly vaultSession = inject(VaultSessionService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly twoFactorToken = signal<string | null>(null);
  readonly twoFactorMethod = signal<TwoFactorMethodName | null>(null);

  /** Captured at submit() so a silent vault unlock attempt can reuse it after a 2FA challenge, without prompting for the password twice. */
  private pendingPassword = '';

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  readonly codeForm = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);
    this.pendingPassword = this.form.getRawValue().password;

    this.authService.login(this.form.getRawValue()).subscribe({
      next: (response) => {
        this.loading.set(false);
        if (response.requiresTwoFactor) {
          this.twoFactorToken.set(response.twoFactorToken);
          this.twoFactorMethod.set(response.method);
        } else {
          this.tryUnlockVault();
          this.router.navigateByUrl('/profile');
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Login failed.'));
      },
    });
  }

  submitCode(): void {
    const token = this.twoFactorToken();
    if (!token || this.codeForm.invalid) {
      this.codeForm.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    this.authService.verifyTwoFactor({ twoFactorToken: token, code: this.codeForm.getRawValue().code }).subscribe({
      next: () => {
        this.loading.set(false);
        this.tryUnlockVault();
        this.router.navigateByUrl('/profile');
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Incorrect code.'));
      },
    });
  }

  /**
   * Best-effort, fire-and-forget: unlocks the vault silently using the password already typed for
   * login, so the common case needs no extra prompt. Failure (no vault, or a password that's
   * diverged from the vault's) is swallowed - vaultUnlockedGuard falls back to the explicit
   * unlock screen the next time the user navigates into /vault. Deliberately one-directional
   * (Login -> Vault) so a vault failure can never affect core auth.
   */
  private tryUnlockVault(): void {
    this.vaultSession.unlockWithPassword(this.pendingPassword).catch(() => {});
  }

  resendCode(): void {
    const token = this.twoFactorToken();
    if (!token) {
      return;
    }

    this.errorMessage.set(null);
    this.authService.resendTwoFactorCode(token).subscribe({
      error: (err) => this.errorMessage.set(extractErrorMessage(err, 'Could not resend code.')),
    });
  }

  cancelTwoFactor(): void {
    this.twoFactorToken.set(null);
    this.twoFactorMethod.set(null);
    this.codeForm.reset();
    this.errorMessage.set(null);
  }
}
