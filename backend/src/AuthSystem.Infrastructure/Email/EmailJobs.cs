using System.Text;
using System.Text.Json;
using AuthSystem.Domain.Constant;
using AuthSystem.Infrastructure.Http;
using Hangfire;

namespace AuthSystem.Infrastructure.Email;

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
            From = "support@mtmulch.com",
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
            From = "support@mtmulch.com",
            To = new List<string> { toEmail },
            Subject = "Password Reset",
            Html = $"<p>Reset your password by clicking <a href='{resetLink}'>here</a>.</p>"
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
