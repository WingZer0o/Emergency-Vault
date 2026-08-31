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

/** Client secret Stripe.js needs to mount the embedded Checkout iframe - never a URL. */
export interface EmbeddedCheckoutSessionResponse {
  clientSecret: string;
}

/** Client secret Stripe.js needs to mount the embedded Payment Element for updating the card on file. */
export interface SetupIntentResponse {
  clientSecret: string;
}

export interface PublishableKeyResponse {
  publishableKey: string;
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
