import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, finalize, map, of, tap } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  AuthResponse,
  ForgotPasswordRequest,
  LoginRequest,
  LoginResponse,
  RegisterRequest,
  ResetPasswordRequest,
  UserProfile,
  VerifyTwoFactorRequest,
} from '../models/auth.models';

/**
 * Access token lives only in memory (a signal) - never localStorage - to limit XSS blast radius.
 * The refresh token is an httpOnly cookie the browser attaches automatically; this service never
 * sees its value. Session state is rebuilt on page load via tryRestoreSession() (see app.config.ts).
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/auth`;

  private readonly accessTokenSig = signal<string | null>(null);
  private readonly currentUserSig = signal<UserProfile | null>(null);

  readonly currentUser = this.currentUserSig.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUserSig() !== null);

  getAccessToken(): string | null {
    return this.accessTokenSig();
  }

  hasRole(role: string): boolean {
    return this.currentUserSig()?.roles.includes(role) ?? false;
  }

  register(request: RegisterRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/register`, request);
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/login`, request).pipe(
      tap((response) => {
        if (!response.requiresTwoFactor) {
          this.setSession(response);
        }
      }),
    );
  }

  verifyTwoFactor(request: VerifyTwoFactorRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/login/two-factor`, request)
      .pipe(tap((response) => this.setSession(response)));
  }

  resendTwoFactorCode(twoFactorToken: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/login/two-factor/resend`, { twoFactorToken });
  }

  getTwoFactorStatus(): Observable<{ enabled: boolean }> {
    return this.http.get<{ enabled: boolean }>(`${this.baseUrl}/two-factor/status`);
  }

  enableTwoFactor(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/two-factor/enable`, {});
  }

  disableTwoFactor(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/two-factor/disable`, {});
  }

  refresh(): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/refresh`, {})
      .pipe(tap((response) => this.setSession(response)));
  }

  logout(): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/logout`, {}).pipe(finalize(() => this.clearSession()));
  }

  confirmEmail(userId: string, token: string): Observable<{ message: string }> {
    return this.http.get<{ message: string }>(`${this.baseUrl}/confirm-email`, { params: { userId, token } });
  }

  forgotPassword(request: ForgotPasswordRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/forgot-password`, request);
  }

  resetPassword(request: ResetPasswordRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/reset-password`, request);
  }

  /** Called once at app bootstrap to silently restore a session from the refresh cookie, if any. */
  tryRestoreSession(): Observable<boolean> {
    return this.refresh().pipe(
      map(() => true),
      catchError(() => {
        this.clearSession();
        return of(false);
      }),
    );
  }

  private setSession(response: AuthResponse): void {
    this.accessTokenSig.set(response.accessToken);
    this.currentUserSig.set(response.user);
  }

  private clearSession(): void {
    this.accessTokenSig.set(null);
    this.currentUserSig.set(null);
  }
}
