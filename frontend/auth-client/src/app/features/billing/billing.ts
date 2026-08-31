import { DatePipe } from '@angular/common';
import { Component, ElementRef, OnDestroy, OnInit, ViewChild, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Stripe, StripeElements, StripeEmbeddedCheckout, loadStripe } from '@stripe/stripe-js';
import { firstValueFrom } from 'rxjs';

import { BillingService } from '../../core/services/billing.service';
import { BillingStatus } from '../../core/models/billing.models';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { extractErrorMessage } from '../auth/auth-error.util';

@Component({
  selector: 'app-billing',
  imports: [DatePipe, MatButtonModule, MatProgressSpinnerModule],
  templateUrl: './billing.html',
  styleUrl: './billing.scss',
})
export class Billing implements OnInit, OnDestroy {
  private readonly billingService = inject(BillingService);
  private readonly route = inject(ActivatedRoute);
  private readonly confirmDialog = inject(ConfirmDialogService);

  @ViewChild('checkoutContainer') private checkoutContainerRef?: ElementRef<HTMLDivElement>;
  @ViewChild('paymentMethodContainer')
  private paymentMethodContainerRef?: ElementRef<HTMLDivElement>;

  readonly status = signal<BillingStatus | null>(null);
  readonly loading = signal(true);
  readonly starting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly checkoutOpen = signal(false);

  readonly justSucceeded = signal(false);
  readonly justCanceled = signal(false);

  readonly managingPaymentMethod = signal(false);
  readonly updatingPaymentMethod = signal(false);
  readonly paymentMethodUpdated = signal(false);
  readonly canceling = signal(false);

  // Cached across subscribe()/updatePaymentMethod() calls - the publishable key never changes
  // mid-session, so there's no reason to load Stripe.js more than once.
  private stripePromise: Promise<Stripe | null> | null = null;
  private embeddedCheckout: StripeEmbeddedCheckout | null = null;
  private paymentElements: StripeElements | null = null;

  ngOnInit(): void {
    // Only reached via the return_url fallback for a payment method that needs an out-of-band
    // redirect (e.g. 3DS) - the common case completes in-page via onComplete and never navigates
    // here. See BillingService.CreateCheckoutSessionAsync/StripeGateway's RedirectOnCompletion comment.
    this.justSucceeded.set(this.route.snapshot.queryParamMap.get('success') === '1');
    this.justCanceled.set(this.route.snapshot.queryParamMap.get('canceled') === '1');
    this.loadStatus();
  }

  ngOnDestroy(): void {
    this.embeddedCheckout?.destroy();
    this.paymentElements?.getElement('payment')?.destroy();
  }

  subscribe(): void {
    this.starting.set(true);
    this.errorMessage.set(null);

    this.billingService.startCheckout().subscribe({
      next: async ({ clientSecret }) => {
        try {
          const stripe = await this.loadStripeInstance();
          if (!stripe) {
            throw new Error('Stripe.js failed to load.');
          }

          this.embeddedCheckout = await stripe.createEmbeddedCheckoutPage({
            clientSecret,
            onComplete: () => this.onCheckoutComplete(),
          });
          this.checkoutOpen.set(true);
          this.embeddedCheckout.mount(this.checkoutContainerRef!.nativeElement);
        } catch {
          this.errorMessage.set('Could not start checkout. Please try again.');
        } finally {
          this.starting.set(false);
        }
      },
      error: (err) => {
        this.starting.set(false);
        this.errorMessage.set(
          extractErrorMessage(err, 'Could not start checkout. Please try again.'),
        );
      },
    });
  }

  /** Our own back/cancel action - embedded Checkout has no page chrome of its own to navigate away with. */
  cancelCheckout(): void {
    this.embeddedCheckout?.destroy();
    this.embeddedCheckout = null;
    this.checkoutOpen.set(false);
    this.justCanceled.set(true);
  }

