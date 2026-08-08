using System.Security.Cryptography;
using AuthSystem.Application.Interfaces;

namespace AuthSystem.Infrastructure.Security;

/// <summary>
/// RFC 4226 HOTP: HMAC-SHA1 over the big-endian counter, dynamically truncated to a
/// Digits-length decimal code. Deterministic in (secret, counter), so the caller is
/// responsible for never reusing a counter value once its code has been issued.
/// </summary>
public class HotpService : IHotpService
{
    public string GenerateCode(byte[] secret, long counter, int digits = 6)
    {
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counterBytes);
        }

        var hash = HMACSHA1.HashData(secret, counterBytes);

        var offset = hash[^1] & 0x0F;
        var binaryCode = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);

        var otp = binaryCode % (int)Math.Pow(10, digits);
        return otp.ToString().PadLeft(digits, '0');
    }
}
