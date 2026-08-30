/** Desktop platforms a build could target. Add an id here once a platform actually ships. */
export type DownloadPlatform = 'windows' | 'macos' | 'linux';

export interface DownloadEntry {
  version: string;
  url: string;
}

/** Shape of the public manifest.json in blob storage - only ships entries for platforms that have a release. */
export type DownloadManifest = Partial<Record<DownloadPlatform, DownloadEntry>>;
