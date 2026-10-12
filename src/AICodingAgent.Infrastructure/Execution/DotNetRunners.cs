using AICodingAgent.Application.Abstractions;
using AICodingAgent.Domain.Agents;

namespace AICodingAgent.Infrastructure.Execution;

public sealed class DotNetBuildRunner : IBuildRunner
{
    private readonly ProcessRunner _runner;

    public DotNetBuildRunner(ProcessRunner runner)
    {
        _runner = runner;
    }

    public async Task<BuildResult> BuildAsync(
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            "dotnet",
            "build --nologo",
            workspacePath,
            cancellationToken);

        return new BuildResult
        {
            Success = result.ExitCode == 0,
            ExitCode = result.ExitCode,
            Output = result.Output
        };
    }
}

public sealed class DotNetTestRunner : ITestRunner
{
    private readonly ProcessRunner _runner;

    public DotNetTestRunner(ProcessRunner runner)
    {
        _runner = runner;
    }

    public async Task<TestRunResult> TestAsync(
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            "dotnet",
            "test --nologo",
            workspacePath,
            cancellationToken);

        return new TestRunResult
        {
            Success = result.ExitCode == 0,
            ExitCode = result.ExitCode,
            Output = result.Output
        };
    }
}
