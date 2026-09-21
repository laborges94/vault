using System.Text;
using System.Text.RegularExpressions;
using Vault.Core.Exceptions;

namespace Vault.Core.Model;

/// <summary>
/// Validates secret keys and values against vault format constraints.
/// </summary>
public static partial class SecretKeyValidator
{
    public const int MaxKeyLength = 128;
    public const int MaxValueSizeBytes = 1024 * 1024; // 1 MiB

    private static readonly Regex KeyRegex = CreateKeyRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9_:\.\/-]{1,128}$")]
    private static partial Regex CreateKeyRegex();

    /// <summary>
    /// Checks whether the specified key conforms to the allowed key regex and length constraints.
    /// </summary>
    public static bool IsValidKey(string? key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > MaxKeyLength)
        {
            return false;
        }

        return KeyRegex.IsMatch(key);
    }

    /// <summary>
    /// Validates a secret key and throws <see cref="VaultValidationException"/> if invalid.
    /// </summary>
    public static void ValidateKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new VaultValidationException("Secret key cannot be null or empty.");
        }

        if (key.Length > MaxKeyLength)
        {
            throw new VaultValidationException($"Secret key exceeds maximum allowed length of {MaxKeyLength} characters.");
        }

        if (!KeyRegex.IsMatch(key))
        {
            throw new VaultValidationException($"Invalid secret key '{key}'. Key must match pattern '^[a-zA-Z0-9_:\\./-]{{1,128}}$'.");
        }
    }

    /// <summary>
    /// Validates a secret value size and throws <see cref="VaultValidationException"/> if it exceeds 1 MiB.
    /// </summary>
    public static void ValidateValue(string? value)
    {
        if (value is null)
        {
            throw new VaultValidationException("Secret value cannot be null.");
        }

        var byteCount = Encoding.UTF8.GetByteCount(value);
        if (byteCount > MaxValueSizeBytes)
        {
            throw new VaultValidationException($"Secret value size ({byteCount} bytes) exceeds maximum allowed size of {MaxValueSizeBytes} bytes (1 MiB).");
        }
    }

    /// <summary>
    /// Validates both key and value constraints.
    /// </summary>
    public static void Validate(string? key, string? value)
    {
        ValidateKey(key);
        ValidateValue(value);
    }
}
