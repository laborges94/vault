using System.Diagnostics;
using Vault.Core.Exceptions;

namespace Vault.Core.Storage;

/// <summary>
/// Atomic file-based vault storage supporting staging replacements, concurrency locks, and corruption detection.
/// </summary>
public sealed class FileVaultStorage : IVaultStorage
{
    public static readonly TimeSpan DefaultLockTimeout = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan DefaultRetryInterval = TimeSpan.FromMilliseconds(50);

    private readonly TimeSpan _lockTimeout;
    private readonly TimeSpan _retryInterval;

    public FileVaultStorage(TimeSpan? lockTimeout = null, TimeSpan? retryInterval = null)
    {
        _lockTimeout = lockTimeout ?? DefaultLockTimeout;
        _retryInterval = retryInterval ?? DefaultRetryInterval;
    }

    public bool Exists(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return File.Exists(filePath);
    }

    public async Task<(VaultHeader Header, byte[] Ciphertext)> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new VaultNotFoundException($"Vault file '{filePath}' does not exist.");
        }

        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true);

                if (stream.Length < VaultHeader.HeaderSize)
                {
                    throw new VaultCorruptedException("Vault file is truncated or corrupted.");
                }

                var headerBytes = new byte[VaultHeader.HeaderSize];
                await stream.ReadExactlyAsync(headerBytes, cancellationToken).ConfigureAwait(false);

                var header = VaultHeader.ReadFrom(headerBytes);
                var cipherLength = (int)(stream.Length - VaultHeader.HeaderSize);
                var ciphertext = new byte[cipherLength];
                await stream.ReadExactlyAsync(ciphertext, cancellationToken).ConfigureAwait(false);

                return (header, ciphertext);
            }
            catch (VaultException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (stopwatch.Elapsed >= _lockTimeout)
                {
                    throw new VaultFileLockedException($"Vault file '{filePath}' is locked by another process.", ex);
                }

                await Task.Delay(_retryInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task SaveAsync(
        string filePath,
        VaultHeader header,
        byte[] ciphertext,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(ciphertext);

        var fullPath = Path.GetFullPath(filePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var stagingPath = Path.Combine(dir ?? string.Empty, $"{Path.GetFileName(fullPath)}.tmp.{Guid.NewGuid():N}");

        try
        {
            await using (var stream = new FileStream(
                stagingPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                var headerBytes = header.ToBytes();
                await stream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(ciphertext, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    File.Move(stagingPath, fullPath, overwrite: true);
                    break;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    if (stopwatch.Elapsed >= _lockTimeout)
                    {
                        throw new VaultFileLockedException($"Vault file '{filePath}' is locked by another process.", ex);
                    }

                    await Task.Delay(_retryInterval, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (File.Exists(stagingPath))
            {
                try
                {
                    File.Delete(stagingPath);
                }
                catch
                {
                    // Staging cleanup failure should not mask primary exceptions
                }
            }
        }
    }
}
