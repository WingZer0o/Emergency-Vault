import { Component, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

import { ClipboardService } from '../../core/services/clipboard.service';
import { isDesktopApp } from '../../core/utils/desktop.util';

const COPIED_FEEDBACK_MS = 2000;

/**
 * Icon button that copies `value` to the clipboard, with the "copied" checkmark/tooltip swap and
 * (in the desktop app) the "clears in 30s" hint all owned in one place - every call site was
 * hand-rolling this same timer/icon-swap logic before.
 */
@Component({
  selector: 'app-copy-button',
  imports: [MatButtonModule, MatIconModule, MatTooltipModule],
  templateUrl: './copy-button.html',
})
export class CopyButton {
  private readonly clipboard = inject(ClipboardService);
  private readonly isDesktop = isDesktopApp();
  private copiedTimeout?: ReturnType<typeof setTimeout>;

  readonly value = input.required<string>();
  readonly disabled = input(false);
  /** Base label, used for both aria-label and the pre-copy tooltip text. */
  readonly label = input('Copy value');

  readonly copied = signal(false);

  get tooltip(): string {
    if (!this.copied()) {
      return this.label();
    }
    return this.isDesktop ? 'Copied - clears in 30s' : 'Copied';
  }

  async copy(): Promise<void> {
    if (this.disabled() || !this.value()) {
      return;
    }

    await this.clipboard.copy(this.value());
    this.copied.set(true);
    clearTimeout(this.copiedTimeout);
    this.copiedTimeout = setTimeout(() => this.copied.set(false), COPIED_FEEDBACK_MS);
  }
}
