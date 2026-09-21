using System.Text;
using Vault.Core.Cryptography;

namespace Vault.Core.Tests.Cryptography;

public class Argon2KeyDerivationTests
{
    private readonly IKeyDerivationService _keyDerivationService = new Argon2KeyDerivationService();

    [Fact]
    public void GenerateSalt_DefaultSize_Returns16Bytes()
    {
        var salt = _keyDerivationService.GenerateSalt();
        Assert.Equal(Argon2Parameters.DefaultSaltSizeBytes, salt.Length);
        Assert.Contains(salt, b => b != 0); // Not all zeros
    }

    [Fact]
    public void GenerateSalt_InvalidSize_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _keyDerivationService.GenerateSalt(15));
    }

    [Fact]
    public void DeriveKey_SameInputs_ProducesIdenticalKeys()
    {
        // Arrange
        var passphrase = "MasterPassphrase123!";
        var salt = _keyDerivationService.GenerateSalt();
        var testParams = Argon2Parameters.ForTesting;

        // Act
        using var key1 = _keyDerivationService.DeriveKey(passphrase, salt, testParams);
        using var key2 = _keyDerivationService.DeriveKey(passphrase, salt, testParams);

        // Assert
        Assert.Equal(testParams.KeySizeBytes, key1.Length);
        Assert.Equal(testParams.KeySizeBytes, key2.Length);
        Assert.True(key1.Span.SequenceEqual(key2.Span));
    }

    [Fact]
    public void DeriveKey_DifferentPassphrases_ProducesDifferentKeys()
    {
        // Arrange
        var salt = _keyDerivationService.GenerateSalt();
        var testParams = Argon2Parameters.ForTesting;

        // Act
        using var key1 = _keyDerivationService.DeriveKey("PassphraseA", salt, testParams);
        using var key2 = _keyDerivationService.DeriveKey("PassphraseB", salt, testParams);

        // Assert
        Assert.False(key1.Span.SequenceEqual(key2.Span));
    }

    [Fact]
    public void DeriveKey_DifferentSalts_ProducesDifferentKeys()
    {
        // Arrange
        var passphrase = "CommonPassphrase";
        var salt1 = _keyDerivationService.GenerateSalt();
        var salt2 = _keyDerivationService.GenerateSalt();
        var testParams = Argon2Parameters.ForTesting;

        // Act
        using var key1 = _keyDerivationService.DeriveKey(passphrase, salt1, testParams);
        using var key2 = _keyDerivationService.DeriveKey(passphrase, salt2, testParams);

        // Assert
        Assert.False(key1.Span.SequenceEqual(key2.Span));
    }

    [Fact]
    public void DeriveKey_WithBytePassphrase_MatchesCharPassphrase()
    {
        // Arrange
        var passphrase = "UnicodePassphrase_🔑";
        var passphraseBytes = Encoding.UTF8.GetBytes(passphrase);
        var salt = _keyDerivationService.GenerateSalt();
        var testParams = Argon2Parameters.ForTesting;

        // Act
        using var keyFromChars = _keyDerivationService.DeriveKey(passphrase, salt, testParams);
        using var keyFromBytes = _keyDerivationService.DeriveKey(passphraseBytes, salt, testParams);

        // Assert
        Assert.True(keyFromChars.Span.SequenceEqual(keyFromBytes.Span));
    }
}
