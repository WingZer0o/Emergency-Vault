import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { BillingService } from '../../../core/services/billing.service';
import { extractErrorMessage } from '../../auth/auth-error.util';

@Component({
  selector: 'app-billing-settings',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './billing-settings.html',
  styleUrl: './billing-settings.scss',
})
export class BillingSettings implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly billingService = inject(BillingService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    yearlyPriceDollars: [50, [Validators.required, Validators.min(1)]],
    currency: ['usd', [Validators.required]],
  });

  ngOnInit(): void {
    this.billingService.getSettings().subscribe({
      next: (settings) => {
        this.form.patchValue({
          yearlyPriceDollars: settings.yearlyPriceCents / 100,
          currency: settings.currency,
        });
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Could not load billing settings.'));
      },
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const { yearlyPriceDollars, currency } = this.form.getRawValue();
    this.billingService
      .updateSettings({ yearlyPriceCents: Math.round(yearlyPriceDollars * 100), currency })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.successMessage.set('Billing settings updated.');
        },
        error: (err) => {
          this.saving.set(false);
          this.errorMessage.set(extractErrorMessage(err, 'Could not save billing settings.'));
        },
      });
  }
}
