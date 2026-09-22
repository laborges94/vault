using System.Buffers.Binary;
using Vault.Core.Cryptography;
using Vault.Core.Exceptions;

namespace Vault.Core.Storage;

/// <summary>
/// Deterministic binary header layout for .vault.enc container files.
/// </summary>
public sealed record VaultHeader
{
    public static readonly byte[] MagicBytes = "VAULT"u8.ToArray();
    public const ushort CurrentVersion = 1;
    public const int HeaderSize = 63;
    public const int AadSize = 47;
    public const int SaltSizeBytes = 16;
    public const int NonceSizeBytes = 12;
    public const int TagSizeBytes = 16;

    public ushort Version { get; init; } = CurrentVersion;
    public byte[] Salt { get; init; }
    public Argon2Parameters Argon2Params { get; init; }
    public byte[] Nonce { get; init; }
    public byte[] Tag { get; init; }

    public VaultHeader(
        byte[] salt,
        Argon2Parameters argon2Params,
        byte[] nonce,
        byte[] tag,
        ushort version = CurrentVersion)
    {
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentNullException.ThrowIfNull(argon2Params);
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentNullException.ThrowIfNull(tag);

        if (salt.Length != SaltSizeBytes)
        {
            throw new ArgumentException($"Salt must be {SaltSizeBytes} bytes.", nameof(salt));
        }

        if (nonce.Length != NonceSizeBytes)
        {
            throw new ArgumentException($"Nonce must be {NonceSizeBytes} bytes.", nameof(nonce));
        }

        if (tag.Length != TagSizeBytes)
        {
            throw new ArgumentException($"Tag must be {TagSizeBytes} bytes.", nameof(tag));
        }

        Salt = salt;
        Argon2Params = argon2Params;
        Nonce = nonce;
        Tag = tag;
        Version = version;
    }

    /// <summary>
    /// Writes the 47-byte Additional Authenticated Data (AAD) into the specified span.
    /// </summary>
    public void WriteAssociatedData(Span<byte> destination)
    {
        if (destination.Length < AadSize)
        {
            throw new ArgumentException($"Destination span too small. Required: {AadSize} bytes.", nameof(destination));
        }

        MagicBytes.CopyTo(destination[..5]);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(5, 2), Version);
        Salt.CopyTo(destination.Slice(7, 16));
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(23, 4), (uint)Argon2Params.Iterations);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(27, 4), (uint)Argon2Params.MemorySizeKiB);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(31, 4), (uint)Argon2Params.Parallelism);
        Nonce.CopyTo(destination.Slice(35, 12));
    }

    /// <summary>
    /// Generates and returns the 47-byte Additional Authenticated Data (AAD) for AES-256-GCM AEAD binding.
    /// </summary>
    public byte[] GetAssociatedData()
    {
        var aad = new byte[AadSize];
        WriteAssociatedData(aad);
        return aad;
    }

    /// <summary>
    /// Writes the full 63-byte binary header into the specified span.
    /// </summary>
    public void WriteTo(Span<byte> destination)
    {
        if (destination.Length < HeaderSize)
        {
            throw new ArgumentException($"Destination span too small. Required: {HeaderSize} bytes.", nameof(destination));
        }

        WriteAssociatedData(destination[..AadSize]);
        Tag.CopyTo(destination.Slice(47, 16));
    }

    /// <summary>
    /// Serializes the header into a 63-byte array.
    /// </summary>
    public byte[] ToBytes()
    {
        var buffer = new byte[HeaderSize];
        WriteTo(buffer);
        return buffer;
    }

    /// <summary>
    /// Parses a binary header from the given byte span.
    /// </summary>
    public static VaultHeader ReadFrom(ReadOnlySpan<byte> source)
    {
        if (source.Length < HeaderSize)
        {
            throw new VaultCorruptedException($"Vault file header is truncated or incomplete. Expected at least {HeaderSize} bytes, found {source.Length}.");
        }

        if (!source[..5].SequenceEqual(MagicBytes))
        {
            throw new VaultCorruptedException("Invalid vault file format. Missing magic bytes 'VAULT'.");
        }

        var version = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(5, 2));
        if (version != CurrentVersion)
        {
            throw new VaultCorruptedException($"Unsupported vault format version '{version}'. Only version {CurrentVersion} is supported.");
        }

        var salt = source.Slice(7, 16).ToArray();
        var iterations = (int)BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(23, 4));
        var memorySizeKiB = (int)BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(27, 4));
        var parallelism = (int)BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(31, 4));

        if (iterations <= 0 || memorySizeKiB <= 0 || parallelism <= 0)
        {
            throw new VaultCorruptedException("Invalid Argon2id parameters encoded in vault header.");
        }

        var nonce = source.Slice(35, 12).ToArray();
        var tag = source.Slice(47, 16).ToArray();

        var argon2Params = new Argon2Parameters
        {
            Iterations = iterations,
            MemorySizeKiB = memorySizeKiB,
            Parallelism = parallelism,
            SaltSizeBytes = salt.Length
        };

        return new VaultHeader(salt, argon2Params, nonce, tag, version);
    }
}
