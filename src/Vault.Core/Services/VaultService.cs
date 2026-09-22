using System.Security.Cryptography;
using System.Text.Json;
using Vault.Core.Cryptography;
using Vault.Core.Env;
using Vault.Core.Exceptions;
using Vault.Core.Execution;
using Vault.Core.Model;
using Vault.Core.Storage;

namespace Vault.Core.Services;

/// <summary>
/// Implements vault lifecycle management and encrypted secret operations.
/// </summary>
public sealed class VaultService : IVaultService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IVaultStorage _storage;
    private readonly IKeyDerivationService _keyDerivation;
    private readonly IAeadEncryptionService _encryptionService;
    private readonly IProcessRunner _processRunner;

    public VaultService(
        IVaultStorage storage,
        IKeyDerivationService keyDerivation,
        IAeadEncryptionService encryptionService,
        IProcessRunner? processRunner = null)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _keyDerivation = keyDerivation ?? throw new ArgumentNullException(nameof(keyDerivation));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _processRunner = processRunner ?? new ProcessRunner();
    }

    public async Task InitAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        Argon2Parameters? parameters = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (_storage.Exists(filePath) && !overwrite)
        {
            throw new VaultException($"Vault file already exists at '{filePath}'. Use overwrite to replace it.");
        }

        var document = new VaultDocument();
        var p = parameters ?? Argon2Parameters.Default;
        var salt = _keyDerivation.GenerateSalt(p.SaltSizeBytes);

        using var keyBuffer = _keyDerivation.DeriveKey(passphrase.Span, salt, p);
        var nonce = _encryptionService.GenerateNonce();

        var header = new VaultHeader(salt, p, nonce, new byte[VaultHeader.TagSizeBytes]);
        var aad = header.GetAssociatedData();

        byte[]? jsonBytes = null;
        try
        {
            jsonBytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
            var encResult = _encryptionService.Encrypt(jsonBytes, keyBuffer.Span, aad, nonce);
            var finalHeader = header with { Tag = encResult.Tag };

            await _storage.SaveAsync(filePath, finalHeader, encResult.Ciphertext, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (jsonBytes != null)
            {
                CryptographicOperations.ZeroMemory(jsonBytes);
            }
        }
    }

    public Task InitAsync(
        string filePath,
        string passphrase,
        Argon2Parameters? parameters = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default) =>
        InitAsync(filePath, (passphrase ?? string.Empty).AsMemory(), parameters, overwrite, cancellationToken);

    public async Task<VaultDocument> LoadDocumentAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var (header, ciphertext) = await _storage.LoadAsync(filePath, cancellationToken).ConfigureAwait(false);
        using var keyBuffer = _keyDerivation.DeriveKey(passphrase.Span, header.Salt, header.Argon2Params);

        using var decryptedBuffer = _encryptionService.Decrypt(
            ciphertext,
            keyBuffer.Span,
            header.Nonce,
            header.Tag,
            header.GetAssociatedData());

        var document = JsonSerializer.Deserialize<VaultDocument>(decryptedBuffer.Span, JsonOptions);
        if (document == null)
        {
            throw new VaultCorruptedException("Decrypted vault payload could not be parsed.");
        }

        return document;
    }

    public Task<VaultDocument> LoadDocumentAsync(
        string filePath,
        string passphrase,
        CancellationToken cancellationToken = default) =>
        LoadDocumentAsync(filePath, (passphrase ?? string.Empty).AsMemory(), cancellationToken);

    public async Task SaveDocumentAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        VaultDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(document);

        var (header, _) = await _storage.LoadAsync(filePath, cancellationToken).ConfigureAwait(false);
        using var keyBuffer = _keyDerivation.DeriveKey(passphrase.Span, header.Salt, header.Argon2Params);

        document.ModifiedAt = DateTimeOffset.UtcNow;
        var nonce = _encryptionService.GenerateNonce();
        var newHeader = new VaultHeader(header.Salt, header.Argon2Params, nonce, new byte[VaultHeader.TagSizeBytes]);
        var aad = newHeader.GetAssociatedData();

        byte[]? jsonBytes = null;
        try
        {
            jsonBytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
            var encResult = _encryptionService.Encrypt(jsonBytes, keyBuffer.Span, aad, nonce);
            var finalHeader = newHeader with { Tag = encResult.Tag };

            await _storage.SaveAsync(filePath, finalHeader, encResult.Ciphertext, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (jsonBytes != null)
            {
                CryptographicOperations.ZeroMemory(jsonBytes);
            }
        }
    }

    public Task SaveDocumentAsync(
        string filePath,
        string passphrase,
        VaultDocument document,
        CancellationToken cancellationToken = default) =>
        SaveDocumentAsync(filePath, (passphrase ?? string.Empty).AsMemory(), document, cancellationToken);

    public async Task SetSecretAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        string key,
        string value,
        string? environment = null,
        string? description = null,
        IReadOnlyList<string>? tags = null,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        SecretKeyValidator.Validate(key, value);

        var (header, ciphertext) = await _storage.LoadAsync(filePath, cancellationToken).ConfigureAwait(false);
        using var keyBuffer = _keyDerivation.DeriveKey(passphrase.Span, header.Salt, header.Argon2Params);

        VaultDocument document;
        using (var decryptedBuffer = _encryptionService.Decrypt(
            ciphertext,
            keyBuffer.Span,
            header.Nonce,
            header.Tag,
            header.GetAssociatedData()))
        {
            document = JsonSerializer.Deserialize<VaultDocument>(decryptedBuffer.Span, JsonOptions)
                ?? throw new VaultCorruptedException("Decrypted vault payload could not be parsed.");
        }

        var entry = new SecretEntry(value, description, tags, DateTimeOffset.UtcNow, expiresAt);
        document.SetSecret(key, entry, environment);

        var nonce = _encryptionService.GenerateNonce();
        var newHeader = new VaultHeader(header.Salt, header.Argon2Params, nonce, new byte[VaultHeader.TagSizeBytes]);
        var aad = newHeader.GetAssociatedData();

        byte[]? jsonBytes = null;
        try
        {
            jsonBytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
            var encResult = _encryptionService.Encrypt(jsonBytes, keyBuffer.Span, aad, nonce);
            var finalHeader = newHeader with { Tag = encResult.Tag };

            await _storage.SaveAsync(filePath, finalHeader, encResult.Ciphertext, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (jsonBytes != null)
            {
                CryptographicOperations.ZeroMemory(jsonBytes);
            }
        }
    }

    public Task SetSecretAsync(
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

    public async Task<SecretEntry> GetSecretAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        string key,
        string? environment = null,
        CancellationToken cancellationToken = default)
    {
        SecretKeyValidator.ValidateKey(key);

        var document = await LoadDocumentAsync(filePath, passphrase, cancellationToken).ConfigureAwait(false);
        var env = string.IsNullOrWhiteSpace(environment) ? VaultDocument.DefaultEnvironment : environment.Trim();

        if (!document.TryGetSecret(key, env, out var entry) || entry == null)
        {
            throw new SecretNotFoundException($"Secret '{key}' was not found in environment '{env}'.");
        }

        return entry;
    }

    public Task<SecretEntry> GetSecretAsync(
        string filePath,
        string passphrase,
        string key,
        string? environment = null,
        CancellationToken cancellationToken = default) =>
        GetSecretAsync(filePath, (passphrase ?? string.Empty).AsMemory(), key, environment, cancellationToken);

    public async Task<IReadOnlyDictionary<string, SecretEntry>> ListSecretsAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        string? environment = null,
        CancellationToken cancellationToken = default)
    {
        var document = await LoadDocumentAsync(filePath, passphrase, cancellationToken).ConfigureAwait(false);
        var env = string.IsNullOrWhiteSpace(environment) ? VaultDocument.DefaultEnvironment : environment.Trim();

        if (document.Environments.TryGetValue(env, out var secrets))
        {
            return secrets.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        return new Dictionary<string, SecretEntry>();
    }

    public Task<IReadOnlyDictionary<string, SecretEntry>> ListSecretsAsync(
        string filePath,
        string passphrase,
        string? environment = null,
        CancellationToken cancellationToken = default) =>
        ListSecretsAsync(filePath, (passphrase ?? string.Empty).AsMemory(), environment, cancellationToken);

    public async Task<IReadOnlyList<string>> ListEnvironmentsAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        CancellationToken cancellationToken = default)
    {
        var document = await LoadDocumentAsync(filePath, passphrase, cancellationToken).ConfigureAwait(false);
        return document.Environments.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public Task<IReadOnlyList<string>> ListEnvironmentsAsync(
        string filePath,
        string passphrase,
        CancellationToken cancellationToken = default) =>
        ListEnvironmentsAsync(filePath, (passphrase ?? string.Empty).AsMemory(), cancellationToken);

    public async Task DeleteSecretAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        string key,
        string? environment = null,
        CancellationToken cancellationToken = default)
    {
        SecretKeyValidator.ValidateKey(key);

        var (header, ciphertext) = await _storage.LoadAsync(filePath, cancellationToken).ConfigureAwait(false);
        using var keyBuffer = _keyDerivation.DeriveKey(passphrase.Span, header.Salt, header.Argon2Params);

        VaultDocument document;
        using (var decryptedBuffer = _encryptionService.Decrypt(
            ciphertext,
            keyBuffer.Span,
            header.Nonce,
            header.Tag,
            header.GetAssociatedData()))
        {
            document = JsonSerializer.Deserialize<VaultDocument>(decryptedBuffer.Span, JsonOptions)
                ?? throw new VaultCorruptedException("Decrypted vault payload could not be parsed.");
        }

        var env = string.IsNullOrWhiteSpace(environment) ? VaultDocument.DefaultEnvironment : environment.Trim();
        if (!document.RemoveSecret(key, env))
        {
            throw new SecretNotFoundException($"Secret '{key}' was not found in environment '{env}'.");
        }

        var nonce = _encryptionService.GenerateNonce();
        var newHeader = new VaultHeader(header.Salt, header.Argon2Params, nonce, new byte[VaultHeader.TagSizeBytes]);
        var aad = newHeader.GetAssociatedData();

        byte[]? jsonBytes = null;
        try
        {
            jsonBytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
            var encResult = _encryptionService.Encrypt(jsonBytes, keyBuffer.Span, aad, nonce);
            var finalHeader = newHeader with { Tag = encResult.Tag };

            await _storage.SaveAsync(filePath, finalHeader, encResult.Ciphertext, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (jsonBytes != null)
            {
                CryptographicOperations.ZeroMemory(jsonBytes);
            }
        }
    }

    public Task DeleteSecretAsync(
        string filePath,
        string passphrase,
        string key,
        string? environment = null,
        CancellationToken cancellationToken = default) =>
        DeleteSecretAsync(filePath, (passphrase ?? string.Empty).AsMemory(), key, environment, cancellationToken);

    public async Task<IReadOnlyList<string>> PushEnvAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        string dotEnvContent,
        string? environment = null,
        CancellationToken cancellationToken = default)
    {
        var parseResult = DotEnvParser.Parse(dotEnvContent);
        if (parseResult.Entries.Count == 0)
        {
            return parseResult.DuplicateKeyWarnings;
        }

        var (header, ciphertext) = await _storage.LoadAsync(filePath, cancellationToken).ConfigureAwait(false);
        using var keyBuffer = _keyDerivation.DeriveKey(passphrase.Span, header.Salt, header.Argon2Params);

        VaultDocument document;
        using (var decryptedBuffer = _encryptionService.Decrypt(
            ciphertext,
            keyBuffer.Span,
            header.Nonce,
            header.Tag,
            header.GetAssociatedData()))
        {
            document = JsonSerializer.Deserialize<VaultDocument>(decryptedBuffer.Span, JsonOptions)
                ?? throw new VaultCorruptedException("Decrypted vault payload could not be parsed.");
        }

        foreach (var (key, value) in parseResult.Entries)
        {
            document.SetSecret(key, new SecretEntry(value), environment);
        }

        var nonce = _encryptionService.GenerateNonce();
        var newHeader = new VaultHeader(header.Salt, header.Argon2Params, nonce, new byte[VaultHeader.TagSizeBytes]);
        var aad = newHeader.GetAssociatedData();

        byte[]? jsonBytes = null;
        try
        {
            jsonBytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
            var encResult = _encryptionService.Encrypt(jsonBytes, keyBuffer.Span, aad, nonce);
            var finalHeader = newHeader with { Tag = encResult.Tag };

            await _storage.SaveAsync(filePath, finalHeader, encResult.Ciphertext, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (jsonBytes != null)
            {
                CryptographicOperations.ZeroMemory(jsonBytes);
            }
        }

        return parseResult.DuplicateKeyWarnings;
    }

    public async Task<IReadOnlyList<string>> PushEnvFileAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        string envFilePath,
        string? environment = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(envFilePath))
        {
            throw new FileNotFoundException($"The .env file was not found at '{envFilePath}'.", envFilePath);
        }

        var content = await File.ReadAllTextAsync(envFilePath, cancellationToken).ConfigureAwait(false);
        return await PushEnvAsync(filePath, passphrase, content, environment, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> PullEnvAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        string? environment = null,
        CancellationToken cancellationToken = default)
    {
        var secrets = await ListSecretsAsync(filePath, passphrase, environment, cancellationToken).ConfigureAwait(false);
        return DotEnvParser.Serialize(secrets);
    }

    public async Task PullEnvFileAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        string envFilePath,
        string? environment = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        if (File.Exists(envFilePath) && !overwrite)
        {
            throw new VaultException($"Destination file '{envFilePath}' already exists. Use overwrite to replace it.");
        }

        var content = await PullEnvAsync(filePath, passphrase, environment, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(envFilePath, content, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> RunWithSecretsAsync(
        string filePath,
        ReadOnlyMemory<char> passphrase,
        string command,
        IReadOnlyList<string> arguments,
        string? environment = null,
        string? workingDirectory = null,
        TextReader? standardInput = null,
        TextWriter? standardOutput = null,
        TextWriter? standardError = null,
        CancellationToken cancellationToken = default)
    {
        var secrets = await ListSecretsAsync(filePath, passphrase, environment, cancellationToken).ConfigureAwait(false);
        var envVars = secrets.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);

        return await _processRunner.RunAsync(
            command,
            arguments,
            envVars,
            workingDirectory,
            standardInput,
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }
}
