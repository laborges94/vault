using System.CommandLine;
using System.CommandLine.Invocation;
using Vault.Core.Exceptions;

namespace Vault.Cli.Commands;

/// <summary>
/// Handles 'vault share [key] [--value <val>] [--ttl <duration>]'.
/// </summary>
public static class ShareCommand
{
    public static Command Create(VaultCliApp app)
    {
        var keyArg = new Argument<string?>("key", () => null, "Secret key in the vault to share");
        var valueOption = new Option<string?>(new[] { "--value", "-v" }, "Direct secret value to encrypt into an envelope");
        var ttlOption = new Option<string?>(new[] { "--ttl", "-t" }, () => "15m", "Expiration time-to-live duration (e.g. '15m', '1h', '24h')");
        var rawOption = new Option<bool>(new[] { "--raw", "-r" }, "Output raw combined token (<envelope>#<key>)");

        var command = new Command("share", "Generates an ephemeral encrypted sharing envelope with client TTL")
        {
            keyArg,
            valueOption,
            ttlOption,
            rawOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var key = context.ParseResult.GetValueForArgument(keyArg);
            var directValue = context.ParseResult.GetValueForOption(valueOption);
            var rawTtl = context.ParseResult.GetValueForOption(ttlOption) ?? "15m";
            var isRaw = context.ParseResult.GetValueForOption(rawOption);

            TimeSpan ttl;
            try
            {
                ttl = TimeSpanParser.Parse(rawTtl);
            }
            catch (FormatException ex)
            {
                app.Formatter.WriteError(ex.Message);
                context.ExitCode = 1;
                return;
            }

            string? secretValue = null;

            if (!string.IsNullOrWhiteSpace(key))
            {
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

                    secretValue = entry.Value;
                }
                catch (VaultException ex)
                {
                    app.Formatter.WriteError(ex.Message);
                    context.ExitCode = 1;
                    return;
                }
            }
            else if (!string.IsNullOrEmpty(directValue))
            {
                secretValue = directValue;
            }
            else if (Console.IsInputRedirected)
            {
                secretValue = Console.In.ReadToEnd().TrimEnd('\r', '\n');
            }
            else
            {
                app.Formatter.WriteError("Secret key or --value must be provided, or piped via standard input.");
                context.ExitCode = 1;
                return;
            }

            try
            {
                var result = app.ShareService.CreateShare(secretValue, ttl);
                var combined = $"{result.EnvelopeToken}#{result.Key}";

                if (isRaw)
                {
                    app.Formatter.WriteRaw(combined);
                }
                else
                {
                    app.Formatter.WriteSuccess($"Ephemeral share created (Expires at {result.ExpiresAt:u}):");
                    app.Formatter.WriteSuccess($"Envelope: {result.EnvelopeToken}");
                    app.Formatter.WriteSuccess($"Key:      {result.Key}");
                    app.Formatter.WriteSuccess($"Combined: {combined}");
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
