using System.CommandLine;
using System.CommandLine.Invocation;
using Vault.Core.Exceptions;

namespace Vault.Cli.Commands;

/// <summary>
/// Handles 'vault init [path]'.
/// </summary>
public static class InitCommand
{
    public static Command Create(VaultCliApp app)
    {
        var pathArg = new Argument<string?>("path", () => null, "Target file path for the vault");
        var forceOption = new Option<bool>(new[] { "--force", "-F" }, "Overwrite existing vault file if it already exists");

        var command = new Command("init", "Initializes a new encrypted vault container")
        {
            pathArg,
            forceOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArg)
                ?? context.ParseResult.GetValueForOption(app.VaultFileOption)
                ?? ".vault.enc";
            var force = context.ParseResult.GetValueForOption(forceOption);
            var passphraseOpts = app.GetPassphraseOptions(context.ParseResult);

            try
            {
                using var passphrase = app.PassphraseProvider.GetNewPassphrase(passphraseOpts);
                await app.VaultService.InitAsync(path, passphrase.ReadOnlySpan.ToArray().AsMemory(), overwrite: force);
                app.Formatter.WriteSuccess($"Initialized empty encrypted vault at '{path}'.");
                context.ExitCode = 0;
            }
            catch (VaultException ex)
            {
                app.Formatter.WriteError(ex.Message);
                context.ExitCode = 1;
            }
            catch (Exception ex)
            {
                app.Formatter.WriteError(ex.Message);
                context.ExitCode = 1;
            }
        });

        return command;
    }
}
