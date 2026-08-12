using EmergencyVault.Domain.Constant;
using Infisical.Sdk;
using Infisical.Sdk.Model;

public class InfisicalSetup
{
    public async static Task InjectEmailAPISecrets()
    {
        InfisicalSdkSettings settings = new InfisicalSdkSettingsBuilder().Build();
        InfisicalClient infisicalClient = new InfisicalClient(settings);
#pragma warning disable CS8604 // Possible null reference argument.
        MachineIdentityCredential credential = infisicalClient.Auth().UniversalAuth().LoginAsync(
            Environment.GetEnvironmentVariable(EnvironmentVariableConst.InfisicalClientId),
            Environment.GetEnvironmentVariable(EnvironmentVariableConst.InfisicalClientSecret)
            ).Result;

        ListSecretsOptions rootOptions = new ListSecretsOptions
        {
            SetSecretsAsEnvironmentVariables = true,
            EnvironmentSlug = GetEnvironment(),
            SecretPath = "/",
            ProjectId = Environment.GetEnvironmentVariable(EnvironmentVariableConst.InfisicalProjectId),
        };
        var rootSecrets = await infisicalClient.Secrets().ListAsync(rootOptions);
        #pragma warning restore CS8604 // Possible null reference argument.
    }

    private static string GetEnvironment()
    {
        switch (Environment.GetEnvironmentVariable(EnvironmentVariableConst.Environment))
        {
            case "Production":
                return "prod";
            case "Staging":
                return "staging";
            case "Development":
                return "dev";
            default:
                throw new NullReferenceException("Environment variable cannot be null");
        }
    }
}