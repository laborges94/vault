namespace Vault.Cli;

/// <summary>
/// Parses human-readable duration strings (e.g. '30s', '15m', '1h', '7d') into TimeSpan instances.
/// </summary>
public static class TimeSpanParser
{
    public static TimeSpan Parse(string? input, TimeSpan? defaultIfEmpty = null)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultIfEmpty ?? TimeSpan.FromHours(1);
        }

        var s = input.Trim().ToLowerInvariant();

        if (s.EndsWith('s') && double.TryParse(s[..^1], out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }
        if (s.EndsWith('m') && double.TryParse(s[..^1], out var minutes))
        {
            return TimeSpan.FromMinutes(minutes);
        }
        if (s.EndsWith('h') && double.TryParse(s[..^1], out var hours))
        {
            return TimeSpan.FromHours(hours);
        }
        if (s.EndsWith('d') && double.TryParse(s[..^1], out var days))
        {
            return TimeSpan.FromDays(days);
        }

        if (double.TryParse(s, out var numMinutes))
        {
            return TimeSpan.FromMinutes(numMinutes);
        }

        throw new FormatException($"Invalid duration format '{input}'. Expected format like '30s', '15m', '1h', or '7d'.");
    }
}
