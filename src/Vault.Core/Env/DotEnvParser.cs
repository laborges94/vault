using System.Text;
using Vault.Core.Exceptions;
using Vault.Core.Model;

namespace Vault.Core.Env;

/// <summary>
/// Encapsulates the output of parsing a .env file, including entries and duplicate key warnings.
/// </summary>
public sealed record DotEnvParseResult(
    IReadOnlyDictionary<string, string> Entries,
    IReadOnlyList<string> DuplicateKeyWarnings);

/// <summary>
/// Parser and serializer for standard .env files supporting comments, quotes, escaping, and duplicate handling.
/// </summary>
public static class DotEnvParser
{
    public static DotEnvParseResult Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var reader = new StringReader(content);
        return Parse(reader);
    }

    public static DotEnvParseResult Parse(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        var warnings = new List<string>();

        string? line;
        var lineNumber = 0;

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            var trimmedLine = line.Trim();

            // Ignore empty lines and comment lines
            if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#'))
            {
                continue;
            }

            // Strip optional 'export ' prefix (common in shell-sourced .env)
            if (trimmedLine.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
            {
                trimmedLine = trimmedLine[7..].TrimStart();
            }

            var separatorIndex = trimmedLine.IndexOf('=');
            if (separatorIndex < 0)
            {
                throw new VaultValidationException($"Malformed .env entry on line {lineNumber}: '{line}'. Expected KEY=VALUE format.");
            }

            var key = trimmedLine[..separatorIndex].Trim();
            SecretKeyValidator.ValidateKey(key);

            var rawValue = trimmedLine[(separatorIndex + 1)..].Trim();
            var parsedValue = ParseValue(rawValue);
            SecretKeyValidator.ValidateValue(parsedValue);

            if (entries.ContainsKey(key))
            {
                warnings.Add($"Duplicate key '{key}' on line {lineNumber}. Overwriting previous value with last-write-wins.");
            }

            entries[key] = parsedValue;
        }

        return new DotEnvParseResult(entries, warnings);
    }

    private static string ParseValue(string rawValue)
    {
        if (rawValue.Length == 0)
        {
            return string.Empty;
        }

        // Double-quoted string: "value"
        if (rawValue.Length >= 2 && rawValue.StartsWith('"') && rawValue.EndsWith('"'))
        {
            var inner = rawValue[1..^1];
            return UnescapeDoubleQuoted(inner);
        }

        // Single-quoted string: 'value' (literal, no unescaping)
        if (rawValue.Length >= 2 && rawValue.StartsWith('\'') && rawValue.EndsWith('\''))
        {
            return rawValue[1..^1];
        }

        // Unquoted: check for inline comments starting with '#' preceded by whitespace
        var commentIndex = -1;
        for (var i = 0; i < rawValue.Length; i++)
        {
            if (rawValue[i] == '#' && i > 0 && char.IsWhiteSpace(rawValue[i - 1]))
            {
                commentIndex = i;
                break;
            }
        }

        if (commentIndex >= 0)
        {
            rawValue = rawValue[..commentIndex].TrimEnd();
        }

        return rawValue;
    }

    private static string UnescapeDoubleQuoted(string value)
    {
        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                var next = value[i + 1];
                switch (next)
                {
                    case 'n':
                        sb.Append('\n');
                        i++;
                        break;
                    case 'r':
                        sb.Append('\r');
                        i++;
                        break;
                    case 't':
                        sb.Append('\t');
                        i++;
                        break;
                    case '"':
                        sb.Append('"');
                        i++;
                        break;
                    case '\\':
                        sb.Append('\\');
                        i++;
                        break;
                    default:
                        sb.Append(next);
                        i++;
                        break;
                }
            }
            else
            {
                sb.Append(value[i]);
            }
        }

        return sb.ToString();
    }

    public static string Serialize(IReadOnlyDictionary<string, string> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var sb = new StringBuilder();
        foreach (var (key, value) in entries.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            sb.AppendLine(FormatEntry(key, value));
        }

        return sb.ToString();
    }

    public static string Serialize(IEnumerable<KeyValuePair<string, SecretEntry>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var sb = new StringBuilder();
        foreach (var (key, entry) in entries.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(entry.Description))
            {
                sb.AppendLine($"# {entry.Description}");
            }
            sb.AppendLine(FormatEntry(key, entry.Value));
        }

        return sb.ToString();
    }

    private static string FormatEntry(string key, string value)
    {
        // Quote if contains newline, tab, quotes, comment hash, or leading/trailing whitespace
        if (value.Contains('\n') || value.Contains('\r') || value.Contains('\t') ||
            value.Contains('"') || value.Contains('#') ||
            value.StartsWith(' ') || value.EndsWith(' '))
        {
            var escaped = value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");

            return $"{key}=\"{escaped}\"";
        }

        return $"{key}={value}";
    }
}
