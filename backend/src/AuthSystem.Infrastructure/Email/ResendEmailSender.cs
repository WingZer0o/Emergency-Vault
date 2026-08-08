using AuthSystem.Application.Interfaces;
using Hangfire;

namespace AuthSystem.Infrastructure.Email;

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
}
