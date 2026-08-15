using EmergencyVault.Application.Interfaces;
using Hangfire;

namespace EmergencyVault.Infrastructure.Email;

/// <summary>
/// Queues emails onto Hangfire's "emails" queue (backed by SQL Server) instead of sending
/// synchronously, so the actual Resend calls happen one at a time via EmailJobs.
/// </summary>
public class ResendEmailSender(IBackgroundJobClient backgroundJobClient) : IEmailSender
{
    public Task SendEmailConfirmationAsync(string toEmail, string confirmationLink, CancellationToken ct)
    {
        backgroundJobClient.Enqueue<EmailJobs>(job => job.SendEmailConfirmationAsync(toEmail, confirmationLink));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken ct)
    {
        backgroundJobClient.Enqueue<EmailJobs>(job => job.SendPasswordResetAsync(toEmail, resetLink));
        return Task.CompletedTask;
    }

    public Task SendTwoFactorCodeAsync(string toEmail, string code, CancellationToken ct)
    {
        backgroundJobClient.Enqueue<EmailJobs>(job => job.SendTwoFactorCodeAsync(toEmail, code));
        return Task.CompletedTask;
    }

    public Task SendAccountLockedAsync(string toEmail, string unlockLink, CancellationToken ct)
    {
        backgroundJobClient.Enqueue<EmailJobs>(job => job.SendAccountLockedAsync(toEmail, unlockLink));
        return Task.CompletedTask;
    }

    public Task SendNewIpLoginAsync(string toEmail, string ipAddress, DateTimeOffset loggedInAt, CancellationToken ct)
    {
        backgroundJobClient.Enqueue<EmailJobs>(job => job.SendNewIpLoginAsync(toEmail, ipAddress, loggedInAt));
        return Task.CompletedTask;
    }

    public Task SendVaultShareCodeAsync(string toEmail, string code, string ownerDisplayName, CancellationToken ct)
    {
        backgroundJobClient.Enqueue<EmailJobs>(job => job.SendVaultShareCodeAsync(toEmail, code, ownerDisplayName));
        return Task.CompletedTask;
    }

    public Task SendSharedVaultAccessCodeAsync(string toEmail, string code, string ownerDisplayName, CancellationToken ct)
    {
        backgroundJobClient.Enqueue<EmailJobs>(job => job.SendSharedVaultAccessCodeAsync(toEmail, code, ownerDisplayName));
        return Task.CompletedTask;
    }
}
