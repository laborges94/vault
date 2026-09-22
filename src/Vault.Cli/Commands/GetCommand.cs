using System.CommandLine;
using System.CommandLine.Invocation;
using Vault.Core.Exceptions;

namespace Vault.Cli.Commands;

/// <summary>
/// Handles 'vault get <key> [--raw]'.
/// </summary>
public static class GetCommand
{
    public static Command Create(VaultCliApp app)
    {
        var keyArg = new Argument<string>("key", "Name of the secret key to retrieve");
        var rawOption = new Option<bool>(new[] { "--raw", "-r" }, "Output raw secret value without trailing newline");

        var command = new Command("get", "Retrieves and displays a decrypted secret value")
        {
            keyArg,
            rawOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var key = context.ParseResult.GetValueForArgument(keyArg);
            var isRaw = context.ParseResult.GetValueForOption(rawOption);

            var vaultPath = context.ParseResult.GetValueForOption(app.VaultFileOption) ?? ".vault.enc";
            var environment = context.ParseResult.GetValueForOption(app.EnvOption);
            var passphraseOpts = app.GetPassphraseOptions(context.ParseResult);

            try
            {
                using var passphrase = app.PassphraseProvider.GetPassphrase(passphraseOpts);
                var entry = await app.VaultService.GetSecretAsync(
                    vaultPath,
                    passphrase.ReadOnlySpan.ToArray().AsMemory(),
                    key,
                    environment: environment);

                if (isRaw)
                {
                    app.Formatter.WriteRaw(entry.Value);
                }
                else
                {
                    app.Formatter.WriteSuccess(entry.Value);
                }

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
