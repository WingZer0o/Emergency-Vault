import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  CreateVaultItemRequest,
  CreateVaultShareRequest,
  ResendSharedVaultAccessRequest,
  SharedVaultAccessChallengeDto,
  UpdateVaultItemRequest,
  VaultItemDto,
  VaultRecoverRequest,
  VaultResetRecoveryBundleDto,
  VaultSetupRequest,
  VaultShareDto,
  VaultSharedWithMeDto,
  VaultStatusDto,
  VaultUnlockBundleDto,
  VaultUserLookupDto,
  VerifySharedVaultAccessRequest,
  VerifyVaultShareRequest,
} from '../models/vault.models';

/**
 * Pure HTTP layer for the vault API - every method is a direct request/response mapping, with no
 * crypto or key state of its own. VaultSessionService is where the actual key material lives.
 */
@Injectable({ providedIn: 'root' })
export class VaultService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/vault`;

  setup(request: VaultSetupRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/setup`, request);
  }

  getStatus(): Observable<VaultStatusDto> {
    return this.http.get<VaultStatusDto>(`${this.baseUrl}/status`);
  }

  getUnlockBundle(): Observable<VaultUnlockBundleDto> {
    return this.http.get<VaultUnlockBundleDto>(`${this.baseUrl}/unlock-bundle`);
  }

  recover(request: VaultRecoverRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/recover`, request);
  }

  getResetRecoveryBundle(userId: string, token: string): Observable<VaultResetRecoveryBundleDto> {
    return this.http.get<VaultResetRecoveryBundleDto>(`${this.baseUrl}/reset-recovery-bundle`, {
      params: { userId, token },
    });
  }

  createItem(request: CreateVaultItemRequest): Observable<VaultItemDto> {
    return this.http.post<VaultItemDto>(`${this.baseUrl}/items`, request);
  }

  getItems(): Observable<VaultItemDto[]> {
    return this.http.get<VaultItemDto[]>(`${this.baseUrl}/items`);
  }

  getItem(id: string): Observable<VaultItemDto> {
    return this.http.get<VaultItemDto>(`${this.baseUrl}/items/${id}`);
  }

  updateItem(id: string, request: UpdateVaultItemRequest): Observable<VaultItemDto> {
    return this.http.put<VaultItemDto>(`${this.baseUrl}/items/${id}`, request);
  }

  deleteItem(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/items/${id}`);
  }

  lookupUser(email: string): Observable<VaultUserLookupDto> {
    return this.http.get<VaultUserLookupDto>(`${this.baseUrl}/lookup`, { params: { email } });
  }

  createShare(request: CreateVaultShareRequest): Observable<VaultShareDto> {
    return this.http.post<VaultShareDto>(`${this.baseUrl}/shares`, request);
  }

  getShares(): Observable<VaultShareDto[]> {
    return this.http.get<VaultShareDto[]>(`${this.baseUrl}/shares`);
  }

  revokeShare(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/shares/${id}`);
  }

  getSharedWithMe(): Observable<VaultSharedWithMeDto[]> {
    return this.http.get<VaultSharedWithMeDto[]>(`${this.baseUrl}/shared-with-me`);
  }

  getSharedItems(ownerUserId: string): Observable<VaultItemDto[]> {
    return this.http.get<VaultItemDto[]>(`${this.baseUrl}/shared-with-me/${ownerUserId}/items`);
  }

  verifyShare(id: string, request: VerifyVaultShareRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/shares/${id}/verify`, request);
  }

  resendShareCode(id: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/shares/${id}/resend-code`, {});
  }

  requestSharedVaultAccess(ownerUserId: string): Observable<SharedVaultAccessChallengeDto> {
    return this.http.post<SharedVaultAccessChallengeDto>(`${this.baseUrl}/shared-with-me/${ownerUserId}/access-challenge`, {});
  }

  verifySharedVaultAccess(request: VerifySharedVaultAccessRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/shared-with-me/access/verify`, request);
  }

  resendSharedVaultAccessCode(request: ResendSharedVaultAccessRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/shared-with-me/access/resend`, request);
  }
}
