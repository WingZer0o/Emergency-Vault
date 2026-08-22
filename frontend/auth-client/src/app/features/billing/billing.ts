import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { BillingService } from '../../core/services/billing.service';
import { BillingStatus } from '../../core/models/billing.models';
import { extractErrorMessage } from '../auth/auth-error.util';

@Component({
  selector: 'app-billing',
  imports: [DatePipe, MatButtonModule, MatProgressSpinnerModule],
  templateUrl: './billing.html',
  styleUrl: './billing.scss',
})
export class Billing implements OnInit {
  private readonly billingService = inject(BillingService);
  private readonly route = inject(ActivatedRoute);

  readonly status = signal<BillingStatus | null>(null);
  readonly loading = signal(true);
  readonly starting = signal(false);
  readonly openingPortal = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly justSucceeded = signal(false);
  readonly justCanceled = signal(false);

  ngOnInit(): void {
    this.justSucceeded.set(this.route.snapshot.queryParamMap.get('success') === '1');
    this.justCanceled.set(this.route.snapshot.queryParamMap.get('canceled') === '1');
    this.loadStatus();
  }

  subscribe(): void {
    this.starting.set(true);
    this.errorMessage.set(null);

    this.billingService.startCheckout().subscribe({
      next: ({ url }) => {
        window.location.href = url;
      },
      error: (err) => {
        this.starting.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Could not start checkout. Please try again.'));
      },
    });
  }

  openPortal(): void {
    this.openingPortal.set(true);
    this.errorMessage.set(null);

    this.billingService.openPortal().subscribe({
      next: ({ url }) => {
        window.location.href = url;
      },
      error: (err) => {
        this.openingPortal.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Could not open the billing portal. Please try again.'));
      },
    });
  }

  refresh(): void {
    this.loadStatus();
  }

  formatPrice(cents: number, currency: string): string {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency: currency.toUpperCase() }).format(cents / 100);
  }

  private loadStatus(): void {
    this.loading.set(true);
    this.billingService.getStatus().subscribe({
      next: (status) => {
        this.status.set(status);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Could not load billing status.'));
      },
    });
  }
}
