namespace Vault.Cli.Output;

/// <summary>
/// Service contract for formatting CLI console output, raw output, tables, and errors.
/// </summary>
public interface IConsoleFormatter
{
    /// <summary>
    /// Writes raw, unformatted text without trailing newline (used with --raw for piping).
    /// </summary>
    void WriteRaw(string text);

    /// <summary>
    /// Writes an informative or success message to standard output.
    /// </summary>
    void WriteSuccess(string message);

    /// <summary>
    /// Writes a warning message to standard error.
    /// </summary>
    void WriteWarning(string message);

    /// <summary>
    /// Writes an error message to standard error.
    /// </summary>
    void WriteError(string message);

    /// <summary>
    /// Formats and renders a collection of items into an aligned tabular display.
    /// </summary>
    void WriteTable<T>(IEnumerable<T> items, params (string Header, Func<T, string> Selector)[] columns);
}
