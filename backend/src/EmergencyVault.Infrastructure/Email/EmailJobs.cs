using System.Text;
using System.Text.Json;
using EmergencyVault.Domain.Constant;
using EmergencyVault.Infrastructure.Http;
using Hangfire;

namespace EmergencyVault.Infrastructure.Email;

/// <summary>
/// Hangfire job methods that drain the "emails" queue one at a time (see AddHangfireServer's
/// WorkerCount = 1 in DependencyInjection.cs) so sends stay under Resend's rate limit.
/// Fill in the actual Resend API call (IResend.EmailSendAsync) in each method below.
/// </summary>
[Queue("emails")]
public class EmailJobs
{
    public async Task SendEmailConfirmationAsync(string toEmail, string confirmationLink)
    {
        EmailRequestBody body = new EmailRequestBody()
        {
            From = "support@emergencyvault.net",
            To = new List<string> { toEmail },
            Subject = "Email Confirmation",
            Html = $"<p>Please confirm your email by clicking <a href='{confirmationLink}'>here</a>.</p>"
        };
        await SendEmailAsync(body);
    }

    public async Task SendPasswordResetAsync(string toEmail, string resetLink)
    {
        EmailRequestBody body = new EmailRequestBody()
        {
            From = "support@emergencyvault.net",
            To = new List<string> { toEmail },
            Subject = "Password Reset",
            Html = $"<p>Reset your password by clicking <a href='{resetLink}'>here</a>.</p>"
        };
        await SendEmailAsync(body);
    }

    public async Task SendTwoFactorCodeAsync(string toEmail, string code)
    {
        EmailRequestBody body = new EmailRequestBody()
        {
            From = "support@emergencyvault.net",
            To = new List<string> { toEmail },
            Subject = "Your verification code",
            Html = $"<p>Your verification code is <strong>{code}</strong>. It expires in 10 minutes.</p>" +
                   "<p>If you didn't try to sign in, you can ignore this email.</p>"
        };
        await SendEmailAsync(body);
    }

    public async Task SendAccountLockedAsync(string toEmail, string unlockLink)
    {
        EmailRequestBody body = new EmailRequestBody()
        {
            From = "support@emergencyvault.net",
            Subject = "Your account has been locked",
            To = new List<string> { toEmail },
            Html = $"<p>Your account has been temporarily locked due to repeated failed sign-in attempts.</p>" +
                   $"<p>You can unlock it now by clicking <a href='{unlockLink}'>here</a>, or wait 15 minutes and it will unlock automatically.</p>" +
                   "<p>If this wasn't you, consider resetting your password once you're back in.</p>"
        };
        await SendEmailAsync(body);
    }

    public async Task SendVaultShareCodeAsync(string toEmail, string code, string ownerDisplayName)
    {
        EmailRequestBody body = new EmailRequestBody()
        {
            From = "support@emergencyvault.net",
            To = new List<string> { toEmail },
            Subject = $"{ownerDisplayName} shared a vault with you",
            Html = $"<p>{ownerDisplayName} wants to share their vault with you. Enter this code in the app to accept: <strong>{code}</strong>.</p>" +
                   "<p>It expires in 72 hours. Entering it grants you access to their vault.</p>" +
                   "<p>If you weren't expecting this, you can ignore this email.</p>"
        };
        await SendEmailAsync(body);
    }

    public async Task SendSharedVaultAccessCodeAsync(string toEmail, string code, string ownerDisplayName)
    {
        EmailRequestBody body = new EmailRequestBody()
        {
            From = "support@emergencyvault.net",
            To = new List<string> { toEmail },
            Subject = $"Your code to view {ownerDisplayName}'s shared vault",
            Html = $"<p>Enter this code in the app to view the vault {ownerDisplayName} shared with you: <strong>{code}</strong>.</p>" +
                   "<p>It expires in 10 minutes.</p>" +
                   "<p>If you weren't trying to open this vault, you can ignore this email.</p>"
        };
        await SendEmailAsync(body);
    }

    private async Task<bool> SendEmailAsync(EmailRequestBody body)
    {
        HttpClientSingleton httpClient = HttpClientSingleton.Instance;
        string url = "https://api.resend.com/emails";
        string apiKey = Environment.GetEnvironmentVariable(EnvironmentVariableConst.ResendApiKey)
            ?? throw new InvalidOperationException($"Missing {EnvironmentVariableConst.ResendApiKey} environment variable.");
        string requestBody = JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        HttpResponseMessage response = await httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }
}
