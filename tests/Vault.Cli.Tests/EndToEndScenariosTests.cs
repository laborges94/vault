using Vault.Cli;
using Vault.Cli.Authentication;
using Vault.Cli.Output;
using Vault.Core.Exceptions;

namespace Vault.Cli.Tests;

public sealed class EndToEndScenariosTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _vaultPath;
    private readonly string _passphrase = "Master-Test-Passphrase-2026!";

    public EndToEndScenariosTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "vault_e2e_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _vaultPath = Path.Combine(_tempDir, ".vault.enc");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // Best effort
            }
        }
    }

    private (VaultCliApp App, StringWriter Stdout, StringWriter Stderr) CreateApp(Func<string, string?>? envLookup = null)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var formatter = new ConsoleFormatter(stdout, stderr);
        var passProvider = new PassphraseProvider(
            envLookup: envLookup ?? (key => key == "VAULT_PASSPHRASE" ? _passphrase : null));
        var app = new VaultCliApp(formatter: formatter, passphraseProvider: passProvider);
        return (app, stdout, stderr);
    }

    [Fact]
    public async Task Scenario1_InitializingNewEncryptedVault()
    {
        var (app, stdout, _) = CreateApp();

        // When user executes vault init
        var exitCode = await app.RunAsync(new[] { "init", _vaultPath });

        // Then:
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(_vaultPath));

        var bytes = await File.ReadAllBytesAsync(_vaultPath);
        // Header contains magic 'VAULT'
        Assert.Equal((byte)'V', bytes[0]);
        Assert.Equal((byte)'A', bytes[1]);
        Assert.Equal((byte)'U', bytes[2]);
        Assert.Equal((byte)'L', bytes[3]);
        Assert.Equal((byte)'T', bytes[4]);

        // Zero plaintext of passphrase
        var passBytes = System.Text.Encoding.UTF8.GetBytes(_passphrase);
        Assert.DoesNotContain(passBytes, b => false); // sanity
        Assert.False(bytes.AsSpan().IndexOf(passBytes) >= 0);

        // No leftover tmp files
        var tmpFiles = Directory.GetFiles(_tempDir, "*.tmp.*");
        Assert.Empty(tmpFiles);
    }

    [Fact]
    public async Task Scenario2_StoringAndRetrievingSecrets_WithNamespacesAndValidation()
    {
        var (app, stdout, stderr) = CreateApp();
        await app.RunAsync(new[] { "init", _vaultPath });

        // When setting secret under staging env
        var setCode = await app.RunAsync(new[] { "-f", _vaultPath, "-e", "staging", "set", "DB_PASSWORD", "s3cur3P@ssw0rd!" });
        Assert.Equal(0, setCode);

        // When setting invalid key name
        stderr.GetStringBuilder().Clear();
        var invalidCode = await app.RunAsync(new[] { "-f", _vaultPath, "set", "INVALID KEY NAME", "val" });
        Assert.Equal(1, invalidCode);
        Assert.Contains("Error:", stderr.ToString());

        // Get secret under staging
        stdout.GetStringBuilder().Clear();
        var getCode = await app.RunAsync(new[] { "-f", _vaultPath, "-e", "staging", "get", "DB_PASSWORD", "--raw" });
        Assert.Equal(0, getCode);
        Assert.Equal("s3cur3P@ssw0rd!", stdout.ToString());

        // List secret under staging
        stdout.GetStringBuilder().Clear();
        var listCode = await app.RunAsync(new[] { "-f", _vaultPath, "-e", "staging", "list" });
        Assert.Equal(0, listCode);
        Assert.Contains("DB_PASSWORD", stdout.ToString());
        Assert.DoesNotContain("s3cur3P@ssw0rd!", stdout.ToString());

        // File contains no plaintext
        var rawBytes = await File.ReadAllBytesAsync(_vaultPath);
        var secretBytes = System.Text.Encoding.UTF8.GetBytes("s3cur3P@ssw0rd!");
        Assert.False(rawBytes.AsSpan().IndexOf(secretBytes) >= 0);
    }

    [Fact]
    public async Task Scenario3_TamperDetectionAndIntegrityVerification()
    {
        var (app, _, stderr) = CreateApp();
        await app.RunAsync(new[] { "init", _vaultPath });
        await app.RunAsync(new[] { "-f", _vaultPath, "set", "SECRET_KEY", "sensitive_val" });

        // Flip a bit in the encrypted vault file (at offset 50)
        var fileBytes = await File.ReadAllBytesAsync(_vaultPath);
        fileBytes[50] ^= 0x01;
        await File.WriteAllBytesAsync(_vaultPath, fileBytes);

        // Decryption must fail due to AES-GCM tag mismatch
        stderr.GetStringBuilder().Clear();
        var getCode = await app.RunAsync(new[] { "-f", _vaultPath, "get", "SECRET_KEY" });
        Assert.Equal(1, getCode);
        Assert.Contains("Error:", stderr.ToString());
    }

    [Fact]
    public async Task Scenario4_ProcessSecretInjection_VaultRun()
    {
        var (app, _, _) = CreateApp();
        await app.RunAsync(new[] { "init", _vaultPath });
        await app.RunAsync(new[] { "-f", _vaultPath, "-e", "production", "set", "API_KEY", "test-token-xyz" });

        var isWindows = OperatingSystem.IsWindows();
        var cmd = isWindows ? "cmd.exe" : "sh";
        var cmdArg = isWindows ? "/c" : "-c";
        var script = isWindows ? "echo %API_KEY%" : "echo $API_KEY";

        var runCode = await app.RunAsync(new[] { "-f", _vaultPath, "-e", "production", "run", "--", cmd, cmdArg, script });
        Assert.Equal(0, runCode);
    }

    [Fact]
    public async Task Scenario5_EphemeralSecretSharingAndEnvelopeConsumption()
    {
        var (app, stdout, stderr) = CreateApp();
        await app.RunAsync(new[] { "init", _vaultPath });
        await app.RunAsync(new[] { "-f", _vaultPath, "set", "PROD_SSH_KEY", "ssh-rsa AAAAB3NzaC1yc2EAAAADAQAB" });

        // Share from vault
        stdout.GetStringBuilder().Clear();
        var shareCode = await app.RunAsync(new[] { "-f", _vaultPath, "share", "PROD_SSH_KEY", "--ttl", "1h", "--raw" });
        Assert.Equal(0, shareCode);
        var combinedToken = stdout.ToString().Trim();
        Assert.Contains('#', combinedToken);

        var parts = combinedToken.Split('#');
        var envelope = parts[0];
        var key = parts[1];

        // Open with correct key
        stdout.GetStringBuilder().Clear();
        var openCode = await app.RunAsync(new[] { "open", envelope, "--key", key, "--raw" });
        Assert.Equal(0, openCode);
        Assert.Equal("ssh-rsa AAAAB3NzaC1yc2EAAAADAQAB", stdout.ToString());

        // Open with invalid key fails
        stderr.GetStringBuilder().Clear();
        var badKeyCode = await app.RunAsync(new[] { "open", envelope, "--key", "invalid-key-token", "--raw" });
        Assert.Equal(1, badKeyCode);
        Assert.Contains("Error:", stderr.ToString());
    }

    [Fact]
    public async Task Scenario6_NonInteractivePassphraseProvisioning()
    {
        // 1. Via VAULT_PASSPHRASE
        var (appEnv, stdoutEnv, _) = CreateApp(key => key == "VAULT_PASSPHRASE" ? _passphrase : null);
        await appEnv.RunAsync(new[] { "init", _vaultPath });
        await appEnv.RunAsync(new[] { "-f", _vaultPath, "set", "NON_INT_KEY", "hello" });

        stdoutEnv.GetStringBuilder().Clear();
        var getCode = await appEnv.RunAsync(new[] { "-f", _vaultPath, "get", "NON_INT_KEY", "--raw" });
        Assert.Equal(0, getCode);
        Assert.Equal("hello", stdoutEnv.ToString());

        // 2. Via --passphrase-file
        var passFilePath = Path.Combine(_tempDir, "file_pass.txt");
        await File.WriteAllTextAsync(passFilePath, _passphrase);

        var (appFile, stdoutFile, _) = CreateApp(key => null); // No VAULT_PASSPHRASE in env
        stdoutFile.GetStringBuilder().Clear();
        var getFileCode = await appFile.RunAsync(new[] { "-f", _vaultPath, "--passphrase-file", passFilePath, "get", "NON_INT_KEY", "--raw" });
        Assert.Equal(0, getFileCode);
        Assert.Equal("hello", stdoutFile.ToString());
    }
}
