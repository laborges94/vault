using Vault.Core.Model;

namespace Vault.Core.Tests.Model;

public class SecretEntryTests
{
    [Fact]
    public void Constructor_WithValidValue_InitializesProperties()
    {
        // Act
        var entry = new SecretEntry("secret_value_123");

        // Assert
        Assert.Equal("secret_value_123", entry.Value);
        Assert.Null(entry.Description);
        Assert.Empty(entry.Tags);
        Assert.True(entry.UpdatedAt <= DateTimeOffset.UtcNow);
        Assert.Null(entry.ExpiresAt);
    }

    [Fact]
    public void Constructor_WithNullValue_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SecretEntry(null!));
    }

    [Fact]
    public void Constructor_WithFullMetadata_AssignsAllProperties()
    {
        // Arrange
        var tags = new[] { "prod", "database" };
        var updated = DateTimeOffset.UtcNow.AddMinutes(-5);
        var expires = DateTimeOffset.UtcNow.AddDays(30);

        // Act
        var entry = new SecretEntry("db_password", "Main PostgreSQL password", tags, updated, expires);

        // Assert
        Assert.Equal("db_password", entry.Value);
        Assert.Equal("Main PostgreSQL password", entry.Description);
        Assert.Equal(tags, entry.Tags);
        Assert.Equal(updated, entry.UpdatedAt);
        Assert.Equal(expires, entry.ExpiresAt);
    }

    [Fact]
    public void Record_SupportsNonDestructiveMutationWithExpression()
    {
        // Arrange
        var entry = new SecretEntry("initial_value", "desc", new[] { "tag1" });

        // Act
        var modified = entry with { Value = "updated_value" };

        // Assert
        Assert.Equal("initial_value", entry.Value);
        Assert.Equal("updated_value", modified.Value);
        Assert.Equal(entry.Description, modified.Description);
        Assert.Equal(entry.Tags, modified.Tags);
    }
}
