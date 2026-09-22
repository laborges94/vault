using Vault.Cli;

namespace Vault.Cli.Tests;

public class VaultCliAppTests
{
    [Fact]
    public async Task RunAsync_WithHelpFlag_ReturnsSuccessExitCode()
    {
        var app = new VaultCliApp();

        var exitCode = await app.RunAsync(new[] { "--help" });

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void App_ConfiguresGlobalOptions()
    {
        var app = new VaultCliApp();

        Assert.NotNull(app.VaultFileOption);
        Assert.NotNull(app.EnvOption);
        Assert.NotNull(app.PassphraseStdinOption);
        Assert.NotNull(app.PassphraseFileOption);

        Assert.Contains(app.VaultFileOption, app.RootCommand.Options);
        Assert.Contains(app.EnvOption, app.RootCommand.Options);
        Assert.Contains(app.PassphraseStdinOption, app.RootCommand.Options);
        Assert.Contains(app.PassphraseFileOption, app.RootCommand.Options);
    }
}
