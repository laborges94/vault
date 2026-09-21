namespace Vault.Core.Cryptography;

/// <summary>
/// Service contract for deriving encryption keys from user passphrases using Argon2id.
/// </summary>
public interface IKeyDerivationService
{
    /// <summary>
    /// Generates a cryptographically secure random salt of the specified length.
    /// </summary>
    byte[] GenerateSalt(int sizeBytes = Argon2Parameters.DefaultSaltSizeBytes);

    /// <summary>
    /// Derives a cryptographic key from a passphrase character span and salt.
    /// </summary>
    SecureBuffer DeriveKey(ReadOnlySpan<char> passphrase, ReadOnlySpan<byte> salt, Argon2Parameters? parameters = null);

    /// <summary>
    /// Derives a cryptographic key from a passphrase byte span and salt.
    /// </summary>
    SecureBuffer DeriveKey(ReadOnlySpan<byte> passphraseBytes, ReadOnlySpan<byte> salt, Argon2Parameters? parameters = null);
}
