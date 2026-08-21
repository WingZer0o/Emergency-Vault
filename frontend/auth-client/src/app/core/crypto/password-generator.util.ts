// Cryptographically secure password generation. Every character is drawn via rejection sampling
// against crypto.getRandomValues, so selection stays uniform across whichever character sets are
// enabled (a plain `randomByte % pool.length` would bias toward the low end of the pool).

const LOWERCASE = 'abcdefghijklmnopqrstuvwxyz';
const UPPERCASE = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
const NUMBERS = '0123456789';
const SYMBOLS = '!@#$%^&*()_+-=[]{}|;:,.<>?';

/** Characters that are easy to mis-key or misread in most fonts (1/l/I, 0/O). */
const AMBIGUOUS_CHARS = '01lIO';

export interface PasswordGeneratorOptions {
  length: number;
  uppercase: boolean;
  lowercase: boolean;
  numbers: boolean;
  symbols: boolean;
  excludeAmbiguous: boolean;
}

export const DEFAULT_PASSWORD_OPTIONS: PasswordGeneratorOptions = {
  length: 20,
  uppercase: true,
  lowercase: true,
  numbers: true,
  symbols: true,
  excludeAmbiguous: false,
};

/** The non-empty character sets implied by `options`, after stripping ambiguous characters. */
function selectedPools(options: PasswordGeneratorOptions): string[] {
  const pools: string[] = [];
  if (options.uppercase) pools.push(UPPERCASE);
  if (options.lowercase) pools.push(LOWERCASE);
  if (options.numbers) pools.push(NUMBERS);
  if (options.symbols) pools.push(SYMBOLS);

  const strip = (set: string) =>
    options.excludeAmbiguous ? [...set].filter((c) => !AMBIGUOUS_CHARS.includes(c)).join('') : set;

  return pools.map(strip).filter((set) => set.length > 0);
}

/** Total distinct characters available across the selected sets - used for entropy estimation. */
export function getAlphabetSize(options: PasswordGeneratorOptions): number {
  return selectedPools(options).reduce((sum, set) => sum + set.length, 0);
}

export function generatePassword(options: PasswordGeneratorOptions): string {
  const pools = selectedPools(options);
  if (pools.length === 0) {
    throw new Error('Select at least one character set.');
  }

  // Guarantee one character from each selected set so e.g. "symbols on" can't silently produce a
  // password with no symbol in it, then fill the rest from the combined alphabet and shuffle -
  // otherwise the guaranteed characters would always land in the same leading positions.
  const alphabet = pools.join('');
  const required = pools.map((set) => randomChar(set));
  const remaining = Math.max(0, options.length - required.length);
  const rest = Array.from({ length: remaining }, () => randomChar(alphabet));

  return shuffle([...required, ...rest]).slice(0, options.length).join('');
}

function randomChar(chars: string): string {
  return chars[randomInt(chars.length)];
}

/** Uniform random integer in [0, exclusiveMax) via rejection sampling - no modulo bias. */
function randomInt(exclusiveMax: number): number {
  const buffer = new Uint32Array(1);
  const max = Math.floor(0x100000000 / exclusiveMax) * exclusiveMax;
  let value: number;
  do {
    crypto.getRandomValues(buffer);
    value = buffer[0];
  } while (value >= max);
  return value % exclusiveMax;
}

function shuffle<T>(items: T[]): T[] {
  const arr = [...items];
  for (let i = arr.length - 1; i > 0; i--) {
    const j = randomInt(i + 1);
    [arr[i], arr[j]] = [arr[j], arr[i]];
  }
  return arr;
}

export type PasswordStrength = 'weak' | 'fair' | 'good' | 'strong';

export interface PasswordStrengthEstimate {
  bits: number;
  label: PasswordStrength;
}

/** Rough entropy estimate (length * log2(alphabet size)) - assumes uniform random generation, which is what this module produces. */
export function estimatePasswordStrength(password: string, alphabetSize: number): PasswordStrengthEstimate {
  const bits = password.length * Math.log2(Math.max(alphabetSize, 2));
  const label: PasswordStrength = bits < 40 ? 'weak' : bits < 60 ? 'fair' : bits < 80 ? 'good' : 'strong';
  return { bits: Math.round(bits), label };
}
