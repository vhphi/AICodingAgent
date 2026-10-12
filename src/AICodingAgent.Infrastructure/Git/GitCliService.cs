using AICodingAgent.Application.Abstractions;
using AICodingAgent.Domain.Agents;
using AICodingAgent.Infrastructure.Execution;

namespace AICodingAgent.Infrastructure.Git;

public sealed class GitCliService : IGitService
{
    private readonly ProcessRunner _runner;

    public GitCliService(ProcessRunner runner)
    {
        _runner = runner;
    }

    public async Task<GitStatus> GetStatusAsync(
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            "git",
            "status --porcelain",
            workspacePath,
            cancellationToken);

        var probe = await _runner.RunAsync(
            "git",
            "rev-parse --is-inside-work-tree",
            workspacePath,
            cancellationToken);

        return new GitStatus
        {
            IsRepository = probe.ExitCode == 0,
            IsDirty = !string.IsNullOrWhiteSpace(result.Output),
            Output = result.Output
        };
    }

    public async Task CreateBranchAsync(
        string workspacePath,
        string branchName,
        CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            "git",
            $"switch -c \"{branchName}\"",
            workspacePath,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Unable to create Git branch:\n" + result.Output);
        }
    }

    public async Task<GitDiff> GetDiffAsync(
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            "git",
            "diff --no-ext-diff",
            workspacePath,
            cancellationToken);

        return new GitDiff
        {
            Text = result.Output
        };
    }

    public async Task StageAllAsync(
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            "git",
            "add -A",
            workspacePath,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.Output);
        }
    }

    public async Task<string> CommitAsync(
        string workspacePath,
        string message,
        CancellationToken cancellationToken)
    {
        var escaped = message.Replace("\"", "\\\"");
        var result = await _runner.RunAsync(
            "git",
            $"commit -m \"{escaped}\"",
            workspacePath,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.Output);
        }

        return result.Output;
    }
}
