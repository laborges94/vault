using Vault.Core.Cryptography;
using Vault.Core.Model;

namespace Vault.Core.Services;

/// <summary>
/// Application service providing core vault operations and secret lifecycle management.
/// </summary>
public interface IVaultService
{
    /// <summary>
    /// Initializes a new encrypted vault at the specified file path.
    /// </summary>
    Task InitAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        Argon2Parameters? parameters = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default);

    Task InitAsync(
        string filePath,
        string passphrase,
        Argon2Parameters? parameters = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default) =>
        InitAsync(filePath, (passphrase ?? string.Empty).AsMemory(), parameters, overwrite, cancellationToken);

    /// <summary>
    /// Stores or updates a secret within the specified environment namespace.
    /// </summary>
    Task SetSecretAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        string key,
        string value,
        string? environment = null,
        string? description = null,
        IReadOnlyList<string>? tags = null,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default);

    Task SetSecretAsync(
        string filePath,
        string passphrase,
        string key,
        string value,
        string? environment = null,
        string? description = null,
        IReadOnlyList<string>? tags = null,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default) =>
        SetSecretAsync(filePath, (passphrase ?? string.Empty).AsMemory(), key, value, environment, description, tags, expiresAt, cancellationToken);

    /// <summary>
    /// Retrieves a decrypted secret entry from the specified environment.
    /// </summary>
    Task<SecretEntry> GetSecretAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        string key,
        string? environment = null,
        CancellationToken cancellationToken = default);

    Task<SecretEntry> GetSecretAsync(
        string filePath,
        string passphrase,
        string key,
        string? environment = null,
        CancellationToken cancellationToken = default) =>
        GetSecretAsync(filePath, (passphrase ?? string.Empty).AsMemory(), key, environment, cancellationToken);

    /// <summary>
    /// Lists all secrets within the specified environment namespace.
    /// </summary>
    Task<IReadOnlyDictionary<string, SecretEntry>> ListSecretsAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        string? environment = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, SecretEntry>> ListSecretsAsync(
        string filePath,
        string passphrase,
        string? environment = null,
        CancellationToken cancellationToken = default) =>
        ListSecretsAsync(filePath, (passphrase ?? string.Empty).AsMemory(), environment, cancellationToken);

    /// <summary>
    /// Lists all environment names configured within the vault.
    /// </summary>
    Task<IReadOnlyList<string>> ListEnvironmentsAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListEnvironmentsAsync(
        string filePath,
        string passphrase,
        CancellationToken cancellationToken = default) =>
        ListEnvironmentsAsync(filePath, (passphrase ?? string.Empty).AsMemory(), cancellationToken);

    /// <summary>
    /// Removes a secret from the specified environment namespace.
    /// </summary>
    Task DeleteSecretAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        string key,
        string? environment = null,
        CancellationToken cancellationToken = default);

    Task DeleteSecretAsync(
        string filePath,
        string passphrase,
        string key,
        string? environment = null,
        CancellationToken cancellationToken = default) =>
        DeleteSecretAsync(filePath, (passphrase ?? string.Empty).AsMemory(), key, environment, cancellationToken);

    /// <summary>
    /// Decrypts and loads the entire vault document model into memory.
    /// </summary>
    Task<VaultDocument> LoadDocumentAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        CancellationToken cancellationToken = default);

    Task<VaultDocument> LoadDocumentAsync(
        string filePath,
        string passphrase,
        CancellationToken cancellationToken = default) =>
        LoadDocumentAsync(filePath, (passphrase ?? string.Empty).AsMemory(), cancellationToken);

    /// <summary>
    /// Encrypts and saves the complete vault document to disk.
    /// </summary>
    Task SaveDocumentAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        VaultDocument document,
        CancellationToken cancellationToken = default);

    Task SaveDocumentAsync(
        string filePath,
        string passphrase,
        VaultDocument document,
        CancellationToken cancellationToken = default) =>
        SaveDocumentAsync(filePath, (passphrase ?? string.Empty).AsMemory(), document, cancellationToken);

    /// <summary>
    /// Ingests key-value pairs from .env formatted content into the vault under the specified environment.
    /// Returns duplicate key warnings if any were encountered.
    /// </summary>
    Task<IReadOnlyList<string>> PushEnvAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        string dotEnvContent,
        string? environment = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> PushEnvAsync(
        string filePath,
        string passphrase,
        string dotEnvContent,
        string? environment = null,
        CancellationToken cancellationToken = default) =>
        PushEnvAsync(filePath, (passphrase ?? string.Empty).AsMemory(), dotEnvContent, environment, cancellationToken);

    /// <summary>
    /// Ingests key-value pairs from a local .env file into the vault under the specified environment.
    /// Returns duplicate key warnings if any were encountered.
    /// </summary>
    Task<IReadOnlyList<string>> PushEnvFileAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        string envFilePath,
        string? environment = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> PushEnvFileAsync(
        string filePath,
        string passphrase,
        string envFilePath,
        string? environment = null,
        CancellationToken cancellationToken = default) =>
        PushEnvFileAsync(filePath, (passphrase ?? string.Empty).AsMemory(), envFilePath, environment, cancellationToken);

    /// <summary>
    /// Exports secrets from the specified environment as standard .env formatted content.
    /// </summary>
    Task<string> PullEnvAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        string? environment = null,
        CancellationToken cancellationToken = default);

    Task<string> PullEnvAsync(
        string filePath,
        string passphrase,
        string? environment = null,
        CancellationToken cancellationToken = default) =>
        PullEnvAsync(filePath, (passphrase ?? string.Empty).AsMemory(), environment, cancellationToken);

    /// <summary>
    /// Exports secrets from the specified environment to a local .env file.
    /// </summary>
    Task PullEnvFileAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        string envFilePath,
        string? environment = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default);

    Task PullEnvFileAsync(
        string filePath,
        string passphrase,
        string envFilePath,
        string? environment = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default) =>
        PullEnvFileAsync(filePath, (passphrase ?? string.Empty).AsMemory(), envFilePath, environment, overwrite, cancellationToken);

    /// <summary>
    /// Decrypts secrets from the specified environment and injects them directly into child process environment variables.
    /// </summary>
    Task<int> RunWithSecretsAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        string command,
        IReadOnlyList<string> arguments,
        string? environment = null,
        string? workingDirectory = null,
        TextReader? standardInput = null,
        TextWriter? standardOutput = null,
        TextWriter? standardError = null,
        CancellationToken cancellationToken = default);

    Task<int> RunWithSecretsAsync(
        string filePath,
        string passphrase,
        string command,
        IReadOnlyList<string> arguments,
        string? environment = null,
        string? workingDirectory = null,
        TextReader? standardInput = null,
        TextWriter? standardOutput = null,
        TextWriter? standardError = null,
        CancellationToken cancellationToken = default) =>
        RunWithSecretsAsync(
            filePath,
            (passphrase ?? string.Empty).AsMemory(),
            command,
            arguments,
            environment,
            workingDirectory,
            standardInput,
            standardOutput,
            standardError,
            cancellationToken);
}
