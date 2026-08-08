using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace AuthSystem.Infrastructure.Security;

/// <summary>
/// Loads the persisted ECDSA P-521 signing key, or generates and persists a new one on first run.
/// The key must survive container restarts (mounted volume) - otherwise every restart invalidates
/// all previously issued tokens.
/// </summary>
public static class EcdsaKeyProvider
{
    public static ECDsa LoadOrCreate(string keyPath, ILogger logger)
    {
        var directory = Path.GetDirectoryName(keyPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(keyPath))
        {
            var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(File.ReadAllText(keyPath));
            logger.LogInformation("Loaded existing ECDSA P-521 signing key from {KeyPath}", keyPath);
            return ecdsa;
        }

        var newKey = ECDsa.Create(ECCurve.NamedCurves.nistP521);
        File.WriteAllText(keyPath, newKey.ExportECPrivateKeyPem());
        logger.LogWarning(
            "No signing key found at {KeyPath} - generated a new ECDSA P-521 key pair. " +
            "Ensure this path is backed by a persistent volume in production.", keyPath);
        return newKey;
    }
}
