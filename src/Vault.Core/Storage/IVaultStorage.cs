namespace Vault.Core.Storage;

/// <summary>
/// Contract for atomic file persistence and retrieval of encrypted vault containers.
/// </summary>
public interface IVaultStorage
{
    /// <summary>
    /// Determines whether a vault file exists at the specified path.
    /// </summary>
    bool Exists(string filePath);

    /// <summary>
    /// Loads and parses the vault header and ciphertext from the specified file.
    /// </summary>
    Task<(VaultHeader Header, byte[] Ciphertext)> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically writes the vault header and ciphertext to disk using staging replacement and file locks.
    /// </summary>
    Task SaveAsync(
        string filePath,
        VaultHeader header,
        byte[] ciphertext,
        CancellationToken cancellationToken = default);
}
