using System.Diagnostics;

namespace Vault.Core.Execution;

/// <summary>
/// Spawns and supervises child processes with environment variables, I/O forwarding, and cancellation support.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    public async Task<int> RunAsync(ProcessExecutionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Command);

        var startInfo = new ProcessStartInfo
        {
            FileName = options.Command,
            UseShellExecute = false,
            RedirectStandardInput = options.StandardInput != null,
            RedirectStandardOutput = options.StandardOutput != null,
            RedirectStandardError = options.StandardError != null,
            CreateNoWindow = true,
            WorkingDirectory = options.WorkingDirectory ?? Directory.GetCurrentDirectory()
        };

        foreach (var arg in options.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        foreach (var (key, value) in options.EnvironmentVariables)
        {
            startInfo.EnvironmentVariables[key] = value;
        }

        using var process = new Process { StartInfo = startInfo };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to launch process '{options.Command}'.");
        }

        await using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Process may have already exited
            }
        });

        var stdInTask = Task.CompletedTask;
        if (options.StandardInput != null)
        {
            stdInTask = Task.Run(async () =>
            {
                try
                {
                    var buffer = new char[4096];
                    int read;
                    while ((read = await options.StandardInput.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await process.StandardInput.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    }
                    process.StandardInput.Close();
                }
                catch
                {
                    // Stream closed
                }
            }, cancellationToken);
        }

        var stdOutTask = Task.CompletedTask;
        if (options.StandardOutput != null)
        {
            stdOutTask = Task.Run(async () =>
            {
                try
                {
                    var buffer = new char[4096];
                    int read;
                    while ((read = await process.StandardOutput.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await options.StandardOutput.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                        await options.StandardOutput.FlushAsync().ConfigureAwait(false);
                    }
                }
                catch
                {
                    // Output closed
                }
            }, cancellationToken);
        }

        var stdErrTask = Task.CompletedTask;
        if (options.StandardError != null)
        {
            stdErrTask = Task.Run(async () =>
            {
                try
                {
                    var buffer = new char[4096];
                    int read;
                    while ((read = await process.StandardError.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await options.StandardError.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                        await options.StandardError.FlushAsync().ConfigureAwait(false);
                    }
                }
                catch
                {
                    // Error closed
                }
            }, cancellationToken);
        }

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignored
            }

            throw;
        }

        await Task.WhenAll(stdInTask, stdOutTask, stdErrTask).ConfigureAwait(false);
        return process.ExitCode;
    }

    public Task<int> RunAsync(
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
