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

    public SecretEntry(
        string value,
        string? description = null,
        IReadOnlyList<string>? tags = null,
        DateTimeOffset? updatedAt = null,
        DateTimeOffset? expiresAt = null)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Description = description;
        Tags = tags ?? Array.Empty<string>();
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
        ExpiresAt = expiresAt;
    }
}
