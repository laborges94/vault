using System.Buffers.Text;
using Vault.Core.Exceptions;
using Vault.Core.Sharing;

namespace Vault.Core.Tests.Sharing;

public class EphemeralShareTests
{
    private readonly IEphemeralShareService _shareService = new EphemeralShareService();

    [Fact]
    public void CreateShare_And_OpenShare_BeforeExpiration_RecoversOriginalSecret()
    {
        var secret = "super_secret_ssh_private_key_data";
        var ttl = TimeSpan.FromHours(1);

        var result = _shareService.CreateShare(secret, ttl);

        Assert.NotNull(result.EnvelopeToken);
        Assert.NotNull(result.Key);
        Assert.True(result.ExpiresAt > DateTimeOffset.UtcNow);

        var decrypted = _shareService.OpenShareString(result.EnvelopeToken, result.Key);
        Assert.Equal(secret, decrypted);
    }

    [Fact]
    public void OpenShare_AfterTtlElapsed_ThrowsVaultExpiredException()
    {
        var secret = "ephemeral_code";
        var createdAt = DateTimeOffset.UtcNow.AddHours(-2);
        var ttl = TimeSpan.FromHours(1); // expired 1 hour ago

        var result = _shareService.CreateShare(secret, ttl, createdAt);

        var ex = Assert.Throws<VaultExpiredException>(() =>
            _shareService.OpenShare(result.EnvelopeToken, result.Key));

        Assert.Contains("has expired", ex.Message);
    }

    [Fact]
    public void OpenShare_WithSimulatedReferenceTime_ThrowsWhenTimeExceedsTtl()
    {
        var secret = "temporary_password";
        var now = DateTimeOffset.UtcNow;
        var ttl = TimeSpan.FromMinutes(30);

        var result = _shareService.CreateShare(secret, ttl, now);

        // Within TTL
        var valid = _shareService.OpenShareString(result.EnvelopeToken, result.Key, now.AddMinutes(29));
        Assert.Equal(secret, valid);

        // 1 second past TTL
        Assert.Throws<VaultExpiredException>(() =>
            _shareService.OpenShareString(result.EnvelopeToken, result.Key, now.AddMinutes(30).AddSeconds(1)));
    }

    [Fact]
    public void OpenShare_WrongKey_ThrowsVaultAuthenticationException()
    {
        var secret = "confidential_token";
        var result = _shareService.CreateShare(secret, TimeSpan.FromHours(1));

        // Generate a random different key
        var wrongKey = Base64Url.EncodeToString(new byte[32]);

        Assert.Throws<VaultAuthenticationException>(() =>
            _shareService.OpenShare(result.EnvelopeToken, wrongKey));
    }

    [Fact]
    public void OpenShare_CombinedTokenWithFragmentAnchor_SucceedsWithoutExplicitKey()
    {
        var secret = "secret_via_url_fragment";
        var result = _shareService.CreateShare(secret, TimeSpan.FromHours(1));

        var combined = $"{result.EnvelopeToken}#{result.Key}";

        var decrypted = _shareService.OpenShareString(combined);
        Assert.Equal(secret, decrypted);
    }

    [Fact]
    public void OpenShare_MissingKey_ThrowsVaultValidationException()
    {
        var result = _shareService.CreateShare("val", TimeSpan.FromMinutes(10));

        Assert.Throws<VaultValidationException>(() =>
            _shareService.OpenShare(result.EnvelopeToken, key: null));
    }

    [Fact]
    public void CreateShare_InvalidTtl_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _shareService.CreateShare("val", TimeSpan.Zero));

        Assert.Throws<ArgumentException>(() =>
            _shareService.CreateShare("val", TimeSpan.FromSeconds(-10)));
    }

    [Fact]
    public void TamperEnvelope_TtlInHeaderModified_ThrowsVaultAuthenticationExceptionDueToAad()
    {
        var secret = "tamper_target";
        var result = _shareService.CreateShare(secret, TimeSpan.FromHours(1));

        var envelope = SharingEnvelope.FromBase64Url(result.EnvelopeToken);
        var envelopeBytes = envelope.ToBytes();

        // Byte index 9 is part of TtlSeconds in header
        envelopeBytes[9] ^= 0xFF;

        var tamperedToken = Base64Url.EncodeToString(envelopeBytes);

        // AES-GCM must detect AAD modification and fail tag verification
        Assert.Throws<VaultAuthenticationException>(() =>
            _shareService.OpenShare(tamperedToken, result.Key));
    }
}
