export type SubscriptionStatus = 'None' | 'Active' | 'PastDue' | 'Canceled';

export interface BillingStatus {
  hasAccess: boolean;
  isFreeAccess: boolean;
  status: SubscriptionStatus;
  currentPeriodEnd: string | null;
  yearlyPriceCents: number;
  currency: string;
  hasStripeCustomer: boolean;
}

export interface CheckoutSessionResponse {
  url: string;
}

export interface BillingSettings {
  yearlyPriceCents: number;
  currency: string;
}

export interface UpdateBillingSettingsRequest {
  yearlyPriceCents: number;
  currency: string;
}

export interface AdminSubscriptionRow {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  status: SubscriptionStatus;
  currentPeriodEnd: string | null;
  isFreeAccess: boolean;
}
