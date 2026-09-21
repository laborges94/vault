namespace Vault.Core.Cryptography;

/// <summary>
/// Result of an AES-256-GCM authenticated encryption operation.
/// </summary>
public sealed record AeadEncryptionResult(byte[] Ciphertext, byte[] Nonce, byte[] Tag);

/// <summary>
/// Service contract for authenticated symmetric encryption and decryption with associated data (AEAD).
/// </summary>
public interface IAeadEncryptionService
{
    public const int NonceSizeBytes = 12; // 96 bits
    public const int TagSizeBytes = 16;   // 128 bits
    public const int KeySizeBytes = 32;   // 256 bits

    /// <summary>
    /// Generates a cryptographically secure random 12-byte (96-bit) nonce.
    /// </summary>
    byte[] GenerateNonce();

    /// <summary>
    /// Encrypts plaintext using AES-256-GCM with associated data and an optional explicit nonce.
    /// </summary>
    AeadEncryptionResult Encrypt(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> associatedData = default,
        ReadOnlySpan<byte> nonce = default);

    /// <summary>
    /// Decrypts ciphertext using AES-256-GCM, verifying the authentication tag and associated data.
    /// Returns a SecureBuffer containing the decrypted plaintext.
    /// </summary>
    SecureBuffer Decrypt(
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> tag,
        ReadOnlySpan<byte> associatedData = default);
}
