using System.Security.Cryptography;
using EmergencyVault.Application.Interfaces;
using OtpNet;

namespace EmergencyVault.Infrastructure.Security;

/// RFC 4226 HOTP: HMAC-SHA512 over the big-endian counter, dynamically truncated to a
/// Digits-length decimal code. Deterministic in (secret, counter), so the caller is
/// responsible for never reusing a counter value once its code has been issued.
/// </summary>
public class HotpService : IHotpService
{
    public string GenerateCode(byte[] secret, long counter, int digits = 6)
    {
        var hotp = new Hotp(secret, hotpSize: digits, mode: OtpHashMode.Sha512);
        var otp = hotp.ComputeHOTP(counter);
        return otp;
    }

    public bool VerifyCode(string secret, string hotp, long counter, int digits = 6)
    {
        var hotpInstance = new Hotp(Convert.FromBase64String(secret), hotpSize: digits, mode: OtpHashMode.Sha512);
        return hotpInstance.VerifyHotp(hotp, counter);
    }
}
