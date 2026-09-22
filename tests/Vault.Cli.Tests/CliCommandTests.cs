using Vault.Cli;
using Vault.Cli.Output;

namespace Vault.Cli.Tests;

public sealed class CliCommandTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _vaultPath;
    private readonly string _passFile;
    private readonly StringWriter _stdout;
    private readonly StringWriter _stderr;
    private readonly ConsoleFormatter _formatter;
    private readonly VaultCliApp _app;

    public CliCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "vault_cli_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _vaultPath = Path.Combine(_testDir, "test.vault.enc");
        _passFile = Path.Combine(_testDir, "pass.txt");
        File.WriteAllText(_passFile, "master-test-passphrase-1234");

        _stdout = new StringWriter();
        _stderr = new StringWriter();
        _formatter = new ConsoleFormatter(_stdout, _stderr);
        _app = new VaultCliApp(formatter: _formatter);
    }

    public void Dispose()
    {
        _stdout.Dispose();
        _stderr.Dispose();

        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    private string[] WithVault(params string[] args)
    {
        var list = new List<string> { "-f", _vaultPath, "--passphrase-file", _passFile };
        list.AddRange(args);
        return list.ToArray();
    }

    [Fact]
    public async Task InitCommand_CreatesValidVaultFile()
    {
        var exitCode = await _app.RunAsync(WithVault("init"));

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(_vaultPath));
        Assert.Contains("Initialized empty encrypted vault", _stdout.ToString());
    }

    [Fact]
    public async Task SetAndGetCommand_StoresAndRetrievesSecret()
    {
        // Init
        await _app.RunAsync(WithVault("init"));

        // Set
        var setCode = await _app.RunAsync(WithVault("set", "DATABASE_URL", "postgres://localhost:5432/db", "--description", "DB Connection"));
        Assert.Equal(0, setCode);

        // Get standard
        _stdout.GetStringBuilder().Clear();
        var getCode = await _app.RunAsync(WithVault("get", "DATABASE_URL"));
        Assert.Equal(0, getCode);
        Assert.Contains("postgres://localhost:5432/db", _stdout.ToString());

        // Get raw
        _stdout.GetStringBuilder().Clear();
        var getRawCode = await _app.RunAsync(WithVault("get", "DATABASE_URL", "--raw"));
        Assert.Equal(0, getRawCode);
        Assert.Equal("postgres://localhost:5432/db", _stdout.ToString());
    }

    [Fact]
    public async Task SetCommand_InvalidKey_ReturnsErrorExitCode()
    {
        await _app.RunAsync(WithVault("init"));

        var setCode = await _app.RunAsync(WithVault("set", "INVALID KEY NAME", "some_value"));

        Assert.Equal(1, setCode);
        Assert.Contains("Error:", _stderr.ToString());
    }

    [Fact]
    public async Task ListCommand_RendersTableOfSecrets()
    {
        await _app.RunAsync(WithVault("init"));
        await _app.RunAsync(WithVault("set", "SECRET_A", "val_a", "--tags", "tag1,tag2"));
        await _app.RunAsync(WithVault("set", "SECRET_B", "val_b", "-e", "staging"));

        // List default env
        _stdout.GetStringBuilder().Clear();
        var listCode = await _app.RunAsync(WithVault("list"));
        Assert.Equal(0, listCode);
        var output = _stdout.ToString();
        Assert.Contains("SECRET_A", output);
        Assert.DoesNotContain("SECRET_B", output);

        // List all environments
        _stdout.GetStringBuilder().Clear();
        var listAllCode = await _app.RunAsync(WithVault("list", "--all-envs"));
        Assert.Equal(0, listAllCode);
        var allOutput = _stdout.ToString();
        Assert.Contains("SECRET_A", allOutput);
        Assert.Contains("SECRET_B", allOutput);
        Assert.Contains("staging", allOutput);
    }

    [Fact]
    public async Task DeleteCommand_WithForceFlag_RemovesSecret()
    {
        await _app.RunAsync(WithVault("init"));
        await _app.RunAsync(WithVault("set", "API_TOKEN", "token-xyz"));

        // Delete with --force
        var delCode = await _app.RunAsync(WithVault("delete", "API_TOKEN", "--force"));
        Assert.Equal(0, delCode);

        // Verify get fails
        var getCode = await _app.RunAsync(WithVault("get", "API_TOKEN"));
        Assert.Equal(1, getCode);
    }

    [Fact]
    public async Task EnvPushAndPullCommand_SynchronizesDotEnvFile()
    {
        await _app.RunAsync(WithVault("init"));

        var envFile = Path.Combine(_testDir, "test.env");
        await File.WriteAllLinesAsync(envFile, new[]
        {
            "# Test configuration",
            "PORT=8080",
            "HOST=127.0.0.1",
            "DUPLICATE=first",
            "DUPLICATE=second"
        });

        // Push
        var pushCode = await _app.RunAsync(WithVault("env", "push", envFile));
        Assert.Equal(0, pushCode);
        Assert.Contains("Warning: Duplicate key 'DUPLICATE'", _stderr.ToString());

        // Verify secret imported with last-write-wins
        _stdout.GetStringBuilder().Clear();
        await _app.RunAsync(WithVault("get", "DUPLICATE", "--raw"));
        Assert.Equal("second", _stdout.ToString());

        // Pull to export file
        var exportFile = Path.Combine(_testDir, "exported.env");
        var pullCode = await _app.RunAsync(WithVault("env", "pull", exportFile, "--force"));
        Assert.Equal(0, pullCode);
        Assert.True(File.Exists(exportFile));

        var pulledContent = await File.ReadAllTextAsync(exportFile);
        Assert.Contains("PORT=8080", pulledContent);
        Assert.Contains("HOST=127.0.0.1", pulledContent);
        Assert.Contains("DUPLICATE=second", pulledContent);
    }

    [Fact]
    public async Task ShareAndOpenCommand_SharesAndDecryptsSecret()
    {
        _stdout.GetStringBuilder().Clear();
        var shareCode = await _app.RunAsync(new[]
        {
            "share",
            "--value", "ephemeral-secret-999",
            "--ttl", "30m",
            "--raw"
        });

        Assert.Equal(0, shareCode);
        var combinedToken = _stdout.ToString().Trim();
        Assert.Contains('#', combinedToken);

        // Open with combined token
        _stdout.GetStringBuilder().Clear();
        var openCode = await _app.RunAsync(new[]
        {
            "open",
            combinedToken,
            "--raw"
        });

        Assert.Equal(0, openCode);
        Assert.Equal("ephemeral-secret-999", _stdout.ToString());
    }

    [Fact]
    public async Task RunCommand_ExecutesChildProcessWithSecrets()
    {
        await _app.RunAsync(WithVault("init"));
        await _app.RunAsync(WithVault("set", "GREETING", "HelloFromVault"));

        // Run pwsh or cmd /c echo
        var isWindows = OperatingSystem.IsWindows();
        var shell = isWindows ? "cmd.exe" : "sh";
        var shellArg = isWindows ? "/c" : "-c";
        var envCommand = isWindows ? "echo %GREETING%" : "echo $GREETING";

        var runCode = await _app.RunAsync(WithVault("run", "--", shell, shellArg, envCommand));
        Assert.Equal(0, runCode);
    }
}
