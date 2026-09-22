using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Vault.Core.Cryptography;
using Vault.Core.Exceptions;
using Vault.Core.Model;

namespace Vault.Core.Sharing;

/// <summary>
/// Implements ephemeral secret envelope creation and consumption with client-side TTL enforcement.
/// </summary>
public sealed class EphemeralShareService : IEphemeralShareService
{
    private readonly IAeadEncryptionService _encryptionService;

    public EphemeralShareService(IAeadEncryptionService? encryptionService = null)
    {
        _encryptionService = encryptionService ?? new AesGcmEncryptionService();
    }

    public EphemeralShareResult CreateShare(
        string secretValue,
        TimeSpan ttl,
        DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(secretValue);
        SecretKeyValidator.ValidateValue(secretValue);

        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentException("TTL duration must be greater than zero.", nameof(ttl));
        }

        var created = createdAt ?? DateTimeOffset.UtcNow;
        var key = RandomNumberGenerator.GetBytes(IAeadEncryptionService.KeySizeBytes);
        var nonce = _encryptionService.GenerateNonce();

        var placeholder = new SharingEnvelope(created, ttl, nonce, new byte[SharingEnvelope.TagSizeBytes], Array.Empty<byte>());
        var aad = placeholder.GetAssociatedData();

        byte[]? plaintextBytes = null;
        try
        {
            plaintextBytes = Encoding.UTF8.GetBytes(secretValue);
            var encResult = _encryptionService.Encrypt(plaintextBytes, key, aad, nonce);

            var envelope = new SharingEnvelope(created, ttl, nonce, encResult.Tag, encResult.Ciphertext);
            var token = envelope.ToBase64Url();
            var keyString = Base64Url.EncodeToString(key);

            return new EphemeralShareResult(token, keyString, envelope.ExpiresAt);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            if (plaintextBytes != null)
            {
                CryptographicOperations.ZeroMemory(plaintextBytes);
            }
        }
    }

    public SecureBuffer OpenShare(
        string envelopeToken,
        string? key = null,
        DateTimeOffset? referenceTime = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(envelopeToken);

        var token = envelopeToken.Trim();

        // Handle fragment anchor: token#key
        var hashIndex = token.IndexOf('#');
        if (hashIndex >= 0)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                key = token[(hashIndex + 1)..].Trim();
            }

            token = token[..hashIndex].Trim();
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new VaultValidationException("Decryption key must be provided to open an ephemeral share envelope.");
        }

        var envelope = SharingEnvelope.FromBase64Url(token);
        var checkTime = referenceTime ?? DateTimeOffset.UtcNow;

        if (envelope.IsExpiredAt(checkTime))
        {
            throw new VaultExpiredException($"The shared secret envelope has expired. It expired at {envelope.ExpiresAt:O} (TTL: {envelope.Ttl}).");
        }

        byte[] keyBytes;
        try
        {
            keyBytes = Base64Url.DecodeFromChars(key.Trim());
        }
        catch (FormatException ex)
        {
            throw new VaultValidationException("Invalid Base64URL decryption key format.", ex);
        }

        if (keyBytes.Length != IAeadEncryptionService.KeySizeBytes)
        {
            throw new VaultValidationException($"Decryption key must be {IAeadEncryptionService.KeySizeBytes} bytes.");
        }

        try
        {
            return _encryptionService.Decrypt(
                envelope.Ciphertext,
                keyBytes,
                envelope.Nonce,
                envelope.Tag,
                envelope.GetAssociatedData());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyBytes);
        }
    }

    public string OpenShareString(
        string envelopeToken,
        string? key = null,
        DateTimeOffset? referenceTime = null)
    {
        using var secureBuffer = OpenShare(envelopeToken, key, referenceTime);
        return Encoding.UTF8.GetString(secureBuffer.Span);
    }
}
