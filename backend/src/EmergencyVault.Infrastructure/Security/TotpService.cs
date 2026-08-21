using EmergencyVault.Application.Interfaces;
using OtpNet;

namespace EmergencyVault.Infrastructure.Security;

public class TotpService : ITotpService
{
    private const string Issuer = "EmergencyVault";

    public string GenerateSecretKey()
    {
        return Base32Encoding.ToString(KeyGeneration.GenerateRandomKey());
    }

    public string GenerateQrCodeUri(string secretKey, string userEmail)
    {
        return $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(userEmail)}?secret={secretKey}&issuer={Uri.EscapeDataString(Issuer)}&digits=6&period=30";
    }

    public string GenerateQrCodePng(string otpUri)
    {
        using var qrCodeGenerator = new QRCoder.QRCodeGenerator();
        var qrCodeData = qrCodeGenerator.CreateQrCode(otpUri, QRCoder.QRCodeGenerator.ECCLevel.Q);
        var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
        var qrCodeBytes = qrCode.GetGraphic(20);
        return Convert.ToBase64String(qrCodeBytes);
    }

    public bool VerifyCode(string secretKey, string code)
    {
        var totp = new Totp(Base32Encoding.ToBytes(secretKey), step: 30, mode: OtpHashMode.Sha1, totpSize: 6);
        return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
    }
}
