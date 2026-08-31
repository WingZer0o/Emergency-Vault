import { TestBed } from '@angular/core/testing';

import { ClipboardService } from './clipboard.service';

function stubClipboard(): { get: () => string } {
  let value = '';
  Object.defineProperty(navigator, 'clipboard', {
    value: {
      writeText: vi.fn(async (text: string) => {
        value = text;
      }),
      readText: vi.fn(async () => value),
    },
    configurable: true,
  });
  return { get: () => value };
}

/**
 * Stubs the native bridge deno desktop's server.ts exposes via win.bind() - see
 * core/utils/desktop.util.ts. Distinct from navigator.clipboard on purpose: asserting the two
 * never get mixed up is the whole point of these bridge-specific tests (this bridge exists
 * precisely because navigator.clipboard silently fails for the delayed auto-clear - see the
 * ClipboardService class comment).
 */
function stubDesktopBindings(): { get: () => string } {
  let value = '';
  (window as unknown as { bindings?: unknown }).bindings = {
    readClipboardText: vi.fn(async () => value),
    writeClipboardText: vi.fn(async (text: string) => {
      value = text;
    }),
    // A dedicated clear binding, not write(''): server.ts clears via its own literal command
    // rather than piping an empty value through, since an env var explicitly set to '' doesn't
    // reliably survive the trip to the child process on Windows - see server.ts's comment.
    clearClipboardText: vi.fn(async () => {
      value = '';
    }),
  };
  return { get: () => value };
}

describe('ClipboardService', () => {
  afterEach(() => {
    delete (window as unknown as { __EV_DESKTOP__?: boolean }).__EV_DESKTOP__;
    delete (window as unknown as { bindings?: unknown }).bindings;
  });

  it('writes the value to the clipboard', async () => {
    const clipboard = stubClipboard();
    const service = TestBed.inject(ClipboardService);

    await service.copy('hunter2');

    expect(clipboard.get()).toBe('hunter2');
  });

  it('does not schedule an auto-clear on the web build', async () => {
    const clipboard = stubClipboard();
    const service = TestBed.inject(ClipboardService);

    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    try {
      await service.copy('hunter2');
      await vi.advanceTimersByTimeAsync(service.autoClearMs);
      expect(clipboard.get()).toBe('hunter2');
    } finally {
      vi.useRealTimers();
    }
  });

  it('clears the clipboard after 30s in the desktop app, if it still holds what we copied', async () => {
    (window as unknown as { __EV_DESKTOP__?: boolean }).__EV_DESKTOP__ = true;
    const clipboard = stubClipboard();
    const service = TestBed.inject(ClipboardService);

    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    try {
      await service.copy('hunter2');
      expect(clipboard.get()).toBe('hunter2');

      await vi.advanceTimersByTimeAsync(service.autoClearMs);

      expect(clipboard.get()).toBe('');
    } finally {
      vi.useRealTimers();
    }
  });

  it('does not clobber a value the user copied afterward', async () => {
    (window as unknown as { __EV_DESKTOP__?: boolean }).__EV_DESKTOP__ = true;
    const clipboard = stubClipboard();
    const service = TestBed.inject(ClipboardService);

    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    try {
      await service.copy('hunter2');
      await navigator.clipboard.writeText('something the user copied afterward');

      await vi.advanceTimersByTimeAsync(service.autoClearMs);

      expect(clipboard.get()).toBe('something the user copied afterward');
    } finally {
      vi.useRealTimers();
    }
  });

  it('a newer copy supersedes the older one\'s pending clear', async () => {
    (window as unknown as { __EV_DESKTOP__?: boolean }).__EV_DESKTOP__ = true;
    const clipboard = stubClipboard();
    const service = TestBed.inject(ClipboardService);

    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    try {
      await service.copy('first');
      await vi.advanceTimersByTimeAsync(service.autoClearMs / 2);
      await service.copy('second');

      // The first copy's timer would fire right about now - it must not clear 'second'.
      await vi.advanceTimersByTimeAsync(service.autoClearMs / 2);
      expect(clipboard.get()).toBe('second');

      // The second copy's own timer, started when it ran, fires here.
      await vi.advanceTimersByTimeAsync(service.autoClearMs / 2);
      expect(clipboard.get()).toBe('');
    } finally {
      vi.useRealTimers();
    }
  });

  it('uses the native bridge, not navigator.clipboard, when present in the desktop app', async () => {
    (window as unknown as { __EV_DESKTOP__?: boolean }).__EV_DESKTOP__ = true;
    stubClipboard();
    const bridge = stubDesktopBindings();
    const service = TestBed.inject(ClipboardService);

    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    try {
      await service.copy('hunter2');
      expect(bridge.get()).toBe('hunter2');
      expect(navigator.clipboard.writeText).not.toHaveBeenCalled();

      // This is the actual bug: navigator.clipboard.readText()/writeText() silently no-op here
      // (no live user gesture at this point), so if the clear ever fell through to them instead
      // of the bridge, this assertion would catch it going back to that broken state.
      await vi.advanceTimersByTimeAsync(service.autoClearMs);

      expect(bridge.get()).toBe('');
      expect(navigator.clipboard.readText).not.toHaveBeenCalled();
      expect(navigator.clipboard.writeText).not.toHaveBeenCalled();
    } finally {
      vi.useRealTimers();
    }
  });
});
