using AICodingAgent.Application.Abstractions;

namespace AICodingAgent.Infrastructure.Workspace;

public sealed class FileSystemWorkspaceFactory : IWorkspaceFactory
{
    public IWorkspace Create(string root)
    {
        return new FileSystemWorkspace(root ?? Directory.GetCurrentDirectory());
    }
}
