using System.Diagnostics;

namespace AICodingAgent.Infrastructure.Execution;

public sealed class ProcessRunner
{
    public async Task<(int ExitCode, string Output)> RunAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process
        {
            StartInfo = psi
        };

        process.Start();

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = (await stdout) + Environment.NewLine + (await stderr);

        return (process.ExitCode, output);
    }
}
