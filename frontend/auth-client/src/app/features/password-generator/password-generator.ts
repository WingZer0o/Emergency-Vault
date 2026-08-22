import { Component, computed, effect, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule, MatCheckboxChange } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatSliderModule } from '@angular/material/slider';
import { MatTooltipModule } from '@angular/material/tooltip';

import {
  DEFAULT_PASSWORD_OPTIONS,
  PasswordGeneratorOptions,
  estimatePasswordStrength,
  generatePassword,
  getAlphabetSize,
} from '../../core/crypto/password-generator.util';

const COPIED_FEEDBACK_MS = 2000;

@Component({
  selector: 'app-password-generator',
  imports: [MatButtonModule, MatIconModule, MatSliderModule, MatCheckboxModule, MatTooltipModule],
  templateUrl: './password-generator.html',
  styleUrl: './password-generator.scss',
})
export class PasswordGenerator {
  readonly length = signal(DEFAULT_PASSWORD_OPTIONS.length);
  readonly uppercase = signal(DEFAULT_PASSWORD_OPTIONS.uppercase);
  readonly lowercase = signal(DEFAULT_PASSWORD_OPTIONS.lowercase);
  readonly numbers = signal(DEFAULT_PASSWORD_OPTIONS.numbers);
  readonly symbols = signal(DEFAULT_PASSWORD_OPTIONS.symbols);
  readonly excludeAmbiguous = signal(DEFAULT_PASSWORD_OPTIONS.excludeAmbiguous);

  readonly password = signal('');
  readonly validationError = signal<string | null>(null);
  readonly copied = signal(false);

  readonly strength = computed(() => {
    const password = this.password();
    if (!password) {
      return null;
    }
    return estimatePasswordStrength(password, getAlphabetSize(this.currentOptions()));
  });

  private copiedTimeout?: ReturnType<typeof setTimeout>;

  constructor() {
    // Regenerate whenever an option changes, so the preview on screen always matches the controls.
    effect(() => this.regenerate(this.currentOptions()));
  }

  onLengthChange(value: number): void {
    this.length.set(value);
  }

  onToggle(setting: 'uppercase' | 'lowercase' | 'numbers' | 'symbols' | 'excludeAmbiguous', event: MatCheckboxChange): void {
    this[setting].set(event.checked);
  }

  regenerate(options: PasswordGeneratorOptions = this.currentOptions()): void {
    try {
      this.password.set(generatePassword(options));
      this.validationError.set(null);
    } catch (err) {
      this.password.set('');
      this.validationError.set(err instanceof Error ? err.message : 'Could not generate password.');
    }
  }

  async copyToClipboard(): Promise<void> {
    if (!this.password()) {
      return;
    }

    await navigator.clipboard.writeText(this.password());
    this.copied.set(true);
    clearTimeout(this.copiedTimeout);
    this.copiedTimeout = setTimeout(() => this.copied.set(false), COPIED_FEEDBACK_MS);
  }

  private currentOptions(): PasswordGeneratorOptions {
    return {
      length: this.length(),
      uppercase: this.uppercase(),
      lowercase: this.lowercase(),
      numbers: this.numbers(),
      symbols: this.symbols(),
      excludeAmbiguous: this.excludeAmbiguous(),
    };
  }
}
