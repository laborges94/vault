using Vault.Core.Cryptography;
using Vault.Core.Env;
using Vault.Core.Exceptions;
using Vault.Core.Services;
using Vault.Core.Storage;

namespace Vault.Core.Tests.Env;

public class DotEnvParserTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly IVaultService _vaultService;
    private readonly Argon2Parameters _testParams = Argon2Parameters.ForTesting;

    public DotEnvParserTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DotEnvTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        var storage = new FileVaultStorage(
            lockTimeout: TimeSpan.FromMilliseconds(500),
            retryInterval: TimeSpan.FromMilliseconds(20));
        var keyDerivation = new Argon2KeyDerivationService();
        var encryption = new AesGcmEncryptionService();

        _vaultService = new VaultService(storage, keyDerivation, encryption);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void Parse_BasicKeyValuePairs_ReturnsEntries()
    {
        var content = """
            DB_HOST=localhost
            DB_PORT=5432
            APP_NAME=VaultApp
            """;

        var result = DotEnvParser.Parse(content);

        Assert.Equal(3, result.Entries.Count);
        Assert.Equal("localhost", result.Entries["DB_HOST"]);
        Assert.Equal("5432", result.Entries["DB_PORT"]);
        Assert.Equal("VaultApp", result.Entries["APP_NAME"]);
        Assert.Empty(result.DuplicateKeyWarnings);
    }

    [Fact]
    public void Parse_WithCommentsAndBlankLines_IgnoresCommentsAndBlankLines()
    {
        var content = """
            # Database settings
            DB_HOST=localhost

            # Application port
            PORT=8080

            """;

        var result = DotEnvParser.Parse(content);

        Assert.Equal(2, result.Entries.Count);
        Assert.Equal("localhost", result.Entries["DB_HOST"]);
        Assert.Equal("8080", result.Entries["PORT"]);
    }

    [Fact]
    public void Parse_WithExportPrefix_StripsExportPrefix()
    {
        var content = """
            export API_KEY=secret-token-123
            export PORT=3000
            """;

        var result = DotEnvParser.Parse(content);

        Assert.Equal(2, result.Entries.Count);
        Assert.Equal("secret-token-123", result.Entries["API_KEY"]);
        Assert.Equal("3000", result.Entries["PORT"]);
    }

    [Fact]
    public void Parse_DoubleQuotedValues_UnescapesSpecialCharacters()
    {
        var content = """
            MESSAGE="Hello \"world\"\nNew line\tTab"
            HASH_VAL="protect # hash"
            """;

        var result = DotEnvParser.Parse(content);

        Assert.Equal("Hello \"world\"\nNew line\tTab", result.Entries["MESSAGE"]);
        Assert.Equal("protect # hash", result.Entries["HASH_VAL"]);
    }

    [Fact]
    public void Parse_SingleQuotedValues_PreservesLiteralContent()
    {
        var content = """
            RAW_VALUE='Literal\nString#NotComment'
            """;

        var result = DotEnvParser.Parse(content);

        Assert.Equal(@"Literal\nString#NotComment", result.Entries["RAW_VALUE"]);
    }

    [Fact]
    public void Parse_InlineComments_StripsCommentsFromUnquotedValues()
    {
        var content = """
            DB_PORT=5432 # PostgreSQL default port
            URL=https://example.com/api # Main endpoint
            """;

        var result = DotEnvParser.Parse(content);

        Assert.Equal("5432", result.Entries["DB_PORT"]);
        Assert.Equal("https://example.com/api", result.Entries["URL"]);
    }

    [Fact]
    public void Parse_DuplicateKeys_EmitsWarningAndAppliesLastWriteWins()
    {
        var content = """
            TIMEOUT=30
            TIMEOUT=60
            """;

        var result = DotEnvParser.Parse(content);

        Assert.Single(result.Entries);
        Assert.Equal("60", result.Entries["TIMEOUT"]);
        Assert.Single(result.DuplicateKeyWarnings);
        Assert.Contains("Duplicate key 'TIMEOUT'", result.DuplicateKeyWarnings[0]);
    }

    [Fact]
    public void Parse_MalformedLine_ThrowsVaultValidationException()
    {
        var content = """
            VALID_KEY=valid_value
            THIS_LINE_HAS_NO_EQUALS_SIGN
            """;

        Assert.Throws<VaultValidationException>(() => DotEnvParser.Parse(content));
    }

    [Fact]
    public void Serialize_FormatsEntriesAlphabeticallyWithProperEscaping()
    {
        var entries = new Dictionary<string, string>
        {
            ["PORT"] = "8080",
            ["GREETING"] = "Hello\nWorld"
        };

        var output = DotEnvParser.Serialize(entries);

        Assert.Contains("GREETING=\"Hello\\nWorld\"", output);
        Assert.Contains("PORT=8080", output);
    }

    [Fact]
    public async Task PushEnvAsync_And_PullEnvAsync_RoundTripsCorrectly()
    {
        var vaultPath = Path.Combine(_tempDirectory, "dotenv.vault.enc");
        var pass = "passphrase";
        await _vaultService.InitAsync(vaultPath, pass, _testParams);

        var envContent = """
            DB_NAME=production_db
            DB_USER=admin
            PASSWORD="s3cur3#P@ss\n123"
            """;

        var warnings = await _vaultService.PushEnvAsync(vaultPath, pass, envContent, "production");
        Assert.Empty(warnings);

        var pulledContent = await _vaultService.PullEnvAsync(vaultPath, pass, "production");

        Assert.Contains("DB_NAME=production_db", pulledContent);
        Assert.Contains("DB_USER=admin", pulledContent);
        Assert.Contains("PASSWORD=\"s3cur3#P@ss\\n123\"", pulledContent);
    }

    [Fact]
    public async Task PushEnvFileAsync_And_PullEnvFileAsync_PerformsFileIoCorrectly()
    {
        var vaultPath = Path.Combine(_tempDirectory, "file_io.vault.enc");
        var sourceEnvPath = Path.Combine(_tempDirectory, ".env.source");
        var destEnvPath = Path.Combine(_tempDirectory, ".env.dest");
        var pass = "passphrase";

        await _vaultService.InitAsync(vaultPath, pass, _testParams);

        var originalEnv = "API_KEY=xyz123\nTIMEOUT=5000\n";
        await File.WriteAllTextAsync(sourceEnvPath, originalEnv);

        // Push from file
        await _vaultService.PushEnvFileAsync(vaultPath, pass, sourceEnvPath, "staging");

        // Pull to file
        await _vaultService.PullEnvFileAsync(vaultPath, pass, destEnvPath, "staging", overwrite: true);

        Assert.True(File.Exists(destEnvPath));
        var destContent = await File.ReadAllTextAsync(destEnvPath);
        Assert.Contains("API_KEY=xyz123", destContent);
        Assert.Contains("TIMEOUT=5000", destContent);
    }
}
