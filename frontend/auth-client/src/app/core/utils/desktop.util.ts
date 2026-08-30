/**
 * True only inside the compiled desktop app (deno desktop's server.ts injects this flag into
 * index.html - see server.ts). Never true for the deployed website (Static Web App/nginx serve
 * the prebuilt dist/ directly, bypassing server.ts) or for `ng serve`/Docker dev.
 */
export function isDesktopApp(): boolean {
  return !!(window as unknown as { __EV_DESKTOP__?: boolean }).__EV_DESKTOP__;
}
