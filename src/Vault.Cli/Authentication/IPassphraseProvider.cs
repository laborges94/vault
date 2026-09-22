using Vault.Core.Cryptography;

namespace Vault.Cli.Authentication;

/// <summary>
/// CLI options for non-interactive passphrase provisioning.
/// </summary>
public sealed record PassphraseOptions
{
    public bool PassphraseStdin { get; init; }
    public string? PassphraseFile { get; init; }
}

/// <summary>
/// Contract for retrieving master passphrases from interactive terminal or non-interactive sources.
/// </summary>
public interface IPassphraseProvider
{
    /// <summary>
    /// Obtains the passphrase for an existing vault container.
    /// </summary>
    SecureCharBuffer GetPassphrase(
        PassphraseOptions? options = null,
        string prompt = "Enter master passphrase: ");

    /// <summary>
    /// Obtains and confirms the passphrase for initializing a new vault.
    /// </summary>
    SecureCharBuffer GetNewPassphrase(
        PassphraseOptions? options = null,
        string prompt = "Enter new master passphrase: ",
        string confirmPrompt = "Confirm master passphrase: ");
}
