namespace AuthSystem.Application.Interfaces;

/// <summary>RFC 4226 HMAC-based one-time password generator.</summary>
public interface IHotpService
{
    string GenerateCode(byte[] secret, long counter, int digits = 6);
}
