using AICodingAgent.Domain.Agents;

namespace AICodingAgent.Application.Abstractions;

public interface IWorkspace
{
    Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken);
    Task WriteFileAsync(string relativePath, string content, CancellationToken cancellationToken);
    Task DeleteFileAsync(string relativePath, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> FindFilesAsync(CancellationToken cancellationToken);
    string ResolvePath(string relativePath);
}

public interface ICodeSearch
{
    Task<IReadOnlyList<string>> SearchAsync(string query, CancellationToken cancellationToken);
    Task<ProjectContext> BuildContextAsync(
        string workspacePath,
        string requirementText,
        CancellationToken cancellationToken);
}

public interface IBuildRunner
{
    Task<BuildResult> BuildAsync(string workspacePath, CancellationToken cancellationToken);
}

public interface ITestRunner
{
    Task<TestRunResult> TestAsync(string workspacePath, CancellationToken cancellationToken);
}

public interface IGitService
{
    Task<GitStatus> GetStatusAsync(string workspacePath, CancellationToken cancellationToken);
    Task CreateBranchAsync(string workspacePath, string branchName, CancellationToken cancellationToken);
    Task<GitDiff> GetDiffAsync(string workspacePath, CancellationToken cancellationToken);
    Task StageAllAsync(string workspacePath, CancellationToken cancellationToken);
    Task<string> CommitAsync(string workspacePath, string message, CancellationToken cancellationToken);
}

public interface IRequirementReader
{
    Task<string> ReadAsync(string source, CancellationToken cancellationToken);
}

public interface IAgentEventSink
{
    Task PublishAsync(
        Guid sessionId,
        string eventName,
        object payload,
        CancellationToken cancellationToken);
}

public interface IWorkspaceFactory
{
    IWorkspace Create(string root);
}
