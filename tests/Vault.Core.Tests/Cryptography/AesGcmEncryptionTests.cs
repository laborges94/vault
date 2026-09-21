using System.Security.Cryptography;
using System.Text;
using Vault.Core.Cryptography;
using Vault.Core.Exceptions;

namespace Vault.Core.Tests.Cryptography;

public class AesGcmEncryptionTests
{
    private readonly IAeadEncryptionService _encryptionService = new AesGcmEncryptionService();

    [Fact]
    public void EncryptAndDecrypt_ValidPayloadAndKey_RecoversExactPlaintext()
    {
        // Arrange
        var key = RandomNumberGenerator.GetBytes(IAeadEncryptionService.KeySizeBytes);
        var originalPlaintext = Encoding.UTF8.GetBytes("SuperSecretDatabasePassword_!@#123");
        var associatedData = Encoding.UTF8.GetBytes("HeaderMetaDataV1");

        // Act
        var result = _encryptionService.Encrypt(originalPlaintext, key, associatedData);
        using var decrypted = _encryptionService.Decrypt(result.Ciphertext, key, result.Nonce, result.Tag, associatedData);

        // Assert
        Assert.Equal(IAeadEncryptionService.NonceSizeBytes, result.Nonce.Length);
        Assert.Equal(IAeadEncryptionService.TagSizeBytes, result.Tag.Length);
        Assert.Equal(originalPlaintext.Length, result.Ciphertext.Length);
        Assert.True(originalPlaintext.AsSpan().SequenceEqual(decrypted.Span));
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsVaultAuthenticationException()
    {
        // Arrange
        var key = RandomNumberGenerator.GetBytes(IAeadEncryptionService.KeySizeBytes);
        var plaintext = Encoding.UTF8.GetBytes("Sensitive confidential data");
        var result = _encryptionService.Encrypt(plaintext, key);

        // Tamper with 1 byte of ciphertext
        result.Ciphertext[0] ^= 0x01;

        // Act & Assert
        Assert.Throws<VaultAuthenticationException>(() =>
            _encryptionService.Decrypt(result.Ciphertext, key, result.Nonce, result.Tag));
    }

    [Fact]
    public void Decrypt_TamperedAssociatedData_ThrowsVaultAuthenticationException()
    {
        // Arrange
        var key = RandomNumberGenerator.GetBytes(IAeadEncryptionService.KeySizeBytes);
        var plaintext = Encoding.UTF8.GetBytes("Sensitive confidential data");
        var originalAad = Encoding.UTF8.GetBytes("VaultHeader-Version1");
        var tamperedAad = Encoding.UTF8.GetBytes("VaultHeader-Version2");

        var result = _encryptionService.Encrypt(plaintext, key, originalAad);

        // Act & Assert
        Assert.Throws<VaultAuthenticationException>(() =>
            _encryptionService.Decrypt(result.Ciphertext, key, result.Nonce, result.Tag, tamperedAad));
    }

    [Fact]
    public void Decrypt_TamperedTag_ThrowsVaultAuthenticationException()
    {
        // Arrange
        var key = RandomNumberGenerator.GetBytes(IAeadEncryptionService.KeySizeBytes);
        var plaintext = Encoding.UTF8.GetBytes("Sensitive confidential data");
        var result = _encryptionService.Encrypt(plaintext, key);

        // Tamper with authentication tag
        result.Tag[0] ^= 0xFF;

        // Act & Assert
        Assert.Throws<VaultAuthenticationException>(() =>
            _encryptionService.Decrypt(result.Ciphertext, key, result.Nonce, result.Tag));
    }

    [Fact]
    public void Decrypt_TamperedNonce_ThrowsVaultAuthenticationException()
    {
        // Arrange
        var key = RandomNumberGenerator.GetBytes(IAeadEncryptionService.KeySizeBytes);
        var plaintext = Encoding.UTF8.GetBytes("Sensitive confidential data");
        var result = _encryptionService.Encrypt(plaintext, key);

        // Tamper with nonce
        result.Nonce[0] ^= 0x02;

        // Act & Assert
        Assert.Throws<VaultAuthenticationException>(() =>
            _encryptionService.Decrypt(result.Ciphertext, key, result.Nonce, result.Tag));
    }

    [Fact]
    public void Decrypt_WrongKey_ThrowsVaultAuthenticationException()
    {
        // Arrange
        var correctKey = RandomNumberGenerator.GetBytes(IAeadEncryptionService.KeySizeBytes);
        var wrongKey = RandomNumberGenerator.GetBytes(IAeadEncryptionService.KeySizeBytes);
        var plaintext = Encoding.UTF8.GetBytes("Sensitive confidential data");
        var result = _encryptionService.Encrypt(plaintext, correctKey);

        // Act & Assert
        Assert.Throws<VaultAuthenticationException>(() =>
            _encryptionService.Decrypt(result.Ciphertext, wrongKey, result.Nonce, result.Tag));
    }

    [Fact]
    public void Encrypt_InvalidKeySize_ThrowsArgumentException()
    {
        var invalidKey = new byte[16]; // 128 bits instead of 256
        var plaintext = Encoding.UTF8.GetBytes("data");

        Assert.Throws<ArgumentException>(() => _encryptionService.Encrypt(plaintext, invalidKey));
    }
}
