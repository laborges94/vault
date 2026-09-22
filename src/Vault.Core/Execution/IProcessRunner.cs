namespace Vault.Core.Execution;

/// <summary>
/// Execution options for child processes with injected secrets.
/// </summary>
public sealed record ProcessExecutionOptions
{
    public required string Command { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } = new Dictionary<string, string>();
    public string? WorkingDirectory { get; init; }
    public TextReader? StandardInput { get; init; }
    public TextWriter? StandardOutput { get; init; }
    public TextWriter? StandardError { get; init; }
}

/// <summary>
/// Service contract for spawning child processes with in-memory environment secret injection.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Spawns a child process with injected environment variables and waits for exit, returning its exit code.
    /// </summary>
    Task<int> RunAsync(ProcessExecutionOptions options, CancellationToken cancellationToken = default);

    Task<int> RunAsync(
        string command,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> environmentVariables,
        string? workingDirectory = null,
        TextReader? standardInput = null,
        TextWriter? standardOutput = null,
        TextWriter? standardError = null,
        CancellationToken cancellationToken = default) =>
        RunAsync(new ProcessExecutionOptions
        {
            Command = command,
            Arguments = arguments,
            EnvironmentVariables = environmentVariables,
            WorkingDirectory = workingDirectory,
            StandardInput = standardInput,
            StandardOutput = standardOutput,
            StandardError = standardError
        }, cancellationToken);
}
