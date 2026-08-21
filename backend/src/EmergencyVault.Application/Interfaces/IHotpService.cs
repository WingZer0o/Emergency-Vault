namespace EmergencyVault.Application.Interfaces;

/// <summary>RFC 4226 HMAC-based one-time password generator.</summary>
public interface IHotpService
{
    string GenerateCode(byte[] secret, long counter, int digits = 6);
    bool VerifyCode(string secret, string hotp, long counter, int digits = 6);
}
