using System.CommandLine;
using System.CommandLine.Invocation;
using Vault.Core.Exceptions;

namespace Vault.Cli.Commands;

/// <summary>
/// Handles 'vault set <key> [value]'.
/// </summary>
public static class SetCommand
{
    public static Command Create(VaultCliApp app)
    {
        var keyArg = new Argument<string>("key", "Name of the secret key");
        var valueArg = new Argument<string?>("value", () => null, "Secret value payload");

        var descOption = new Option<string?>(new[] { "--description", "-d" }, "Optional description for the secret");
        var tagsOption = new Option<string?>(new[] { "--tags", "-t" }, "Comma-separated tags for the secret");
        var ttlOption = new Option<string?>(new[] { "--ttl" }, "Optional time-to-live expiration (e.g. '30m', '24h', '7d')");

        var command = new Command("set", "Stores or updates an encrypted secret in the vault")
        {
            keyArg,
            valueArg,
            descOption,
            tagsOption,
            ttlOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var key = context.ParseResult.GetValueForArgument(keyArg);
            var value = context.ParseResult.GetValueForArgument(valueArg);
            var description = context.ParseResult.GetValueForOption(descOption);
            var rawTags = context.ParseResult.GetValueForOption(tagsOption);
            var rawTtl = context.ParseResult.GetValueForOption(ttlOption);

            var vaultPath = context.ParseResult.GetValueForOption(app.VaultFileOption) ?? ".vault.enc";
            var environment = context.ParseResult.GetValueForOption(app.EnvOption);
            var passphraseOpts = app.GetPassphraseOptions(context.ParseResult);

            if (value == null)
            {
                if (Console.IsInputRedirected)
                {
                    value = Console.In.ReadToEnd().TrimEnd('\r', '\n');
                }
                else
                {
                    app.Formatter.WriteError("Secret value must be specified as an argument or piped via standard input.");
                    context.ExitCode = 1;
                    return;
                }
            }

            IReadOnlyList<string>? tags = null;
            if (!string.IsNullOrWhiteSpace(rawTags))
            {
                tags = rawTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }

            DateTimeOffset? expiresAt = null;
            if (!string.IsNullOrWhiteSpace(rawTtl))
            {
                try
                {
                    var ttl = TimeSpanParser.Parse(rawTtl);
                    expiresAt = DateTimeOffset.UtcNow.Add(ttl);
                }
                catch (FormatException ex)
                {
                    app.Formatter.WriteError(ex.Message);
                    context.ExitCode = 1;
                    return;
                }
            }

            try
            {
                using var passphrase = app.PassphraseProvider.GetPassphrase(passphraseOpts);
                await app.VaultService.SetSecretAsync(
                    vaultPath,
                    passphrase.ReadOnlySpan.ToArray().AsMemory(),
                    key,
                    value,
                    environment: environment,
                    description: description,
                    tags: tags,
                    expiresAt: expiresAt);

                var envDisplay = string.IsNullOrWhiteSpace(environment) ? "default" : environment;
                app.Formatter.WriteSuccess($"Secret '{key}' saved successfully in environment '{envDisplay}'.");
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
