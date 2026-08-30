import { Component, inject, signal } from '@angular/core';

import { DownloadManifest, DownloadPlatform } from '../../core/models/downloads.models';
import { DownloadsService } from '../../core/services/downloads.service';

interface PlatformInfo {
  id: DownloadPlatform;
  label: string;
  fileHint: string;
}

/** Every platform we intend to ship, in display order - independent of which ones the manifest actually has a build for yet. */
const KNOWN_PLATFORMS: readonly PlatformInfo[] = [
  { id: 'windows', label: 'Windows', fileHint: '.exe installer' },
  { id: 'macos', label: 'macOS', fileHint: '.dmg' },
  { id: 'linux', label: 'Linux', fileHint: '.AppImage' },
];

function detectPlatform(): DownloadPlatform | null {
  const ua = navigator.userAgent;
  if (/Windows/i.test(ua)) return 'windows';
  if (/Mac OS X/i.test(ua)) return 'macos';
  if (/Linux/i.test(ua)) return 'linux';
  return null;
}

/**
 * Renders one download button per shipped platform, sourced from the public manifest.json in
 * blob storage (see DownloadsService). New platforms (macOS, Linux) show up automatically the
 * first time build_desktop_installer's counterpart CI job adds a key for them - nothing here
 * needs to change, only KNOWN_PLATFORMS above for the button label/order.
 */
@Component({
  selector: 'app-download-links',
  imports: [],
  templateUrl: './download-links.html',
  styleUrl: './download-links.scss',
})
export class DownloadLinks {
  private readonly downloadsService = inject(DownloadsService);

  protected readonly platforms = KNOWN_PLATFORMS;
  protected readonly manifest = signal<DownloadManifest | null>(null);
  protected readonly loadFailed = signal(false);
  protected readonly detectedPlatform = detectPlatform();

  constructor() {
    this.downloadsService.getManifest().subscribe({
      next: (manifest) => this.manifest.set(manifest),
      error: () => this.loadFailed.set(true),
    });
  }
}
