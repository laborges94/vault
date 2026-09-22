using System.CommandLine;
using System.CommandLine.Invocation;
using Vault.Core.Exceptions;

namespace Vault.Cli.Commands;

/// <summary>
/// Handles 'vault open <envelope> [--key <key>] [--raw]'.
/// </summary>
public static class OpenCommand
{
    public static Command Create(VaultCliApp app)
    {
        var envelopeArg = new Argument<string>("envelope", "Encrypted sharing envelope token or URL (optionally with #<key>)");
        var keyOption = new Option<string?>(new[] { "--key", "-k" }, "Decryption key if not included in envelope token fragment");
        var rawOption = new Option<bool>(new[] { "--raw", "-r" }, "Output raw secret value without trailing newline");

        var command = new Command("open", "Decrypts and displays an ephemeral sharing envelope")
        {
            envelopeArg,
            keyOption,
            rawOption
        };

        command.SetHandler((InvocationContext context) =>
        {
            var envelope = context.ParseResult.GetValueForArgument(envelopeArg);
            var key = context.ParseResult.GetValueForOption(keyOption);
            var isRaw = context.ParseResult.GetValueForOption(rawOption);

            if (string.IsNullOrWhiteSpace(key) && !envelope.Contains('#'))
            {
                if (Console.IsInputRedirected)
                {
                    app.Formatter.WriteError("Decryption key must be provided via --key or envelope fragment (#key) in non-interactive mode.");
                    context.ExitCode = 1;
                    return;
                }

                Console.Error.Write("Enter envelope decryption key: ");
                key = Console.ReadLine()?.Trim();
            }

            try
            {
                var secret = app.ShareService.OpenShareString(envelope, key);

                if (isRaw)
                {
                    app.Formatter.WriteRaw(secret);
                }
                else
                {
                    app.Formatter.WriteSuccess(secret);
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
