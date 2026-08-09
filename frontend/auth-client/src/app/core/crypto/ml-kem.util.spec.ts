import { decapsulateMlKem, encapsulateMlKem, generateMlKemKeypair } from './ml-kem.util';

describe('ml-kem.util', () => {
  it('round-trips a shared secret through keygen/encapsulate/decapsulate', () => {
    const { publicKey, secretKey } = generateMlKemKeypair();
    const { ciphertext, sharedSecret } = encapsulateMlKem(publicKey);

    const decapsulated = decapsulateMlKem(ciphertext, secretKey);

    expect(decapsulated).toEqual(sharedSecret);
  });

  it('produces ML-KEM-768-sized keys, ciphertext, and shared secret', () => {
    const { publicKey, secretKey } = generateMlKemKeypair();
    const { ciphertext, sharedSecret } = encapsulateMlKem(publicKey);

    expect(publicKey.length).toBe(1184);
    expect(secretKey.length).toBe(2400);
    expect(ciphertext.length).toBe(1088);
    expect(sharedSecret.length).toBe(32);
  });

  it('produces different shared secrets for different keypairs', () => {
    const a = generateMlKemKeypair();
    const b = generateMlKemKeypair();

    const encapsulatedForA = encapsulateMlKem(a.publicKey);
    const decapsulatedByB = decapsulateMlKem(encapsulatedForA.ciphertext, b.secretKey);

    expect(decapsulatedByB).not.toEqual(encapsulatedForA.sharedSecret);
  });
});
