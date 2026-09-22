using Vault.Core.Cryptography;
using Vault.Core.Exceptions;
using Vault.Core.Storage;

namespace Vault.Core.Tests.Storage;

public class VaultHeaderTests
{
    private static VaultHeader CreateSampleHeader()
    {
        var salt = new byte[16];
        var nonce = new byte[12];
        var tag = new byte[16];
        for (int i = 0; i < salt.Length; i++) salt[i] = (byte)(i + 1);
        for (int i = 0; i < nonce.Length; i++) nonce[i] = (byte)(i + 10);
        for (int i = 0; i < tag.Length; i++) tag[i] = (byte)(i + 20);

        return new VaultHeader(salt, Argon2Parameters.ForTesting, nonce, tag);
    }

    [Fact]
    public void SerializeAndDeserialize_PreservesAllFields()
    {
        // Arrange
        var original = CreateSampleHeader();

        // Act
        var bytes = original.ToBytes();
        var parsed = VaultHeader.ReadFrom(bytes);

        // Assert
        Assert.Equal(VaultHeader.HeaderSize, bytes.Length);
        Assert.Equal(original.Version, parsed.Version);
        Assert.Equal(original.Salt, parsed.Salt);
        Assert.Equal(original.Nonce, parsed.Nonce);
        Assert.Equal(original.Tag, parsed.Tag);
        Assert.Equal(original.Argon2Params.Iterations, parsed.Argon2Params.Iterations);
        Assert.Equal(original.Argon2Params.MemorySizeKiB, parsed.Argon2Params.MemorySizeKiB);
        Assert.Equal(original.Argon2Params.Parallelism, parsed.Argon2Params.Parallelism);
    }

    [Fact]
    public void ReadFrom_InvalidMagicBytes_ThrowsVaultCorruptedException()
    {
        var header = CreateSampleHeader();
        var bytes = header.ToBytes();
        bytes[0] = (byte)'X'; // Corrupt 'V' to 'X'

        var ex = Assert.Throws<VaultCorruptedException>(() => VaultHeader.ReadFrom(bytes));
        Assert.Contains("Missing magic bytes", ex.Message);
    }

    [Fact]
    public void ReadFrom_UnsupportedVersion_ThrowsVaultCorruptedException()
    {
        var header = CreateSampleHeader();
        var bytes = header.ToBytes();
        bytes[5] = 99; // Change version to 99

        var ex = Assert.Throws<VaultCorruptedException>(() => VaultHeader.ReadFrom(bytes));
        Assert.Contains("Unsupported vault format version", ex.Message);
    }

    [Fact]
    public void ReadFrom_TruncatedBytes_ThrowsVaultCorruptedException()
    {
        var truncated = new byte[62]; // 1 byte short of 63

        var ex = Assert.Throws<VaultCorruptedException>(() => VaultHeader.ReadFrom(truncated));
        Assert.Contains("truncated or incomplete", ex.Message);
    }

    [Fact]
    public void GetAssociatedData_Returns47BytesWithoutTag()
    {
        // Arrange
        var header = CreateSampleHeader();

        // Act
        var aad = header.GetAssociatedData();

        // Assert
        Assert.Equal(VaultHeader.AadSize, aad.Length);
        // Header bytes 0..46 must match AAD
        var fullBytes = header.ToBytes();
        Assert.True(fullBytes.AsSpan(0, VaultHeader.AadSize).SequenceEqual(aad));
    }

    [Fact]
    public void Constructor_InvalidLengths_ThrowsArgumentException()
    {
        var validSalt = new byte[16];
        var validNonce = new byte[12];
        var validTag = new byte[16];

        Assert.Throws<ArgumentException>(() => new VaultHeader(new byte[15], Argon2Parameters.Default, validNonce, validTag));
        Assert.Throws<ArgumentException>(() => new VaultHeader(validSalt, Argon2Parameters.Default, new byte[11], validTag));
        Assert.Throws<ArgumentException>(() => new VaultHeader(validSalt, Argon2Parameters.Default, validNonce, new byte[15]));
    }
}
