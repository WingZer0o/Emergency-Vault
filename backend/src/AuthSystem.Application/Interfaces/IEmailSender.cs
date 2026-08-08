namespace AuthSystem.Application.Interfaces;

public interface IEmailSender
{
    Task SendEmailConfirmationAsync(string toEmail, string confirmationLink, CancellationToken ct);
    Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken ct);
}
