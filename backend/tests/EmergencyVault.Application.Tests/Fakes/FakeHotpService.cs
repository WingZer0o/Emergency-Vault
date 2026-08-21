using System.Security.Cryptography;
using System.Text;
using EmergencyVault.Application.Interfaces;
using OtpNet;

namespace EmergencyVault.Application.Tests.Fakes;

/// <summary>
/// Deterministic in (secret, counter) like the real HotpService, but hashed with SHA-256 instead
/// of implementing RFC 4226 exactly - tests only need "same input produces the same code" and
/// "different counter produces a different code", not bit-for-bit HOTP compliance.
/// </summary>
public class FakeHotpService : IHotpService
{
    public string GenerateCode(byte[] secret, long counter, int digits = 6)
    {
        var hotp = new Hotp(secret, hotpSize: digits, mode: OtpHashMode.Sha512);
        var code = hotp.ComputeHOTP(counter);
        return code;
    }

    public bool VerifyCode(string secret, string hotp, long counter, int digits = 6)
    {
        var hotpInstance = new Hotp(Convert.FromBase64String(secret), hotpSize: digits, mode: OtpHashMode.Sha512);
        return hotpInstance.VerifyHotp(hotp, counter);
    }

}
