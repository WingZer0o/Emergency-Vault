import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { VaultSharedWithMeDto } from '../../../core/models/vault.models';
import { VaultService } from '../../../core/services/vault.service';
import { extractErrorMessage } from '../../auth/auth-error.util';

@Component({
  selector: 'app-shared-with-me-list',
  imports: [RouterLink, MatCardModule, MatButtonModule, MatListModule, MatProgressSpinnerModule],
  templateUrl: './shared-with-me-list.html',
  styleUrl: './shared-with-me-list.scss',
})
export class SharedWithMeList implements OnInit {
  private readonly vaultService = inject(VaultService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly shares = signal<VaultSharedWithMeDto[]>([]);

  ngOnInit(): void {
    this.vaultService.getSharedWithMe().subscribe({
      next: (shares) => {
        this.shares.set(shares);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Could not load vaults shared with you.'));
      },
    });
  }

  open(ownerUserId: string): void {
    this.router.navigateByUrl(`/vault/shared-with-me/${ownerUserId}`);
  }
}
