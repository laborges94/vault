using System.Text;
using Vault.Core.Cryptography;
using Vault.Core.Exceptions;
using Vault.Core.Storage;

namespace Vault.Core.Tests.Storage;

public class FileVaultStorageTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly FileVaultStorage _storage;

    public FileVaultStorageTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "VaultStorageTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _storage = new FileVaultStorage(
            lockTimeout: TimeSpan.FromMilliseconds(300),
            retryInterval: TimeSpan.FromMilliseconds(20));
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

    private static VaultHeader CreateHeader()
    {
        return new VaultHeader(
            new byte[16],
            Argon2Parameters.ForTesting,
            new byte[12],
            new byte[16]);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesHeaderAndCiphertext()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "test.vault.enc");
        var header = CreateHeader();
        var ciphertext = Encoding.UTF8.GetBytes("EncryptedCiphertextPayload_12345");

        // Act
        await _storage.SaveAsync(filePath, header, ciphertext);
        var (loadedHeader, loadedCiphertext) = await _storage.LoadAsync(filePath);

        // Assert
        Assert.True(_storage.Exists(filePath));
        Assert.Equal(header.Version, loadedHeader.Version);
        Assert.Equal(header.Salt, loadedHeader.Salt);
        Assert.Equal(header.Nonce, loadedHeader.Nonce);
        Assert.Equal(header.Tag, loadedHeader.Tag);
        Assert.Equal(ciphertext, loadedCiphertext);

        // Verify no temporary files remain
        var tmpFiles = Directory.GetFiles(_tempDirectory, "*.tmp.*");
        Assert.Empty(tmpFiles);
    }

    [Fact]
    public async Task SaveAsync_AutomaticallyCreatesNonExistentParentDirectories()
    {
        // Arrange
        var nestedPath = Path.Combine(_tempDirectory, "nested", "folder", "vault.enc");
        var header = CreateHeader();
        var ciphertext = new byte[] { 1, 2, 3 };

        // Act
        await _storage.SaveAsync(nestedPath, header, ciphertext);

        // Assert
        Assert.True(_storage.Exists(nestedPath));
    }

    [Fact]
    public async Task LoadAsync_NonExistentFile_ThrowsVaultNotFoundException()
    {
        var nonExistent = Path.Combine(_tempDirectory, "missing.vault.enc");
        await Assert.ThrowsAsync<VaultNotFoundException>(() => _storage.LoadAsync(nonExistent));
    }

    [Fact]
    public async Task LoadAsync_TruncatedFile_ThrowsVaultCorruptedException()
    {
        var corruptFile = Path.Combine(_tempDirectory, "corrupt.vault.enc");
        await File.WriteAllBytesAsync(corruptFile, new byte[20]); // Less than 63 bytes

        await Assert.ThrowsAsync<VaultCorruptedException>(() => _storage.LoadAsync(corruptFile));
    }

    [Fact]
    public async Task SaveAsync_LockedTargetFile_ThrowsVaultFileLockedExceptionAfterTimeout()
    {
        // Arrange
        var targetFile = Path.Combine(_tempDirectory, "locked.vault.enc");
        var header = CreateHeader();
        var ciphertext = new byte[] { 1, 2, 3 };

        // Pre-create the file and lock it exclusively
        await using var lockStream = new FileStream(targetFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<VaultFileLockedException>(() =>
            _storage.SaveAsync(targetFile, header, ciphertext));
        Assert.Contains("locked by another process", ex.Message);
    }

    [Fact]
    public async Task LoadAsync_LockedFile_ThrowsVaultFileLockedExceptionAfterTimeout()
    {
        // Arrange
        var targetFile = Path.Combine(_tempDirectory, "locked_read.vault.enc");
        await File.WriteAllBytesAsync(targetFile, new byte[100]);

        // Lock exclusively
        await using var lockStream = new FileStream(targetFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<VaultFileLockedException>(() =>
            _storage.LoadAsync(targetFile));
        Assert.Contains("locked by another process", ex.Message);
    }
}
