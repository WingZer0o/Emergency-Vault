import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MatCardModule } from '@angular/material/card';
import { MatListModule } from '@angular/material/list';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleChange, MatSlideToggleModule } from '@angular/material/slide-toggle';

import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { UserProfile } from '../../core/models/auth.models';

@Component({
  selector: 'app-dashboard',
  imports: [MatCardModule, MatListModule, MatChipsModule, MatProgressSpinnerModule, MatSlideToggleModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard implements OnInit {
  private readonly http = inject(HttpClient);
  protected readonly authService = inject(AuthService);

  readonly users = signal<UserProfile[] | null>(null);
  readonly usersLoading = signal(false);

  readonly twoFactorEnabled = signal(false);
  readonly twoFactorLoading = signal(true);
  readonly twoFactorSaving = signal(false);
  readonly twoFactorError = signal<string | null>(null);

  ngOnInit(): void {
    if (this.authService.hasRole('Admin')) {
      this.loadUsers();
    }

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

  private loadUsers(): void {
    this.usersLoading.set(true);
    this.http.get<UserProfile[]>(`${environment.apiUrl}/users`).subscribe({
      next: (users) => {
        this.users.set(users);
        this.usersLoading.set(false);
      },
      error: () => this.usersLoading.set(false),
    });
  }
}
