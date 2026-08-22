namespace EmergencyVault.Application.Interfaces;

public interface IEmailSender
{
    Task SendEmailConfirmationAsync(string toEmail, string confirmationLink, CancellationToken ct);
    Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken ct);
    Task SendTwoFactorCodeAsync(string toEmail, string code, CancellationToken ct);
    Task SendAccountLockedAsync(string toEmail, string unlockLink, CancellationToken ct);
    Task SendNewIpLoginAsync(string toEmail, string ipAddress, DateTimeOffset loggedInAt, CancellationToken ct);
    Task SendRecoveryCodeUsedAsync(string toEmail, string? ipAddress, DateTimeOffset usedAt, CancellationToken ct);
    Task SendVaultShareCodeAsync(string toEmail, string code, string ownerDisplayName, CancellationToken ct);
    Task SendSharedVaultAccessCodeAsync(string toEmail, string code, string ownerDisplayName, CancellationToken ct);
}
