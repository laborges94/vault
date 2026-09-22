using Vault.Core.Cryptography;

namespace Vault.Core.Sharing;

/// <summary>
/// Result of generating an ephemeral secret share bundle.
/// </summary>
public sealed record EphemeralShareResult(
    string EnvelopeToken,
    string Key,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Service contract for ephemeral peer-to-peer secret envelope generation and consumption.
/// </summary>
public interface IEphemeralShareService
{
    /// <summary>
    /// Encrypts a secret value into an ephemeral sharing envelope with independent decryption key and client TTL.
    /// </summary>
    EphemeralShareResult CreateShare(
        string secretValue,
        TimeSpan ttl,
        DateTimeOffset? createdAt = null);

    /// <summary>
    /// Decrypts an ephemeral envelope using the provided key and verifies that TTL has not elapsed.
    /// Returns decrypted content in a disposable SecureBuffer.
    /// </summary>
    SecureBuffer OpenShare(
        string envelopeToken,
        string? key = null,
        DateTimeOffset? referenceTime = null);

    /// <summary>
    /// Decrypts an ephemeral envelope and returns the secret as a UTF-8 string.
    /// </summary>
    string OpenShareString(
        string envelopeToken,
        string? key = null,
        DateTimeOffset? referenceTime = null);
}
