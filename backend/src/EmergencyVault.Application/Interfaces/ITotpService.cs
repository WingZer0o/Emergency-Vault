namespace EmergencyVault.Application.Interfaces;

/// RFC 6238 TOTP for authenticator apps (Microsoft/Google Authenticator etc). Codes are
/// SHA1/6-digit/30s-period - authenticator apps assume these defaults regardless of what an
/// otpauth:// URI declares, so the URI intentionally omits the algorithm param.
/// </summary>
public interface ITotpService
{
    /// <summary>Generates a fresh Base32-encoded random secret for a new enrollment.</summary>
    string GenerateSecretKey();

    /// <summary>Builds the otpauth://totp/... URI an authenticator app scans to enroll the given secret.</summary>
    string GenerateQrCodeUri(string secretKey, string userEmail);

    /// <summary>Renders a QR code for the given otpauth:// URI as a Base64-encoded PNG.</summary>
    string GenerateQrCodePng(string otpUri);

    /// <summary>Verifies a submitted code against the secret, allowing +-1 time step for clock drift.</summary>
    bool VerifyCode(string secretKey, string code);
}
