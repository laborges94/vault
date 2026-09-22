using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Vault.Cli.Authentication;
using Vault.Cli.Output;
using Vault.Core.Cryptography;
using Vault.Core.Execution;
using Vault.Core.Services;
using Vault.Core.Sharing;
using Vault.Core.Storage;

using Vault.Cli.Commands;

namespace Vault.Cli;

/// <summary>
/// Root CLI application infrastructure configured with System.CommandLine.
/// </summary>
public sealed class VaultCliApp
{
    public RootCommand RootCommand { get; }
    public Option<string> VaultFileOption { get; }
    public Option<string?> EnvOption { get; }
    public Option<bool> PassphraseStdinOption { get; }
    public Option<string?> PassphraseFileOption { get; }

    public IVaultService VaultService { get; }
    public IEphemeralShareService ShareService { get; }
    public IPassphraseProvider PassphraseProvider { get; }
    public IConsoleFormatter Formatter { get; }

    public VaultCliApp(
        IVaultService? vaultService = null,
        IEphemeralShareService? shareService = null,
        IPassphraseProvider? passphraseProvider = null,
        IConsoleFormatter? formatter = null)
    {
        var storage = new FileVaultStorage();
        var keyDerivation = new Argon2KeyDerivationService();
        var encryption = new AesGcmEncryptionService();
        var processRunner = new ProcessRunner();

        VaultService = vaultService ?? new VaultService(storage, keyDerivation, encryption, processRunner);
        ShareService = shareService ?? new EphemeralShareService(encryption);
        PassphraseProvider = passphraseProvider ?? new PassphraseProvider();
        Formatter = formatter ?? new ConsoleFormatter();

        RootCommand = new RootCommand("Vault: Lightweight zero-knowledge developer secret vault");

        VaultFileOption = new Option<string>(
            aliases: new[] { "--vault-file", "-f" },
            description: "Path to the encrypted vault file",
            getDefaultValue: () => Environment.GetEnvironmentVariable("VAULT_FILE") ?? ".vault.enc");

        EnvOption = new Option<string?>(
            aliases: new[] { "--env", "-e" },
            description: "Environment namespace (defaults to 'default')");

        PassphraseStdinOption = new Option<bool>(
            aliases: new[] { "--passphrase-stdin" },
            description: "Read passphrase from standard input");

        PassphraseFileOption = new Option<string?>(
            aliases: new[] { "--passphrase-file" },
            description: "Read passphrase from specified file path");

        RootCommand.AddGlobalOption(VaultFileOption);
        RootCommand.AddGlobalOption(EnvOption);
        RootCommand.AddGlobalOption(PassphraseStdinOption);
        RootCommand.AddGlobalOption(PassphraseFileOption);

        RootCommand.AddCommand(InitCommand.Create(this));
        RootCommand.AddCommand(SetCommand.Create(this));
        RootCommand.AddCommand(GetCommand.Create(this));
        RootCommand.AddCommand(ListCommand.Create(this));
        RootCommand.AddCommand(DeleteCommand.Create(this));
        RootCommand.AddCommand(EnvCommand.Create(this));
        RootCommand.AddCommand(RunCommand.Create(this));
        RootCommand.AddCommand(ShareCommand.Create(this));
        RootCommand.AddCommand(OpenCommand.Create(this));

        _parser = new CommandLineBuilder(RootCommand)
            .UseDefaults()
            .Build();
    }

    private readonly Parser _parser;

    public PassphraseOptions GetPassphraseOptions(ParseResult parseResult)
    {
        return new PassphraseOptions
        {
            PassphraseStdin = parseResult.GetValueForOption(PassphraseStdinOption),
            PassphraseFile = parseResult.GetValueForOption(PassphraseFileOption)
        };
    }

    public Task<int> RunAsync(string[] args)
    {
        return _parser.InvokeAsync(args);
    }
}
