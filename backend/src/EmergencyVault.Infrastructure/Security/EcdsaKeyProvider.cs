using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace EmergencyVault.Infrastructure.Security;

/// <summary>
/// Loads the ECDSA P-521 signing key from a PEM string injected via an environment
/// variable (Infisical), rather than the file system.
/// </summary>
public static class EcdsaKeyProvider
{
    public static ECDsa Load(string pem, ILogger logger)
    {
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pem);
        logger.LogInformation("Loaded ECDSA P-521 signing key from environment variable.");
        return ecdsa;
    }
}
