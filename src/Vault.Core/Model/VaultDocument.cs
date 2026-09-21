namespace Vault.Core.Model;

/// <summary>
/// Root in-memory aggregate representing stored environments and their secrets.
/// </summary>
public sealed class VaultDocument
{
    public const string DefaultEnvironment = "default";
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, Dictionary<string, SecretEntry>> Environments { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public VaultDocument()
    {
        Environments[DefaultEnvironment] = new Dictionary<string, SecretEntry>(StringComparer.Ordinal);
    }

    public Dictionary<string, SecretEntry> GetOrCreateEnvironment(string? environmentName = null)
    {
        var env = string.IsNullOrWhiteSpace(environmentName) ? DefaultEnvironment : environmentName.Trim();
        if (!Environments.TryGetValue(env, out var secrets))
        {
            secrets = new Dictionary<string, SecretEntry>(StringComparer.Ordinal);
            Environments[env] = secrets;
        }

        return secrets;
    }

    public bool TryGetSecret(string key, string? environment, out SecretEntry? entry)
    {
        var env = string.IsNullOrWhiteSpace(environment) ? DefaultEnvironment : environment.Trim();
        if (Environments.TryGetValue(env, out var secrets) && secrets.TryGetValue(key, out entry))
        {
            return true;
        }

        entry = null;
        return false;
    }

    public void SetSecret(string key, SecretEntry entry, string? environment = null)
    {
        var secrets = GetOrCreateEnvironment(environment);
        secrets[key] = entry;
        ModifiedAt = DateTimeOffset.UtcNow;
    }

    public bool RemoveSecret(string key, string? environment = null)
    {
        var env = string.IsNullOrWhiteSpace(environment) ? DefaultEnvironment : environment.Trim();
        if (Environments.TryGetValue(env, out var secrets) && secrets.Remove(key))
        {
            ModifiedAt = DateTimeOffset.UtcNow;
            return true;
        }

        return false;
    }
}
