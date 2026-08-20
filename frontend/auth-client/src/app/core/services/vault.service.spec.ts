import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { environment } from '../../../environments/environment';
import { VaultService } from './vault.service';

describe('VaultService', () => {
  let service: VaultService;
  let httpMock: HttpTestingController;
  const baseUrl = `/api/vault`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(VaultService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('posts to /setup with the request body', () => {
    const request = {
      publicKey: 'pub',
      kdfSalt: 'salt',
      kdfIterations: 600_000,
      privateKeyWrappedByMasterKey: 'a',
      privateKeyWrappedByRecoveryKey: 'b',
      selfWrappedUvk: 'c',
      mlKemPublicKey: 'd',
      mlKemSecretKeyWrappedByMasterKey: 'e',
      mlKemSecretKeyWrappedByRecoveryKey: 'f',
    };

    service.setup(request).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/setup`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ message: 'ok' });
  });

  it('gets /status', () => {
    service.getStatus().subscribe();

    const req = httpMock.expectOne(`${baseUrl}/status`);
    expect(req.request.method).toBe('GET');
    req.flush({ hasVault: true });
  });

  it('gets /lookup with the email as a query param', () => {
    service.lookupUser('holder@example.com').subscribe();

    const req = httpMock.expectOne((r) => r.url === `${baseUrl}/lookup` && r.params.get('email') === 'holder@example.com');
    expect(req.request.method).toBe('GET');
    req.flush({ userId: '1', firstName: 'A', lastName: 'B', publicKey: 'pub' });
  });

  it('deletes /shares/:id to revoke', () => {
    service.revokeShare('share-1').subscribe();

    const req = httpMock.expectOne(`${baseUrl}/shares/share-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('gets /shared-with-me/:ownerId/items', () => {
    service.getSharedItems('owner-1').subscribe();

    const req = httpMock.expectOne(`${baseUrl}/shared-with-me/owner-1/items`);
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });
});
