using System.Security.Cryptography;
using System.Text;
using AuthSystem.Application.Interfaces;

namespace AuthSystem.Application.Tests.Fakes;

/// <summary>
/// Deterministic in (secret, counter) like the real HotpService, but hashed with SHA-256 instead
/// of implementing RFC 4226 exactly - tests only need "same input produces the same code" and
/// "different counter produces a different code", not bit-for-bit HOTP compliance.
/// </summary>
public class FakeHotpService : IHotpService
{
    public string GenerateCode(byte[] secret, long counter, int digits = 6)
    {
        var seed = Encoding.UTF8.GetBytes(Convert.ToBase64String(secret) + ":" + counter);
        var hash = SHA256.HashData(seed);
        var value = BitConverter.ToUInt32(hash, 0) % (uint)Math.Pow(10, digits);
        return value.ToString().PadLeft(digits, '0');
    }
}
