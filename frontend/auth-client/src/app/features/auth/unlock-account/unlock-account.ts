import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { AuthService } from '../../../core/services/auth.service';
import { extractErrorMessage } from '../auth-error.util';

@Component({
  selector: 'app-unlock-account',
  imports: [RouterLink, MatCardModule, MatProgressSpinnerModule],
  templateUrl: './unlock-account.html',
  styleUrl: './unlock-account.scss',
})
export class UnlockAccount implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  ngOnInit(): void {
    const userId = this.route.snapshot.queryParamMap.get('userId');
    const token = this.route.snapshot.queryParamMap.get('token');

    if (!userId || !token) {
      this.loading.set(false);
      this.errorMessage.set('This unlock link is invalid or incomplete.');
      return;
    }

    this.authService.unlockAccount(userId, token).subscribe({
      next: (res) => {
        this.loading.set(false);
        this.successMessage.set(res.message);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Could not unlock account.'));
      },
    });
  }
}
