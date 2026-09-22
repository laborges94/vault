using System.Text;
using Vault.Core.Cryptography;
using Vault.Core.Exceptions;

namespace Vault.Cli.Authentication;

/// <summary>
/// Resolves master passphrases from non-interactive sources or masked interactive terminal input.
/// </summary>
public sealed class PassphraseProvider : IPassphraseProvider
{
    private readonly TextReader? _inputReader;
    private readonly TextWriter _promptWriter;
    private readonly Func<string, string?> _envLookup;

    public PassphraseProvider(
        TextReader? inputReader = null,
        TextWriter? promptWriter = null,
        Func<string, string?>? envLookup = null)
    {
        _inputReader = inputReader;
        _promptWriter = promptWriter ?? Console.Error;
        _envLookup = envLookup ?? Environment.GetEnvironmentVariable;
    }

    public SecureCharBuffer GetPassphrase(
        PassphraseOptions? options = null,
        string prompt = "Enter master passphrase: ")
    {
        // 1. Stdin pipe
        if (options?.PassphraseStdin == true)
        {
            return ReadFromReader(_inputReader ?? Console.In);
        }

        // 2. Passphrase file
        if (!string.IsNullOrWhiteSpace(options?.PassphraseFile))
        {
            if (!File.Exists(options.PassphraseFile))
            {
                throw new FileNotFoundException($"Passphrase file not found at '{options.PassphraseFile}'.", options.PassphraseFile);
            }

            var text = File.ReadAllText(options.PassphraseFile).TrimEnd('\r', '\n');
            return new SecureCharBuffer(text.ToCharArray());
        }

        // 3. Environment variable
        var envPass = _envLookup("VAULT_PASSPHRASE");
        if (!string.IsNullOrEmpty(envPass))
        {
            return new SecureCharBuffer(envPass.ToCharArray());
        }

        // 4. Interactive prompt
        if (_inputReader == null && Console.IsInputRedirected)
        {
            throw new VaultAuthenticationException(
                "Non-interactive terminal detected but no passphrase was provided. Use VAULT_PASSPHRASE, --passphrase-stdin, or --passphrase-file.");
        }

        if (_inputReader != null)
        {
            _promptWriter.Write(prompt);
            return ReadFromReader(_inputReader);
        }

        return ReadMaskedPassword(prompt);
    }

    public SecureCharBuffer GetNewPassphrase(
        PassphraseOptions? options = null,
        string prompt = "Enter new master passphrase: ",
        string confirmPrompt = "Confirm master passphrase: ")
    {
        // If non-interactive source provided, skip interactive confirmation
        if (options?.PassphraseStdin == true ||
            !string.IsNullOrWhiteSpace(options?.PassphraseFile) ||
            !string.IsNullOrEmpty(_envLookup("VAULT_PASSPHRASE")))
        {
            var pass = GetPassphrase(options, prompt);
            if (pass.Length < 8)
            {
                pass.Dispose();
                throw new VaultValidationException("Master passphrase must be at least 8 characters long.");
            }
            return pass;
        }

        if (_inputReader != null)
        {
            _promptWriter.Write(prompt);
            var pass = ReadFromReader(_inputReader);
            if (pass.Length < 8)
            {
                pass.Dispose();
                throw new VaultValidationException("Master passphrase must be at least 8 characters long.");
            }

            _promptWriter.Write(confirmPrompt);
            var confirm = ReadFromReader(_inputReader);
            if (!pass.Span.SequenceEqual(confirm.Span))
            {
                pass.Dispose();
                confirm.Dispose();
                throw new VaultValidationException("Master passphrases do not match.");
            }

            confirm.Dispose();
            return pass;
        }

        var p1 = ReadMaskedPassword(prompt);
        if (p1.Length < 8)
        {
            p1.Dispose();
            throw new VaultValidationException("Master passphrase must be at least 8 characters long.");
        }

        var p2 = ReadMaskedPassword(confirmPrompt);
        if (!p1.Span.SequenceEqual(p2.Span))
        {
            p1.Dispose();
            p2.Dispose();
            throw new VaultValidationException("Master passphrases do not match.");
        }

        p2.Dispose();
        return p1;
    }

    private static SecureCharBuffer ReadFromReader(TextReader reader)
    {
        var line = reader.ReadLine();
        if (line == null)
        {
            throw new VaultAuthenticationException("End of stream reached while reading passphrase.");
        }

        var chars = line.TrimEnd('\r', '\n').ToCharArray();
        return new SecureCharBuffer(chars);
    }

    private SecureCharBuffer ReadMaskedPassword(string prompt)
    {
        _promptWriter.Write(prompt);

        var chars = new List<char>();
        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true);
            if (keyInfo.Key == ConsoleKey.Enter)
            {
                _promptWriter.WriteLine();
                break;
            }

            if (keyInfo.Key == ConsoleKey.Backspace)
            {
                if (chars.Count > 0)
                {
                    chars.RemoveAt(chars.Count - 1);
                }
            }
            else if (!char.IsControl(keyInfo.KeyChar))
            {
                chars.Add(keyInfo.KeyChar);
            }
        }

        var result = chars.ToArray();
        return new SecureCharBuffer(result);
    }
}
