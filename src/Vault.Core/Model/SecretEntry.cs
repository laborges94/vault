using System.Text.Json.Serialization;

namespace Vault.Core.Model;

/// <summary>
/// Immutable record representing a secret entry and its associated metadata.
/// </summary>
public sealed record SecretEntry
{
    public string Value { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }

    [JsonConstructor]
    public SecretEntry(
        string value,
        string? description = null,
        IReadOnlyList<string>? tags = null,
        DateTimeOffset updatedAt = default,
        DateTimeOffset? expiresAt = null)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Description = description;
        Tags = tags ?? Array.Empty<string>();
        UpdatedAt = updatedAt == default ? DateTimeOffset.UtcNow : updatedAt;
        ExpiresAt = expiresAt;
    }
}
