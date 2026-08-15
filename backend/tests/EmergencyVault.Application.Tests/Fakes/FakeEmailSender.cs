using EmergencyVault.Application.Interfaces;

namespace EmergencyVault.Application.Tests.Fakes;

/// <summary>Captures sent emails instead of delivering them, so tests can read back a code that was "sent".</summary>
public class FakeEmailSender : IEmailSender
{
    public readonly List<(string ToEmail, string Code, string OwnerDisplayName)> VaultShareCodesSent = [];

    public Task SendEmailConfirmationAsync(string toEmail, string confirmationLink, CancellationToken ct) => Task.CompletedTask;
    public Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken ct) => Task.CompletedTask;
    public Task SendTwoFactorCodeAsync(string toEmail, string code, CancellationToken ct) => Task.CompletedTask;
    public Task SendAccountLockedAsync(string toEmail, string unlockLink, CancellationToken ct) => Task.CompletedTask;
    public Task SendNewIpLoginAsync(string toEmail, string ipAddress, DateTimeOffset loggedInAt, CancellationToken ct) => Task.CompletedTask;

    public Task SendVaultShareCodeAsync(string toEmail, string code, string ownerDisplayName, CancellationToken ct)
    {
        VaultShareCodesSent.Add((toEmail, code, ownerDisplayName));
        return Task.CompletedTask;
    }

    public Task SendSharedVaultAccessCodeAsync(string toEmail, string code, string ownerDisplayName, CancellationToken ct)
    {
        VaultShareCodesSent.Add((toEmail, code, ownerDisplayName));
        return Task.CompletedTask;
    }
}
