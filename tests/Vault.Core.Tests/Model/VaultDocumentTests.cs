using Vault.Core.Model;

namespace Vault.Core.Tests.Model;

public class VaultDocumentTests
{
    [Fact]
    public void Constructor_InitializesDefaultEnvironmentAndMetadata()
    {
        // Act
        var doc = new VaultDocument();

        // Assert
        Assert.Equal(VaultDocument.CurrentSchemaVersion, doc.SchemaVersion);
        Assert.True(doc.CreatedAt <= DateTimeOffset.UtcNow);
        Assert.True(doc.ModifiedAt <= DateTimeOffset.UtcNow);
        Assert.True(doc.Environments.ContainsKey(VaultDocument.DefaultEnvironment));
        Assert.Empty(doc.Environments[VaultDocument.DefaultEnvironment]);
    }

    [Fact]
    public void SetSecret_InDefaultEnvironment_AddsSecretAndUpdatesModifiedAt()
    {
        // Arrange
        var doc = new VaultDocument();
        var initialModified = doc.ModifiedAt;
        var entry = new SecretEntry("p@ssword");

        // Act
        doc.SetSecret("DB_PASS", entry);

        // Assert
        Assert.True(doc.TryGetSecret("DB_PASS", null, out var retrieved));
        Assert.NotNull(retrieved);
        Assert.Equal("p@ssword", retrieved!.Value);
        Assert.True(doc.ModifiedAt >= initialModified);
    }

    [Fact]
    public void SetSecret_InCustomEnvironment_IsolatesSecretsAcrossEnvironments()
    {
        // Arrange
        var doc = new VaultDocument();
        var stagingEntry = new SecretEntry("staging_secret");
        var prodEntry = new SecretEntry("prod_secret");

        // Act
        doc.SetSecret("API_KEY", stagingEntry, "staging");
        doc.SetSecret("API_KEY", prodEntry, "production");

        // Assert
        Assert.True(doc.TryGetSecret("API_KEY", "staging", out var retrievedStaging));
        Assert.Equal("staging_secret", retrievedStaging!.Value);

        Assert.True(doc.TryGetSecret("API_KEY", "production", out var retrievedProd));
        Assert.Equal("prod_secret", retrievedProd!.Value);

        Assert.False(doc.TryGetSecret("API_KEY", "default", out _));
    }

    [Fact]
    public void RemoveSecret_ExistingSecret_RemovesAndReturnsTrue()
    {
        // Arrange
        var doc = new VaultDocument();
        doc.SetSecret("TEMP_KEY", new SecretEntry("123"), "staging");

        // Act
        var removed = doc.RemoveSecret("TEMP_KEY", "staging");

        // Assert
        Assert.True(removed);
        Assert.False(doc.TryGetSecret("TEMP_KEY", "staging", out _));
    }

    [Fact]
    public void RemoveSecret_NonExistentSecret_ReturnsFalse()
    {
        // Arrange
        var doc = new VaultDocument();

        // Act
        var removed = doc.RemoveSecret("UNKNOWN_KEY", "default");

        // Assert
        Assert.False(removed);
    }
}
