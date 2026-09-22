using System.CommandLine;
using System.CommandLine.Invocation;
using Vault.Core.Exceptions;

namespace Vault.Cli.Commands;

/// <summary>
/// Handles 'vault run [--env <name>] -- <command> [args...]'.
/// </summary>
public static class RunCommand
{
    public static Command Create(VaultCliApp app)
    {
        var commandArg = new Argument<string[]>(
            name: "command",
            description: "Command and arguments to execute with decrypted environment variables")
        {
            Arity = ArgumentArity.OneOrMore
        };

        var runCommand = new Command("run", "Runs a command with decrypted secrets injected into memory")
        {
            commandArg
        };

        runCommand.TreatUnmatchedTokensAsErrors = false;

        runCommand.SetHandler(async (InvocationContext context) =>
        {
            var tokens = context.ParseResult.GetValueForArgument(commandArg);
            if (tokens == null || tokens.Length == 0)
            {
                app.Formatter.WriteError("No command was specified to run.");
                context.ExitCode = 1;
                return;
            }

            var cmd = tokens[0];
            var args = tokens.Skip(1).ToArray();

            var vaultPath = context.ParseResult.GetValueForOption(app.VaultFileOption) ?? ".vault.enc";
            var environment = context.ParseResult.GetValueForOption(app.EnvOption);
            var passphraseOpts = app.GetPassphraseOptions(context.ParseResult);

            try
            {
                using var passphrase = app.PassphraseProvider.GetPassphrase(passphraseOpts);

                var exitCode = await app.VaultService.RunWithSecretsAsync(
                    vaultPath,
                    passphrase.ReadOnlySpan.ToArray().AsMemory(),
                    cmd,
                    args,
                    environment: environment,
                    cancellationToken: context.GetCancellationToken());

                context.ExitCode = exitCode;
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

        return runCommand;
    }
}
