import { Component, OnInit, inject, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleChange, MatSlideToggleModule } from '@angular/material/slide-toggle';

import { BillingService } from '../../../core/services/billing.service';
import { AdminSubscriptionRow } from '../../../core/models/billing.models';
import { extractErrorMessage } from '../../auth/auth-error.util';

@Component({
  selector: 'app-admin-subscriptions',
  imports: [
    MatCardModule,
    MatListModule,
    MatDividerModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatSlideToggleModule,
  ],
  templateUrl: './admin-subscriptions.html',
  styleUrl: './admin-subscriptions.scss',
})
export class AdminSubscriptions implements OnInit {
  private readonly billingService = inject(BillingService);

  readonly rows = signal<AdminSubscriptionRow[]>([]);
  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly savingUserId = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  onFreeAccessToggle(row: AdminSubscriptionRow, event: MatSlideToggleChange): void {
    const isFreeAccess = event.checked;
    this.savingUserId.set(row.userId);

    this.billingService.setFreeAccess(row.userId, isFreeAccess).subscribe({
      next: () => {
        row.isFreeAccess = isFreeAccess;
        this.savingUserId.set(null);
      },
      error: (err) => {
        event.source.checked = !isFreeAccess;
        this.savingUserId.set(null);
        this.errorMessage.set(extractErrorMessage(err, 'Could not update free access.'));
      },
    });
  }

  private load(): void {
    this.loading.set(true);
    this.billingService.listSubscriptions().subscribe({
      next: (rows) => {
        this.rows.set(rows);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Could not load subscriptions.'));
      },
    });
  }
}
