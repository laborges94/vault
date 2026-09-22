using Vault.Cli.Authentication;
using Vault.Core.Exceptions;

namespace Vault.Cli.Tests.Authentication;

public class PassphraseProviderTests : IDisposable
{
    private readonly string _tempDirectory;

    public PassphraseProviderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PassphraseTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
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
    public void GetPassphrase_WithStdinOption_ReadsFromReader()
    {
        var input = new StringReader("P@sswordFromStdin\n");
        var provider = new PassphraseProvider(inputReader: input);

        var options = new PassphraseOptions { PassphraseStdin = true };
        using var pass = provider.GetPassphrase(options);

        Assert.Equal("P@sswordFromStdin", new string(pass.Span));
    }

    [Fact]
    public void GetPassphrase_WithFileOption_ReadsFromFile()
    {
        var filePath = Path.Combine(_tempDirectory, "pass.txt");
        File.WriteAllText(filePath, "PassphraseFromFile123\r\n");

        var provider = new PassphraseProvider();
        var options = new PassphraseOptions { PassphraseFile = filePath };
        using var pass = provider.GetPassphrase(options);

        Assert.Equal("PassphraseFromFile123", new string(pass.Span));
    }

    [Fact]
    public void GetPassphrase_WithEnvironmentVariable_ResolvesFromEnv()
    {
        var envLookup = new Func<string, string?>(key => key == "VAULT_PASSPHRASE" ? "EnvPassword!@#" : null);
        var provider = new PassphraseProvider(envLookup: envLookup);

        using var pass = provider.GetPassphrase();

        Assert.Equal("EnvPassword!@#", new string(pass.Span));
    }

    [Fact]
    public void GetPassphrase_Precedence_StdinOverridesFileAndEnv()
    {
        var filePath = Path.Combine(_tempDirectory, "unused_pass.txt");
        File.WriteAllText(filePath, "FilePass");

        var input = new StringReader("StdinPass\n");
        var envLookup = new Func<string, string?>(_ => "EnvPass");

        var provider = new PassphraseProvider(inputReader: input, envLookup: envLookup);
        var options = new PassphraseOptions
        {
            PassphraseStdin = true,
            PassphraseFile = filePath
        };

        using var pass = provider.GetPassphrase(options);

        Assert.Equal("StdinPass", new string(pass.Span));
    }

    [Fact]
    public void GetNewPassphrase_MatchingConfirmation_ReturnsPassphrase()
    {
        var input = new StringReader("StrongPassword123!\nStrongPassword123!\n");
        var provider = new PassphraseProvider(inputReader: input);

        using var pass = provider.GetNewPassphrase();

        Assert.Equal("StrongPassword123!", new string(pass.Span));
    }

    [Fact]
    public void GetNewPassphrase_MismatchedConfirmation_ThrowsVaultValidationException()
    {
        var input = new StringReader("StrongPassword123!\nMismatchedPassword!\n");
        var provider = new PassphraseProvider(inputReader: input);

        var ex = Assert.Throws<VaultValidationException>(() => provider.GetNewPassphrase());
        Assert.Contains("do not match", ex.Message);
    }

    [Fact]
    public void GetNewPassphrase_TooShort_ThrowsVaultValidationException()
    {
        var input = new StringReader("short\n");
        var provider = new PassphraseProvider(inputReader: input);

        var ex = Assert.Throws<VaultValidationException>(() => provider.GetNewPassphrase());
        Assert.Contains("at least 8 characters", ex.Message);
    }
}
