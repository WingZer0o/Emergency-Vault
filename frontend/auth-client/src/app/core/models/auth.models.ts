import { VaultRecoverRequest } from './vault.models';

export interface UserProfile {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
}

export interface AuthResponse {
  requiresTwoFactor: false;
  accessToken: string;
  accessTokenExpiresAt: string;
  user: UserProfile;
}

export type TwoFactorMethodName = 'None' | 'Email' | 'Totp';

export interface TwoFactorChallengeResponse {
  requiresTwoFactor: true;
  twoFactorToken: string;
  method: TwoFactorMethodName;
}

export type LoginResponse = AuthResponse | TwoFactorChallengeResponse;

export interface VerifyTwoFactorRequest {
  twoFactorToken: string;
  code: string;
}

export interface TwoFactorStatus {
  enabled: boolean;
  method: TwoFactorMethodName;
  recoveryCodesRemaining: number;
}

export interface TotpEnrollment {
  secret: string;
  qrCode: string;
}

export interface TwoFactorActivationResponse {
  message: string;
  /** Present only the first time a method is ever enabled - the one moment codes are visible in plaintext. */
  recoveryCodes?: string[];
}

export interface RecoveryCodesResponse {
  codes: string[];
}

export interface VerifyTwoFactorRecoveryRequest {
  twoFactorToken: string;
  recoveryCode: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  userId: string;
  token: string;
  newPassword: string;
  /** Present only when the account has a vault and the user supplied their recovery key during reset - re-wraps the vault's private key under the new password in the same request. */
  vaultRewrap?: VaultRecoverRequest;
}

export interface ApiErrorBody {
  errors?: string[];
  message?: string;
}
