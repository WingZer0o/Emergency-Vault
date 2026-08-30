import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { DownloadManifest } from '../models/downloads.models';

@Injectable({ providedIn: 'root' })
export class DownloadsService {
  private readonly http = inject(HttpClient);

  // Public read-only blob storage, independent of environment/apiUrl - the same manifest serves
  // local dev, Docker and Azure alike. The CI release pipeline (build_desktop_installer in
  // deploy.yml) is what keeps this up to date, one platform key at a time.
  private readonly manifestUrl = 'https://evdownloads.blob.core.windows.net/downloads/manifest.json';

  getManifest(): Observable<DownloadManifest> {
    return this.http.get<DownloadManifest>(this.manifestUrl);
  }
}
