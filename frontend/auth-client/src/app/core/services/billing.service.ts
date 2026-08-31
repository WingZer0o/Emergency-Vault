import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  AdminSubscriptionRow,
  BillingSettings,
  BillingStatus,
  CheckoutSessionResponse,
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

  /** Requests a Stripe Checkout session and redirects the browser to it. */
  startCheckout(): Observable<CheckoutSessionResponse> {
    return this.http.post<CheckoutSessionResponse>(`${this.baseUrl}/checkout-session`, {
      isDesktop: isDesktopApp(),
    });
  }

  /** Requests a Stripe Billing Portal session (update card / cancel) and redirects the browser to it. */
  openPortal(): Observable<CheckoutSessionResponse> {
    return this.http.post<CheckoutSessionResponse>(`${this.baseUrl}/portal-session`, {
      isDesktop: isDesktopApp(),
    });
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
    return this.http.put<void>(`${this.adminBaseUrl}/subscriptions/${userId}/free-access`, { isFreeAccess });
  }
}
