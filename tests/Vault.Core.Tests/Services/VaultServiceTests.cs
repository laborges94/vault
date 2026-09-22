using Vault.Core.Cryptography;
using Vault.Core.Exceptions;
using Vault.Core.Services;
using Vault.Core.Storage;

namespace Vault.Core.Tests.Services;

public class VaultServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly IVaultService _vaultService;
    private readonly Argon2Parameters _testParams = Argon2Parameters.ForTesting;

    public VaultServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "VaultServiceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        var storage = new FileVaultStorage(
            lockTimeout: TimeSpan.FromMilliseconds(500),
            retryInterval: TimeSpan.FromMilliseconds(20));
        var keyDerivation = new Argon2KeyDerivationService();
        var encryption = new AesGcmEncryptionService();

        _vaultService = new VaultService(storage, keyDerivation, encryption);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task InitAsync_CreatesNewEncryptedVaultFile()
    {
        var filePath = Path.Combine(_tempDirectory, "test.vault.enc");

        await _vaultService.InitAsync(filePath, "StrongPassphrase123!", _testParams);

        Assert.True(File.Exists(filePath));
        var rawBytes = await File.ReadAllBytesAsync(filePath);
        Assert.True(rawBytes.Length >= VaultHeader.HeaderSize);
        // Header starts with VAULT
        Assert.Equal(VaultHeader.MagicBytes, rawBytes[..5]);
    }

    [Fact]
    public async Task InitAsync_FileAlreadyExists_ThrowsUnlessOverwrite()
    {
        var filePath = Path.Combine(_tempDirectory, "exists.vault.enc");
        await _vaultService.InitAsync(filePath, "pass", _testParams);

        // Try init without overwrite
        await Assert.ThrowsAsync<VaultException>(() =>
            _vaultService.InitAsync(filePath, "pass2", _testParams, overwrite: false));

        // With overwrite: true -> succeeds
        await _vaultService.InitAsync(filePath, "pass2", _testParams, overwrite: true);
    }

    [Fact]
    public async Task SetAndGetSecret_RoundTrip_PreservesValueAndMetadata()
    {
        var filePath = Path.Combine(_tempDirectory, "crud.vault.enc");
        var pass = "my_passphrase";
        await _vaultService.InitAsync(filePath, pass, _testParams);

        var tags = new[] { "db", "primary" };
        var expires = DateTimeOffset.UtcNow.AddDays(7);

        await _vaultService.SetSecretAsync(
            filePath,
            pass,
            key: "DATABASE_URL",
            value: "postgres://user:pass@localhost/db",
            environment: "staging",
            description: "Main DB URL",
            tags: tags,
            expiresAt: expires);

        var secret = await _vaultService.GetSecretAsync(filePath, pass, "DATABASE_URL", "staging");

        Assert.Equal("postgres://user:pass@localhost/db", secret.Value);
        Assert.Equal("Main DB URL", secret.Description);
        Assert.Equal(tags, secret.Tags);
        Assert.Equal(expires, secret.ExpiresAt);
    }

    [Fact]
    public async Task MultipleEnvironments_IsolatesSecrets()
    {
        var filePath = Path.Combine(_tempDirectory, "envs.vault.enc");
        var pass = "pass";
        await _vaultService.InitAsync(filePath, pass, _testParams);

        await _vaultService.SetSecretAsync(filePath, pass, "API_KEY", "dev-token", environment: "development");
        await _vaultService.SetSecretAsync(filePath, pass, "API_KEY", "prod-token", environment: "production");

        var devSecret = await _vaultService.GetSecretAsync(filePath, pass, "API_KEY", "development");
        var prodSecret = await _vaultService.GetSecretAsync(filePath, pass, "API_KEY", "production");

        Assert.Equal("dev-token", devSecret.Value);
        Assert.Equal("prod-token", prodSecret.Value);

        // Secret should not exist in default env
        await Assert.ThrowsAsync<SecretNotFoundException>(() =>
            _vaultService.GetSecretAsync(filePath, pass, "API_KEY", "default"));
    }

    [Fact]
    public async Task GetSecret_WrongPassphrase_ThrowsVaultAuthenticationException()
    {
        var filePath = Path.Combine(_tempDirectory, "auth_fail.vault.enc");
        await _vaultService.InitAsync(filePath, "CorrectPassphrase", _testParams);
        await _vaultService.SetSecretAsync(filePath, "CorrectPassphrase", "SECRET", "val");

        await Assert.ThrowsAsync<VaultAuthenticationException>(() =>
            _vaultService.GetSecretAsync(filePath, "WrongPassphrase", "SECRET"));
    }

    [Fact]
    public async Task GetSecret_NonExistentKey_ThrowsSecretNotFoundException()
    {
        var filePath = Path.Combine(_tempDirectory, "not_found.vault.enc");
        await _vaultService.InitAsync(filePath, "pass", _testParams);

        await Assert.ThrowsAsync<SecretNotFoundException>(() =>
            _vaultService.GetSecretAsync(filePath, "pass", "DOES_NOT_EXIST"));
    }

    [Fact]
    public async Task ListSecretsAsync_ReturnsAllKeysInEnvironment()
    {
        var filePath = Path.Combine(_tempDirectory, "list.vault.enc");
        var pass = "pass";
        await _vaultService.InitAsync(filePath, pass, _testParams);

        await _vaultService.SetSecretAsync(filePath, pass, "SECRET_A", "1", "staging");
        await _vaultService.SetSecretAsync(filePath, pass, "SECRET_B", "2", "staging");
        await _vaultService.SetSecretAsync(filePath, pass, "SECRET_C", "3", "production");

        var stagingSecrets = await _vaultService.ListSecretsAsync(filePath, pass, "staging");

        Assert.Equal(2, stagingSecrets.Count);
        Assert.Contains("SECRET_A", stagingSecrets.Keys);
        Assert.Contains("SECRET_B", stagingSecrets.Keys);
        Assert.DoesNotContain("SECRET_C", stagingSecrets.Keys);
    }

    [Fact]
    public async Task ListEnvironmentsAsync_ReturnsAllUniqueEnvironments()
    {
        var filePath = Path.Combine(_tempDirectory, "env_list.vault.enc");
        var pass = "pass";
        await _vaultService.InitAsync(filePath, pass, _testParams);

        await _vaultService.SetSecretAsync(filePath, pass, "K1", "V1", "staging");
        await _vaultService.SetSecretAsync(filePath, pass, "K2", "V2", "production");

        var envs = await _vaultService.ListEnvironmentsAsync(filePath, pass);

        Assert.Contains("default", envs);
        Assert.Contains("staging", envs);
        Assert.Contains("production", envs);
    }

    [Fact]
    public async Task DeleteSecretAsync_RemovesSecretAndUpdatesVault()
    {
        var filePath = Path.Combine(_tempDirectory, "delete.vault.enc");
        var pass = "pass";
        await _vaultService.InitAsync(filePath, pass, _testParams);

        await _vaultService.SetSecretAsync(filePath, pass, "TO_DELETE", "val");
        Assert.NotNull(await _vaultService.GetSecretAsync(filePath, pass, "TO_DELETE"));

        await _vaultService.DeleteSecretAsync(filePath, pass, "TO_DELETE");

        await Assert.ThrowsAsync<SecretNotFoundException>(() =>
            _vaultService.GetSecretAsync(filePath, pass, "TO_DELETE"));
    }

    [Fact]
    public async Task DeleteSecretAsync_NonExistentKey_ThrowsSecretNotFoundException()
    {
        var filePath = Path.Combine(_tempDirectory, "delete_missing.vault.enc");
        var pass = "pass";
        await _vaultService.InitAsync(filePath, pass, _testParams);

        await Assert.ThrowsAsync<SecretNotFoundException>(() =>
            _vaultService.DeleteSecretAsync(filePath, pass, "MISSING"));
    }
}
