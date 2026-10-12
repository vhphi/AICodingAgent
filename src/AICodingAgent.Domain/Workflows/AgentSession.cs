namespace AICodingAgent.Domain.Workflows;

public sealed class AgentSession
{
    private AgentSession()
    {
    }

    public AgentSession(Guid id, string name, string workspacePath)
    {
        Id = id;
        Name = name;
        WorkspacePath = workspacePath;
        State = AgentState.Created;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string WorkspacePath { get; private set; } = string.Empty;
    public AgentState State { get; private set; }
    public string? RequirementId { get; private set; }
    public string? GitBranch { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void ChangeState(AgentState state)
    {
        State = state;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetRequirement(string requirementId)
    {
        RequirementId = requirementId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetGitBranch(string branch)
    {
        GitBranch = branch;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
