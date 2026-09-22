using System.CommandLine;
using System.CommandLine.Invocation;
using Vault.Core.Exceptions;

namespace Vault.Cli.Commands;

/// <summary>
/// Handles 'vault delete <key> [--force]'.
/// </summary>
public static class DeleteCommand
{
    public static Command Create(VaultCliApp app)
    {
        var keyArg = new Argument<string>("key", "Name of the secret key to delete");
        var forceOption = new Option<bool>(new[] { "--force", "-F" }, "Delete without confirmation prompt");

        var command = new Command("delete", "Removes a secret from the vault")
        {
            keyArg,
            forceOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var key = context.ParseResult.GetValueForArgument(keyArg);
            var force = context.ParseResult.GetValueForOption(forceOption);

            var vaultPath = context.ParseResult.GetValueForOption(app.VaultFileOption) ?? ".vault.enc";
            var environment = context.ParseResult.GetValueForOption(app.EnvOption);
            var passphraseOpts = app.GetPassphraseOptions(context.ParseResult);

            if (!force)
            {
                if (Console.IsInputRedirected)
                {
                    app.Formatter.WriteError("Non-interactive mode detected. Use --force to confirm deletion.");
                    context.ExitCode = 1;
                    return;
                }

                Console.Error.Write($"Are you sure you want to delete secret '{key}'? (y/N): ");
                var response = Console.ReadLine();
                if (!string.Equals(response?.Trim(), "y", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(response?.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
                {
                    app.Formatter.WriteWarning("Deletion cancelled.");
                    context.ExitCode = 0;
                    return;
                }
            }

            try
            {
                using var passphrase = app.PassphraseProvider.GetPassphrase(passphraseOpts);
                await app.VaultService.DeleteSecretAsync(
                    vaultPath,
                    passphrase.ReadOnlySpan.ToArray().AsMemory(),
                    key,
                    environment: environment);

                app.Formatter.WriteSuccess($"Secret '{key}' deleted successfully.");
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
