// Checks a password against HaveIBeenPwned's Pwned Passwords range API using k-anonymity: only
// the first 5 hex characters of the password's SHA-1 hash ever leave the browser, so neither the
// password nor its full hash reaches HIBP or this app's own backend. Fits the vault's existing
// zero-knowledge model (see webcrypto.util.ts) - the server never sees vault contents, and this
// check doesn't change that.

const HIBP_RANGE_URL = 'https://api.pwnedpasswords.com/range/';

async function sha1Hex(text: string): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-1', new TextEncoder().encode(text));
  return Array.from(new Uint8Array(digest))
    .map((byte) => byte.toString(16).padStart(2, '0'))
    .join('')
    .toUpperCase();
}

/** Returns how many times a password has appeared in known breaches (0 = not found). */
export async function checkPwnedCount(password: string): Promise<number> {
  const hash = await sha1Hex(password);
  const prefix = hash.slice(0, 5);
  const suffix = hash.slice(5);

  const response = await fetch(`${HIBP_RANGE_URL}${prefix}`, {
    headers: { 'Add-Padding': 'true' },
  });
  if (!response.ok) {
    throw new Error(`HIBP range lookup failed with status ${response.status}`);
  }

  const body = await response.text();
  for (const line of body.split('\n')) {
    const [lineSuffix, count] = line.trim().split(':');
    if (lineSuffix === suffix) {
      return Number(count);
    }
  }
  return 0;
}
