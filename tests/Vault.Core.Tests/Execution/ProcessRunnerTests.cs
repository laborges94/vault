using Vault.Core.Cryptography;
using Vault.Core.Execution;
using Vault.Core.Services;
using Vault.Core.Storage;

namespace Vault.Core.Tests.Execution;

public class ProcessRunnerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly ProcessRunner _runner;
    private readonly IVaultService _vaultService;
    private readonly Argon2Parameters _testParams = Argon2Parameters.ForTesting;

    public ProcessRunnerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ProcessRunnerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        _runner = new ProcessRunner();
        var storage = new FileVaultStorage(
            lockTimeout: TimeSpan.FromMilliseconds(500),
            retryInterval: TimeSpan.FromMilliseconds(20));
        var keyDerivation = new Argon2KeyDerivationService();
        var encryption = new AesGcmEncryptionService();

        _vaultService = new VaultService(storage, keyDerivation, encryption, _runner);
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
    public async Task RunAsync_InjectsEnvironmentVariablesIntoChildProcess()
    {
        var outputWriter = new StringWriter();
        var envVars = new Dictionary<string, string>
        {
            ["SECRET_TOKEN"] = "my-secret-token-xyz"
        };

        // On Windows cmd.exe /c echo %SECRET_TOKEN%
        var exitCode = await _runner.RunAsync(
            command: "cmd.exe",
            arguments: new[] { "/c", "echo %SECRET_TOKEN%" },
            environmentVariables: envVars,
            standardOutput: outputWriter);

        Assert.Equal(0, exitCode);
        Assert.Contains("my-secret-token-xyz", outputWriter.ToString());
    }

    [Fact]
    public async Task RunAsync_ForwardsExitCodeFromChildProcess()
    {
        var exitCode = await _runner.RunAsync(
            command: "cmd.exe",
            arguments: new[] { "/c", "exit 42" },
            environmentVariables: new Dictionary<string, string>());

        Assert.Equal(42, exitCode);
    }

    [Fact]
    public async Task RunAsync_Cancellation_KillsChildProcess()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // cmd.exe /c ping 127.0.0.1 -n 10 runs for ~10 seconds
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _runner.RunAsync(
                command: "cmd.exe",
                arguments: new[] { "/c", "ping 127.0.0.1 -n 10" },
                environmentVariables: new Dictionary<string, string>(),
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task RunWithSecretsAsync_InjectsVaultSecretsDirectlyIntoChildProcess()
    {
        var vaultPath = Path.Combine(_tempDirectory, "process.vault.enc");
        var pass = "passphrase";

        await _vaultService.InitAsync(vaultPath, pass, _testParams);
        await _vaultService.SetSecretAsync(vaultPath, pass, "API_KEY", "prod-api-key-999", environment: "production");

        var outputWriter = new StringWriter();

        var exitCode = await _vaultService.RunWithSecretsAsync(
            filePath: vaultPath,
            passphrase: pass,
            command: "cmd.exe",
            arguments: new[] { "/c", "echo %API_KEY%" },
            environment: "production",
            standardOutput: outputWriter);

        Assert.Equal(0, exitCode);
        Assert.Contains("prod-api-key-999", outputWriter.ToString());

        // Verify no temporary secret files or .env files were written to disk
        var filesInDir = Directory.GetFiles(_tempDirectory);
        Assert.Single(filesInDir);
        Assert.Equal(Path.GetFileName(vaultPath), Path.GetFileName(filesInDir[0]));
    }
}
