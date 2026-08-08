using AuthSystem.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AuthSystem.Infrastructure.Email;

/// <summary>
/// Dev-only email sender that logs links instead of sending real mail.
/// Swap the DI registration for a real SMTP/SendGrid implementation of IEmailSender in production.
/// </summary>
public class ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) : IEmailSender
{
    public Task SendEmailConfirmationAsync(string toEmail, string confirmationLink, CancellationToken ct)
    {
        logger.LogInformation("[DEV EMAIL] Confirm your account ({Email}): {Link}", toEmail, confirmationLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken ct)
    {
        logger.LogInformation("[DEV EMAIL] Reset your password ({Email}): {Link}", toEmail, resetLink);
        return Task.CompletedTask;
    }
}
