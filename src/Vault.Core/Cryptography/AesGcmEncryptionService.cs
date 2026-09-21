using System.Security.Cryptography;
using Vault.Core.Exceptions;

namespace Vault.Core.Cryptography;

/// <summary>
/// AES-256-GCM authenticated encryption service with Additional Authenticated Data (AAD) support.
/// </summary>
public sealed class AesGcmEncryptionService : IAeadEncryptionService
{
    public byte[] GenerateNonce()
    {
        return RandomNumberGenerator.GetBytes(IAeadEncryptionService.NonceSizeBytes);
    }

    public AeadEncryptionResult Encrypt(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> associatedData = default,
        ReadOnlySpan<byte> nonce = default)
    {
        if (key.Length != IAeadEncryptionService.KeySizeBytes)
        {
            throw new ArgumentException($"Encryption key must be {IAeadEncryptionService.KeySizeBytes} bytes (256 bits).", nameof(key));
        }

        byte[] nonceBytes;
        if (nonce.IsEmpty)
        {
            nonceBytes = GenerateNonce();
        }
        else
        {
            if (nonce.Length != IAeadEncryptionService.NonceSizeBytes)
            {
                throw new ArgumentException($"Nonce must be {IAeadEncryptionService.NonceSizeBytes} bytes (96 bits).", nameof(nonce));
            }

            nonceBytes = nonce.ToArray();
        }

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[IAeadEncryptionService.TagSizeBytes];

        using (var aesGcm = new AesGcm(key, IAeadEncryptionService.TagSizeBytes))
        {
            aesGcm.Encrypt(nonceBytes, plaintext, ciphertext, tag, associatedData);
        }

        return new AeadEncryptionResult(ciphertext, nonceBytes, tag);
    }

    public SecureBuffer Decrypt(
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> tag,
        ReadOnlySpan<byte> associatedData = default)
    {
        if (key.Length != IAeadEncryptionService.KeySizeBytes)
        {
            throw new ArgumentException($"Encryption key must be {IAeadEncryptionService.KeySizeBytes} bytes (256 bits).", nameof(key));
        }

        if (nonce.Length != IAeadEncryptionService.NonceSizeBytes)
        {
            throw new ArgumentException($"Nonce must be {IAeadEncryptionService.NonceSizeBytes} bytes (96 bits).", nameof(nonce));
        }

        if (tag.Length != IAeadEncryptionService.TagSizeBytes)
        {
            throw new ArgumentException($"Authentication tag must be {IAeadEncryptionService.TagSizeBytes} bytes (128 bits).", nameof(tag));
        }

        var decryptedBuffer = new SecureBuffer(ciphertext.Length);

        try
        {
            using var aesGcm = new AesGcm(key, IAeadEncryptionService.TagSizeBytes);
            aesGcm.Decrypt(nonce, ciphertext, tag, decryptedBuffer.Span, associatedData);
            return decryptedBuffer;
        }
        catch (CryptographicException ex)
        {
            decryptedBuffer.Dispose();
            throw new VaultAuthenticationException("Decryption failed. Invalid authentication tag or corrupted data.", ex);
        }
        catch
        {
            decryptedBuffer.Dispose();
            throw;
        }
    }
}