  updatePaymentMethod(): void {
    this.errorMessage.set(null);
    this.paymentMethodUpdated.set(false);
    this.updatingPaymentMethod.set(true);

    this.billingService.createSetupIntent().subscribe({
      next: async ({ clientSecret }) => {
        try {
          const stripe = await this.loadStripeInstance();
          if (!stripe) {
            throw new Error('Stripe.js failed to load.');
          }

          this.paymentElements = stripe.elements({ clientSecret });
          this.paymentElements
            .create('payment')
            .mount(this.paymentMethodContainerRef!.nativeElement);
          this.managingPaymentMethod.set(true);
        } catch {
          this.errorMessage.set('Could not load the payment form. Please try again.');
        } finally {
          this.updatingPaymentMethod.set(false);
        }
      },
      error: (err) => {
        this.updatingPaymentMethod.set(false);
        this.errorMessage.set(
          extractErrorMessage(err, 'Could not load the payment form. Please try again.'),
        );
      },
    });
  }

  cancelPaymentMethodUpdate(): void {
    this.paymentElements?.getElement('payment')?.destroy();
    this.paymentElements = null;
    this.managingPaymentMethod.set(false);
  }

  async confirmPaymentMethod(): Promise<void> {
    const stripe = await this.loadStripeInstance();
    if (!stripe || !this.paymentElements) {
      return;
    }

    this.updatingPaymentMethod.set(true);
    this.errorMessage.set(null);

    const result = await stripe.confirmSetup({
      elements: this.paymentElements,
      redirect: 'if_required',
    });
    if (result.error) {
      this.updatingPaymentMethod.set(false);
      this.errorMessage.set(
        result.error.message ?? 'Could not save the new card. Please try again.',
      );
      return;
    }

    const paymentMethod = result.setupIntent.payment_method;
    const paymentMethodId = typeof paymentMethod === 'string' ? paymentMethod : paymentMethod?.id;
    if (!paymentMethodId) {
      this.updatingPaymentMethod.set(false);
      this.errorMessage.set('Could not save the new card. Please try again.');
      return;
    }

    this.billingService.setDefaultPaymentMethod(paymentMethodId).subscribe({
      next: () => {
        this.paymentElements?.getElement('payment')?.destroy();
        this.paymentElements = null;
        this.updatingPaymentMethod.set(false);
        this.managingPaymentMethod.set(false);
        this.paymentMethodUpdated.set(true);
      },
      error: (err) => {
        this.updatingPaymentMethod.set(false);
        this.errorMessage.set(
          extractErrorMessage(
            err,
            'Card saved with Stripe, but could not set it as your default. Please try again.',
          ),
        );
      },
    });
  }

  cancelSubscription(): void {
    this.confirmDialog
      .confirm({
        title: 'Cancel membership',
        message:
          'Your membership stays active through the end of the current billing period, then will not renew.',
        confirmText: 'Cancel membership',
        danger: true,
      })
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }

        this.canceling.set(true);
        this.errorMessage.set(null);
        this.billingService.cancelSubscription().subscribe({
          next: () => {
            this.canceling.set(false);
            this.loadStatus();
          },
          error: (err) => {
            this.canceling.set(false);
            this.errorMessage.set(
              extractErrorMessage(err, 'Could not cancel your membership. Please try again.'),
            );
          },
        });
      });
  }

  refresh(): void {
    this.loadStatus();
  }

  formatPrice(cents: number, currency: string): string {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: currency.toUpperCase(),
    }).format(cents / 100);
  }

  private onCheckoutComplete(): void {
    this.embeddedCheckout?.destroy();
    this.embeddedCheckout = null;
    this.checkoutOpen.set(false);
    this.justSucceeded.set(true);
    this.loadStatus();
  }

  private loadStripeInstance(): Promise<Stripe | null> {
    if (!this.stripePromise) {
      this.stripePromise = firstValueFrom(this.billingService.getPublishableKey()).then(
        ({ publishableKey }) => loadStripe(publishableKey),
      );
    }
    return this.stripePromise;
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
