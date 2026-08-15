import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatListModule } from '@angular/material/list';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleChange, MatSlideToggleModule } from '@angular/material/slide-toggle';

import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { VaultSessionService } from '../../core/services/vault-session.service';
import { UserProfile } from '../../core/models/auth.models';
import { MatButtonModule } from '@angular/material/button';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { extractErrorMessage } from '../auth/auth-error.util';

@Component({
  selector: 'app-profile',
  imports: [MatCardModule, MatListModule, MatButtonModule, MatChipsModule, MatProgressSpinnerModule, MatSlideToggleModule],
  templateUrl: './profile.html',
  styleUrls: ['./profile.scss'],
})
export class Profile implements OnInit {
  private readonly http = inject(HttpClient);
  protected readonly authService = inject(AuthService);
  private readonly vaultSession = inject(VaultSessionService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly router = inject(Router);

  readonly users = signal<UserProfile[] | null>(null);
  readonly usersLoading = signal(false);

  readonly twoFactorEnabled = signal(false);
  readonly twoFactorLoading = signal(true);
  readonly twoFactorSaving = signal(false);
  readonly twoFactorError = signal<string | null>(null);

  readonly deleteInProgress = signal(false);
  readonly deleteError = signal<string | null>(null);

  ngOnInit(): void {
    this.loadTwoFactorStatus();
  }

  onTwoFactorToggle(event: MatSlideToggleChange): void {
    const enable = event.checked;
    this.twoFactorSaving.set(true);
    this.twoFactorError.set(null);

    const request = enable ? this.authService.enableTwoFactor() : this.authService.disableTwoFactor();
    request.subscribe({
      next: () => {
        this.twoFactorEnabled.set(enable);
        this.twoFactorSaving.set(false);
      },
      error: () => {
        event.source.checked = !enable;
        this.twoFactorEnabled.set(!enable);
        this.twoFactorSaving.set(false);
        this.twoFactorError.set('Could not update two-factor authentication. Please try again.');
      },
    });
  }

  private loadTwoFactorStatus(): void {
    this.twoFactorLoading.set(true);
    this.authService.getTwoFactorStatus().subscribe({
      next: ({ enabled }) => {
        this.twoFactorEnabled.set(enabled);
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
