namespace Vault.Core.Cryptography;

/// <summary>
/// Configuration parameters for Argon2id key derivation.
/// </summary>
public sealed record Argon2Parameters
{
    public const int DefaultSaltSizeBytes = 16;
    public const int DefaultIterations = 3;
    public const int DefaultMemorySizeKiB = 65536; // 64 MiB
    public const int DefaultParallelism = 4;
    public const int DefaultKeySizeBytes = 32; // 256 bits

    public int Iterations { get; init; } = DefaultIterations;
    public int MemorySizeKiB { get; init; } = DefaultMemorySizeKiB;
    public int Parallelism { get; init; } = DefaultParallelism;
    public int SaltSizeBytes { get; init; } = DefaultSaltSizeBytes;
    public int KeySizeBytes { get; init; } = DefaultKeySizeBytes;

    public static Argon2Parameters Default { get; } = new();

    /// <summary>
    /// Lightweight configuration for fast unit test execution.
    /// </summary>
    public static Argon2Parameters ForTesting { get; } = new()
    {
        Iterations = 1,
        MemorySizeKiB = 1024,
        Parallelism = 1,
        SaltSizeBytes = 16,
        KeySizeBytes = 32
    };
}
