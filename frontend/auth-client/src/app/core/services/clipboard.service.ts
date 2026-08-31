import { Injectable } from '@angular/core';

import { getDesktopClipboardBridge, isDesktopApp } from '../utils/desktop.util';

const AUTO_CLEAR_MS = 30_000;

/**
 * Copies sensitive values (passwords, vault field values) to the clipboard. In the desktop app
 * only, auto-clears the clipboard ~30s later - the same behavior 1Password/Bitwarden ship on
 * desktop. Not offered on the web build: there's no reliable way to run code after a browser tab
 * loses focus, is backgrounded, or is closed, so a promised auto-clear there would be a broken
 * promise more often than not - worse than not promising it at all.
 *
 * The clear step goes through the native bridge (getDesktopClipboardBridge) rather than
 * navigator.clipboard when running in the desktop app: the web Clipboard API requires a live user
 * gesture, which the initial copy() call has (fired from a click) but the delayed auto-clear does
 * not, so navigator.clipboard.readText()/writeText() would silently no-op here - see server.ts.
 */
@Injectable({ providedIn: 'root' })
export class ClipboardService {
  private clearTimer?: ReturnType<typeof setTimeout>;
  private pendingValue: string | null = null;

  async copy(value: string): Promise<void> {
    const bridge = getDesktopClipboardBridge();
    if (bridge) {
      try {
        await bridge.write(value);
      } catch {
        // Falls back to the web API on a bridge failure - still has the user gesture from
        // whatever click triggered copy(), so it should work even though the bridge didn't.
        await navigator.clipboard.writeText(value);
      }
    } else {
      await navigator.clipboard.writeText(value);
    }

    if (!isDesktopApp()) {
      return;
    }

    this.pendingValue = value;
    clearTimeout(this.clearTimer);
    this.clearTimer = setTimeout(() => {
      this.clearIfStillOurs(value).catch(() => {
        // Best-effort - see clearIfStillOurs's own try/catch for the expected failure modes.
      });
    }, AUTO_CLEAR_MS);
  }

  get autoClearMs(): number {
    return AUTO_CLEAR_MS;
  }

  private async clearIfStillOurs(value: string): Promise<void> {
    // A newer copy() call already replaced what we're tracking - its own timer owns clearing now.
    if (this.pendingValue !== value) {
      return;
    }

    const bridge = getDesktopClipboardBridge();

    try {
      // Best-effort read-back: only clear if the clipboard still holds exactly what we put there,
      // so we never stomp on something the user copied from elsewhere in the meantime. If clipboard
      // read is unavailable or denied, skip clearing rather than risk that.
      const current = bridge ? await bridge.read() : await navigator.clipboard.readText();
      if (current !== value) {
        return;
      }
    } catch {
      return;
    }

    if (bridge) {
      await bridge.clear();
    } else {
      await navigator.clipboard.writeText('');
    }
    this.pendingValue = null;
  }
}
