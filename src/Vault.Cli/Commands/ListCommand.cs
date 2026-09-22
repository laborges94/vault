using System.CommandLine;
using System.CommandLine.Invocation;
using Vault.Core.Exceptions;
using Vault.Core.Model;

namespace Vault.Cli.Commands;

/// <summary>
/// Handles 'vault list [--all-envs]'.
/// </summary>
public static class ListCommand
{
    private sealed record SecretListRow(
        string Key,
        string Environment,
        SecretEntry Entry);

    public static Command Create(VaultCliApp app)
    {
        var allEnvsOption = new Option<bool>(new[] { "--all-envs", "-a" }, "List secrets across all environments");

        var command = new Command("list", "Lists stored secrets and metadata in tabular format")
        {
            allEnvsOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var allEnvs = context.ParseResult.GetValueForOption(allEnvsOption);
            var vaultPath = context.ParseResult.GetValueForOption(app.VaultFileOption) ?? ".vault.enc";
            var environment = context.ParseResult.GetValueForOption(app.EnvOption);
            var passphraseOpts = app.GetPassphraseOptions(context.ParseResult);

            try
            {
                using var passphrase = app.PassphraseProvider.GetPassphrase(passphraseOpts);

                var rows = new List<SecretListRow>();

                if (allEnvs)
                {
                    var doc = await app.VaultService.LoadDocumentAsync(
                        vaultPath,
                        passphrase.ReadOnlySpan.ToArray().AsMemory());

                    foreach (var (envName, secrets) in doc.Environments.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        foreach (var (key, entry) in secrets.OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
                        {
                            rows.Add(new SecretListRow(key, envName, entry));
                        }
                    }

                    if (rows.Count == 0)
                    {
                        app.Formatter.WriteSuccess("No secrets found across any environment.");
                    }
                    else
                    {
                        app.Formatter.WriteTable(
                            rows,
                            ("Key", r => r.Key),
                            ("Environment", r => r.Environment),
                            ("Expires", r => r.Entry.ExpiresAt.HasValue ? r.Entry.ExpiresAt.Value.ToLocalTime().ToString("g") : "Never"),
                            ("Updated", r => r.Entry.UpdatedAt.ToLocalTime().ToString("g")),
                            ("Tags", r => r.Entry.Tags.Count > 0 ? string.Join(", ", r.Entry.Tags) : "-"),
                            ("Description", r => r.Entry.Description ?? "-"));
                    }
                }
                else
                {
                    var secrets = await app.VaultService.ListSecretsAsync(
                        vaultPath,
                        passphrase.ReadOnlySpan.ToArray().AsMemory(),
                        environment: environment);

                    var currentEnv = string.IsNullOrWhiteSpace(environment) ? VaultDocument.DefaultEnvironment : environment.Trim();

                    foreach (var (key, entry) in secrets.OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        rows.Add(new SecretListRow(key, currentEnv, entry));
                    }

                    if (rows.Count == 0)
                    {
                        app.Formatter.WriteSuccess($"No secrets found in environment '{currentEnv}'.");
                    }
                    else
                    {
                        app.Formatter.WriteTable(
                            rows,
                            ("Key", r => r.Key),
                            ("Expires", r => r.Entry.ExpiresAt.HasValue ? r.Entry.ExpiresAt.Value.ToLocalTime().ToString("g") : "Never"),
                            ("Updated", r => r.Entry.UpdatedAt.ToLocalTime().ToString("g")),
                            ("Tags", r => r.Entry.Tags.Count > 0 ? string.Join(", ", r.Entry.Tags) : "-"),
                            ("Description", r => r.Entry.Description ?? "-"));
                    }
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
