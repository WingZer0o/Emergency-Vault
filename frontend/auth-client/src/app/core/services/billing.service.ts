import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  AdminSubscriptionRow,
  BillingSettings,
  BillingStatus,
  EmbeddedCheckoutSessionResponse,
  PublishableKeyResponse,
  SetupIntentResponse,
  UpdateBillingSettingsRequest,
} from '../models/billing.models';
import { isDesktopApp } from '../utils/desktop.util';

@Injectable({ providedIn: 'root' })
export class BillingService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `/api/billing`;
  private readonly adminBaseUrl = `/api/admin/billing`;

  getStatus(): Observable<BillingStatus> {
    return this.http.get<BillingStatus>(`${this.baseUrl}/status`);
  }

  /** Requests an embedded Stripe Checkout session, mounted client-side via Stripe.js rather than a redirect. */
  startCheckout(): Observable<EmbeddedCheckoutSessionResponse> {
    return this.http.post<EmbeddedCheckoutSessionResponse>(`${this.baseUrl}/checkout-session`, {
      isDesktop: isDesktopApp(),
    });
  }

  getPublishableKey(): Observable<PublishableKeyResponse> {
    return this.http.get<PublishableKeyResponse>(`${this.baseUrl}/publishable-key`);
  }

  /** Requests a SetupIntent for the embedded Payment Element (update card) - see IStripeGateway.CreateSetupIntentAsync for why not Stripe's hosted Billing Portal. */
  createSetupIntent(): Observable<SetupIntentResponse> {
    return this.http.post<SetupIntentResponse>(`${this.baseUrl}/setup-intent`, {});
  }

  setDefaultPaymentMethod(paymentMethodId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/default-payment-method`, { paymentMethodId });
  }

  /** Self-serve cancellation - access is kept through the already-paid period, same as the old portal's "cancel at period end". */
  cancelSubscription(): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/subscription`);
  }

  getSettings(): Observable<BillingSettings> {
    return this.http.get<BillingSettings>(`${this.adminBaseUrl}/settings`);
  }

  updateSettings(request: UpdateBillingSettingsRequest): Observable<BillingSettings> {
    return this.http.put<BillingSettings>(`${this.adminBaseUrl}/settings`, request);
  }

  listSubscriptions(): Observable<AdminSubscriptionRow[]> {
    return this.http.get<AdminSubscriptionRow[]>(`${this.adminBaseUrl}/subscriptions`);
  }

  setFreeAccess(userId: string, isFreeAccess: boolean): Observable<void> {
    return this.http.put<void>(`${this.adminBaseUrl}/subscriptions/${userId}/free-access`, {
      isFreeAccess,
    });
  }
}
