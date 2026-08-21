import {
  DEFAULT_PASSWORD_OPTIONS,
  estimatePasswordStrength,
  generatePassword,
  getAlphabetSize,
  PasswordGeneratorOptions,
} from './password-generator.util';

describe('password-generator.util', () => {
  it('generates a password of the requested length', () => {
    const password = generatePassword({ ...DEFAULT_PASSWORD_OPTIONS, length: 32 });
    expect(password).toHaveLength(32);
  });

  it('only uses characters from the enabled sets', () => {
    const options: PasswordGeneratorOptions = {
      length: 40,
      uppercase: false,
      lowercase: true,
      numbers: true,
      symbols: false,
      excludeAmbiguous: false,
    };

    const password = generatePassword(options);
    expect(password).toMatch(/^[a-z0-9]+$/);
  });

  it('includes at least one character from every enabled set', () => {
    const options: PasswordGeneratorOptions = {
      length: 8,
      uppercase: true,
      lowercase: true,
      numbers: true,
      symbols: true,
      excludeAmbiguous: false,
    };

    for (let i = 0; i < 25; i++) {
      const password = generatePassword(options);
      expect(password).toMatch(/[A-Z]/);
      expect(password).toMatch(/[a-z]/);
      expect(password).toMatch(/[0-9]/);
      expect(password).toMatch(/[^A-Za-z0-9]/);
    }
  });

  it('excludes ambiguous characters when asked', () => {
    const options: PasswordGeneratorOptions = {
      length: 200,
      uppercase: true,
      lowercase: true,
      numbers: true,
      symbols: false,
      excludeAmbiguous: true,
    };

    const password = generatePassword(options);
    expect(password).not.toMatch(/[01lIO]/);
  });

  it('throws when no character set is selected', () => {
    const options: PasswordGeneratorOptions = {
      length: 16,
      uppercase: false,
      lowercase: false,
      numbers: false,
      symbols: false,
      excludeAmbiguous: false,
    };

    expect(() => generatePassword(options)).toThrow();
  });

  it('does not repeat the same password across calls', () => {
    const options = { ...DEFAULT_PASSWORD_OPTIONS, length: 24 };
    const passwords = new Set(Array.from({ length: 20 }, () => generatePassword(options)));
    expect(passwords.size).toBe(20);
  });

  describe('getAlphabetSize', () => {
    it('sums the enabled character sets', () => {
      const size = getAlphabetSize({
        length: 16,
        uppercase: true,
        lowercase: true,
        numbers: false,
        symbols: false,
        excludeAmbiguous: false,
      });
      expect(size).toBe(52);
    });

    it('shrinks when ambiguous characters are excluded', () => {
      const withAmbiguous = getAlphabetSize({ ...DEFAULT_PASSWORD_OPTIONS, excludeAmbiguous: false });
      const withoutAmbiguous = getAlphabetSize({ ...DEFAULT_PASSWORD_OPTIONS, excludeAmbiguous: true });
      expect(withoutAmbiguous).toBeLessThan(withAmbiguous);
    });
  });

  describe('estimatePasswordStrength', () => {
    it('labels a short, small-alphabet password as weak', () => {
      expect(estimatePasswordStrength('12345', 10).label).toBe('weak');
    });

    it('labels a long, full-alphabet password as strong', () => {
      expect(estimatePasswordStrength('x'.repeat(32), 94).label).toBe('strong');
    });
  });
});
