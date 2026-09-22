using System.Buffers.Binary;
using System.Buffers.Text;
using Vault.Core.Exceptions;

namespace Vault.Core.Sharing;

/// <summary>
/// Immutable representation and binary serializer for ephemeral secret sharing envelopes.
/// </summary>
public sealed record SharingEnvelope
{
    public const byte CurrentVersion = 1;
    public const int HeaderSize = 41; // 1 + 8 + 4 + 12 + 16
    public const int AadSize = 25;    // 1 + 8 + 4 + 12
    public const int NonceSizeBytes = 12;
    public const int TagSizeBytes = 16;

    public byte Version { get; init; } = CurrentVersion;
    public DateTimeOffset CreatedAt { get; init; }
    public TimeSpan Ttl { get; init; }
    public byte[] Nonce { get; init; }
    public byte[] Tag { get; init; }
    public byte[] Ciphertext { get; init; }

    public DateTimeOffset ExpiresAt => CreatedAt.Add(Ttl);

    public bool IsExpired => IsExpiredAt(DateTimeOffset.UtcNow);

    public bool IsExpiredAt(DateTimeOffset referenceTime) => referenceTime > ExpiresAt;

    public SharingEnvelope(
        DateTimeOffset createdAt,
        TimeSpan ttl,
        byte[] nonce,
        byte[] tag,
        byte[] ciphertext,
        byte version = CurrentVersion)
    {
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(ciphertext);

        if (nonce.Length != NonceSizeBytes)
        {
            throw new ArgumentException($"Nonce must be {NonceSizeBytes} bytes.", nameof(nonce));
        }

        if (tag.Length != TagSizeBytes)
        {
            throw new ArgumentException($"Tag must be {TagSizeBytes} bytes.", nameof(tag));
        }

        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentException("TTL must be greater than zero.", nameof(ttl));
        }

        Version = version;
        CreatedAt = createdAt;
        Ttl = ttl;
        Nonce = nonce;
        Tag = tag;
        Ciphertext = ciphertext;
    }

    /// <summary>
    /// Writes the 25-byte Additional Authenticated Data (AAD) into the destination span.
    /// </summary>
    public void WriteAssociatedData(Span<byte> destination)
    {
        if (destination.Length < AadSize)
        {
            throw new ArgumentException($"Destination span too small. Required: {AadSize} bytes.", nameof(destination));
        }

        destination[0] = Version;
        BinaryPrimitives.WriteInt64LittleEndian(destination.Slice(1, 8), CreatedAt.ToUnixTimeSeconds());
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(9, 4), (uint)Math.Max(1, (long)Ttl.TotalSeconds));
        Nonce.CopyTo(destination.Slice(13, 12));
    }

    /// <summary>
    /// Generates and returns the 25-byte Additional Authenticated Data (AAD) for the envelope.
    /// </summary>
    public byte[] GetAssociatedData()
    {
        var aad = new byte[AadSize];
        WriteAssociatedData(aad);
        return aad;
    }

    /// <summary>
    /// Serializes the entire envelope (header + ciphertext) to a binary byte array.
    /// </summary>
    public byte[] ToBytes()
    {
        var buffer = new byte[HeaderSize + Ciphertext.Length];
        WriteAssociatedData(buffer.AsSpan(0, AadSize));
        Tag.CopyTo(buffer.AsSpan(25, TagSizeBytes));
        Ciphertext.CopyTo(buffer.AsSpan(HeaderSize));
        return buffer;
    }

    /// <summary>
    /// Encodes the binary envelope into a Base64URL string token.
    /// </summary>
    public string ToBase64Url()
    {
        var bytes = ToBytes();
        return Base64Url.EncodeToString(bytes);
    }

    /// <summary>
    /// Deserializes a binary envelope from raw bytes.
    /// </summary>
    public static SharingEnvelope ReadFrom(ReadOnlySpan<byte> source)
    {
        if (source.Length < HeaderSize)
        {
            throw new VaultCorruptedException($"Envelope is truncated. Expected at least {HeaderSize} bytes, found {source.Length}.");
        }

        var version = source[0];
        if (version != CurrentVersion)
        {
            throw new VaultCorruptedException($"Unsupported envelope version '{version}'. Only version {CurrentVersion} is supported.");
        }

        var unixSeconds = BinaryPrimitives.ReadInt64LittleEndian(source.Slice(1, 8));
        var ttlSeconds = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(9, 4));

        var createdAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        var ttl = TimeSpan.FromSeconds(ttlSeconds);

        var nonce = source.Slice(13, 12).ToArray();
        var tag = source.Slice(25, 16).ToArray();
        var ciphertext = source.Slice(HeaderSize).ToArray();

        return new SharingEnvelope(createdAt, ttl, nonce, tag, ciphertext, version);
    }

    /// <summary>
    /// Parses an envelope from a Base64URL string token.
    /// </summary>
    public static SharingEnvelope FromBase64Url(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        try
        {
            var bytes = Base64Url.DecodeFromChars(token.Trim());
            return ReadFrom(bytes);
        }
        catch (FormatException ex)
        {
            throw new VaultValidationException("Invalid Base64URL sharing envelope format.", ex);
        }
    }
}
