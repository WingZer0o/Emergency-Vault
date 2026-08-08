// Confirms crypto.subtle is actually functional in this Vitest/Node environment before any real
// vault crypto code depends on it — this repo's test runner has already broken one browser-global
// assumption (see test-setup.ts re: localStorage), so this isn't assumed for granted.
describe('crypto.subtle environment check', () => {
  it('computes a SHA-256 digest', async () => {
    const digest = await crypto.subtle.digest('SHA-256', new Uint8Array(1));
    expect(digest.byteLength).toBe(32);
  });
});
