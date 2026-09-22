using System.Text;

namespace Vault.Cli.Output;

/// <summary>
/// Formats console messages, errors, and tables with stream redirection support.
/// </summary>
public sealed class ConsoleFormatter : IConsoleFormatter
{
    private readonly TextWriter _out;
    private readonly TextWriter _err;

    public ConsoleFormatter(TextWriter? output = null, TextWriter? error = null)
    {
        _out = output ?? Console.Out;
        _err = error ?? Console.Error;
    }

    public void WriteRaw(string text)
    {
        _out.Write(text);
    }

    public void WriteSuccess(string message)
    {
        _out.WriteLine(message);
    }

    public void WriteWarning(string message)
    {
        _err.WriteLine($"Warning: {message}");
    }

    public void WriteError(string message)
    {
        _err.WriteLine($"Error: {message}");
    }

    public void WriteTable<T>(IEnumerable<T> items, params (string Header, Func<T, string> Selector)[] columns)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(columns);

        var list = items.ToList();
        if (list.Count == 0 || columns.Length == 0)
        {
            return;
        }

        var colWidths = new int[columns.Length];
        for (var c = 0; c < columns.Length; c++)
        {
            colWidths[c] = columns[c].Header.Length;
        }

        var rowValues = new List<string[]>();
        foreach (var item in list)
        {
            var row = new string[columns.Length];
            for (var c = 0; c < columns.Length; c++)
            {
                var val = columns[c].Selector(item) ?? string.Empty;
                row[c] = val;
                if (val.Length > colWidths[c])
                {
                    colWidths[c] = val.Length;
                }
            }
            rowValues.Add(row);
        }

        // Header line
        var headerSb = new StringBuilder();
        for (var c = 0; c < columns.Length; c++)
        {
            headerSb.Append(columns[c].Header.PadRight(colWidths[c]));
            if (c < columns.Length - 1)
            {
                headerSb.Append("  ");
            }
        }
        _out.WriteLine(headerSb.ToString());

        // Separator line
        var sepSb = new StringBuilder();
        for (var c = 0; c < columns.Length; c++)
        {
            sepSb.Append(new string('-', colWidths[c]));
            if (c < columns.Length - 1)
            {
                sepSb.Append("  ");
            }
        }
        _out.WriteLine(sepSb.ToString());

        // Rows
        foreach (var row in rowValues)
        {
            var rowSb = new StringBuilder();
            for (var c = 0; c < columns.Length; c++)
            {
                rowSb.Append(row[c].PadRight(colWidths[c]));
                if (c < columns.Length - 1)
                {
                    rowSb.Append("  ");
                }
            }
            _out.WriteLine(rowSb.ToString());
        }
    }
}
