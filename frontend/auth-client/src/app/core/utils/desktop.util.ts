/**
 * True only inside the compiled desktop app (deno desktop's server.ts injects this flag into
 * index.html - see server.ts). Never true for the deployed website (Static Web App/nginx serve
 * the prebuilt dist/ directly, bypassing server.ts) or for `ng serve`/Docker dev.
 */
export function isDesktopApp(): boolean {
  return !!(window as unknown as { __EV_DESKTOP__?: boolean }).__EV_DESKTOP__;
}

interface DesktopBindings {
  readClipboardText?: () => Promise<string>;
  writeClipboardText?: (text: string) => Promise<void>;
  clearClipboardText?: () => Promise<void>;
}

export interface DesktopClipboardBridge {
  read(): Promise<string>;
  write(text: string): Promise<void>;
  clear(): Promise<void>;
}

/**
 * The native clipboard bridge `deno desktop`'s server.ts exposes via `win.bind(...)` - see the
 * comment above readClipboardText/writeClipboardText/clearClipboardText in server.ts for why this
 * exists instead of just using navigator.clipboard everywhere (the web Clipboard API silently
 * fails without a live user gesture, which a delayed auto-clear doesn't have). Clearing is its own
 * bound function rather than write(''), because an empty value doesn't survive the trip through
 * the child process the same way a real one does (see server.ts). Null on the web build, and also
 * null on a desktop build old enough not to expose these bindings yet (defensive - keeps
 * ClipboardService falling back to navigator.clipboard rather than throwing).
 */
export function getDesktopClipboardBridge(): DesktopClipboardBridge | null {
  if (!isDesktopApp()) {
    return null;
  }

  const bindings = (window as unknown as { bindings?: DesktopBindings }).bindings;
  if (!bindings?.readClipboardText || !bindings.writeClipboardText || !bindings.clearClipboardText) {
    return null;
  }

  return {
    read: bindings.readClipboardText,
    write: bindings.writeClipboardText,
    clear: bindings.clearClipboardText,
  };
}
