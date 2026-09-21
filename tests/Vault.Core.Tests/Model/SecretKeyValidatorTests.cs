using Vault.Core.Exceptions;
using Vault.Core.Model;

namespace Vault.Core.Tests.Model;

public class SecretKeyValidatorTests
{
    [Theory]
    [InlineData("DATABASE_URL")]
    [InlineData("api_key")]
    [InlineData("aws:secret:key")]
    [InlineData("services/auth/token")]
    [InlineData("config.dev.port")]
    [InlineData("A")]
    [InlineData("a-1_2:3/4.5")]
    public void IsValidKey_WithValidPatterns_ReturnsTrue(string key)
    {
        Assert.True(SecretKeyValidator.IsValidKey(key));
        // ValidateKey should not throw
        SecretKeyValidator.ValidateKey(key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("INVALID KEY NAME")]
    [InlineData("key@domain")]
    [InlineData("key$value")]
    [InlineData("key!")]
    [InlineData("key#hash")]
    public void IsValidKey_WithInvalidPatterns_ReturnsFalseAndThrows(string? key)
    {
        Assert.False(SecretKeyValidator.IsValidKey(key));
        Assert.Throws<VaultValidationException>(() => SecretKeyValidator.ValidateKey(key));
    }

    [Fact]
    public void ValidateKey_Exceeding128Characters_ThrowsVaultValidationException()
    {
        var longKey = new string('a', 129);
        Assert.False(SecretKeyValidator.IsValidKey(longKey));
        var ex = Assert.Throws<VaultValidationException>(() => SecretKeyValidator.ValidateKey(longKey));
        Assert.Contains("exceeds maximum allowed length", ex.Message);
    }

    [Fact]
    public void ValidateKey_Exact128Characters_Succeeds()
    {
        var validLongKey = new string('a', 128);
        Assert.True(SecretKeyValidator.IsValidKey(validLongKey));
        SecretKeyValidator.ValidateKey(validLongKey);
    }

    [Fact]
    public void ValidateValue_NullValue_ThrowsVaultValidationException()
    {
        Assert.Throws<VaultValidationException>(() => SecretKeyValidator.ValidateValue(null));
    }

    [Fact]
    public void ValidateValue_EmptyValue_Succeeds()
    {
        // Should not throw (empty values are allowed, e.g. for empty config flags)
        SecretKeyValidator.ValidateValue(string.Empty);
    }

    [Fact]
    public void ValidateValue_Exceeding1MiB_ThrowsVaultValidationException()
    {
        // 1 MiB = 1024 * 1024 bytes = 1,048,576 bytes.
        var oversizeValue = new string('x', (1024 * 1024) + 1);
        var ex = Assert.Throws<VaultValidationException>(() => SecretKeyValidator.ValidateValue(oversizeValue));
        Assert.Contains("exceeds maximum allowed size", ex.Message);
    }

    [Fact]
    public void ValidateValue_Exact1MiB_Succeeds()
    {
        var exactValue = new string('x', 1024 * 1024);
        SecretKeyValidator.ValidateValue(exactValue);
    }
}
