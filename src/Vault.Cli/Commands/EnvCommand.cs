using System.CommandLine;
using System.CommandLine.Invocation;
using Vault.Core.Exceptions;

namespace Vault.Cli.Commands;

/// <summary>
/// Handles 'vault env push [file]' and 'vault env pull [file] [--force]'.
/// </summary>
public static class EnvCommand
{
    public static Command Create(VaultCliApp app)
    {
        var envCommand = new Command("env", "Synchronize vault secrets with local .env files");

        envCommand.AddCommand(CreatePushCommand(app));
        envCommand.AddCommand(CreatePullCommand(app));

        return envCommand;
    }

    private static Command CreatePushCommand(VaultCliApp app)
    {
        var fileArg = new Argument<string?>("file", () => ".env", "Path to the .env file to import");

        var pushCmd = new Command("push", "Imports key-value pairs from a .env file into the vault")
        {
            fileArg
        };

        pushCmd.SetHandler(async (InvocationContext context) =>
        {
            var filePath = context.ParseResult.GetValueForArgument(fileArg) ?? ".env";
            var vaultPath = context.ParseResult.GetValueForOption(app.VaultFileOption) ?? ".vault.enc";
            var environment = context.ParseResult.GetValueForOption(app.EnvOption);
            var passphraseOpts = app.GetPassphraseOptions(context.ParseResult);

            try
            {
                using var passphrase = app.PassphraseProvider.GetPassphrase(passphraseOpts);
                IReadOnlyList<string> warnings;

                if (filePath == "-" || (filePath == ".env" && !File.Exists(filePath) && Console.IsInputRedirected))
                {
                    var content = await Console.In.ReadToEndAsync();
                    warnings = await app.VaultService.PushEnvAsync(
                        vaultPath,
                        passphrase.ReadOnlySpan.ToArray().AsMemory(),
                        content,
                        environment: environment);
                }
                else
                {
                    if (!File.Exists(filePath))
                    {
                        app.Formatter.WriteError($"The .env file was not found at '{filePath}'.");
                        context.ExitCode = 1;
                        return;
                    }

                    warnings = await app.VaultService.PushEnvFileAsync(
                        vaultPath,
                        passphrase.ReadOnlySpan.ToArray().AsMemory(),
                        filePath,
                        environment: environment);
                }

                foreach (var warning in warnings)
                {
                    app.Formatter.WriteWarning(warning);
                }

                var envDisplay = string.IsNullOrWhiteSpace(environment) ? "default" : environment;
                app.Formatter.WriteSuccess($"Successfully imported secrets into environment '{envDisplay}'.");
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

        return pushCmd;
    }

    private static Command CreatePullCommand(VaultCliApp app)
    {
        var fileArg = new Argument<string?>("file", () => ".env", "Target path for the exported .env file");
        var forceOption = new Option<bool>(new[] { "--force", "-F" }, "Overwrite existing destination file without confirmation");

        var pullCmd = new Command("pull", "Exports secrets from the vault into a .env file")
        {
            fileArg,
            forceOption
        };

        pullCmd.SetHandler(async (InvocationContext context) =>
        {
            var filePath = context.ParseResult.GetValueForArgument(fileArg) ?? ".env";
            var force = context.ParseResult.GetValueForOption(forceOption);
            var vaultPath = context.ParseResult.GetValueForOption(app.VaultFileOption) ?? ".vault.enc";
            var environment = context.ParseResult.GetValueForOption(app.EnvOption);
            var passphraseOpts = app.GetPassphraseOptions(context.ParseResult);

            if (filePath != "-" && File.Exists(filePath) && !force)
            {
                if (Console.IsInputRedirected)
                {
                    app.Formatter.WriteError($"Destination file '{filePath}' already exists. Use --force to overwrite.");
                    context.ExitCode = 1;
                    return;
                }

                Console.Error.Write($"File '{filePath}' already exists. Overwrite? (y/N): ");
                var response = Console.ReadLine();
                if (!string.Equals(response?.Trim(), "y", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(response?.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
                {
                    app.Formatter.WriteWarning("Export cancelled.");
                    context.ExitCode = 0;
                    return;
                }
            }

            try
            {
                using var passphrase = app.PassphraseProvider.GetPassphrase(passphraseOpts);

                if (filePath == "-")
                {
                    var content = await app.VaultService.PullEnvAsync(
                        vaultPath,
                        passphrase.ReadOnlySpan.ToArray().AsMemory(),
                        environment: environment);
                    app.Formatter.WriteRaw(content);
                }
                else
                {
                    await app.VaultService.PullEnvFileAsync(
                        vaultPath,
                        passphrase.ReadOnlySpan.ToArray().AsMemory(),
                        filePath,
                        environment: environment,
                        overwrite: true);

                    app.Formatter.WriteSuccess($"Successfully exported secrets to '{filePath}'.");
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

        return pullCmd;
    }
}
