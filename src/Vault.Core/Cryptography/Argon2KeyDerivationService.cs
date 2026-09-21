using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Vault.Core.Cryptography;

/// <summary>
/// Implements key derivation using the Argon2id memory-hard algorithm.
/// </summary>
public sealed class Argon2KeyDerivationService : IKeyDerivationService
{
    public byte[] GenerateSalt(int sizeBytes = Argon2Parameters.DefaultSaltSizeBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sizeBytes, 16);
        return RandomNumberGenerator.GetBytes(sizeBytes);
    }

    public SecureBuffer DeriveKey(ReadOnlySpan<char> passphrase, ReadOnlySpan<byte> salt, Argon2Parameters? parameters = null)
    {
        var utf8ByteCount = Encoding.UTF8.GetByteCount(passphrase);
        byte[]? tempUtf8 = null;
        try
        {
            tempUtf8 = new byte[utf8ByteCount];
            Encoding.UTF8.GetBytes(passphrase, tempUtf8);
            return DeriveKey(tempUtf8, salt, parameters);
        }
        finally
        {
            if (tempUtf8 != null)
            {
                CryptographicOperations.ZeroMemory(tempUtf8);
            }
        }
    }

    public SecureBuffer DeriveKey(ReadOnlySpan<byte> passphraseBytes, ReadOnlySpan<byte> salt, Argon2Parameters? parameters = null)
    {
        var p = parameters ?? Argon2Parameters.Default;
        var saltArray = salt.ToArray();
        var rawPassphrase = passphraseBytes.ToArray();

        try
        {
            using var argon2 = new Argon2id(rawPassphrase)
            {
                Salt = saltArray,
                DegreeOfParallelism = p.Parallelism,
                Iterations = p.Iterations,
                MemorySize = p.MemorySizeKiB
            };

            var keyBytes = argon2.GetBytes(p.KeySizeBytes);
            return new SecureBuffer(keyBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rawPassphrase);
        }
    }
}
