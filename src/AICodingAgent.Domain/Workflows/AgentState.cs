namespace AICodingAgent.Domain.Workflows;

public enum AgentState
{
    Created,
    LoadingRequirement,
    AnalyzingRequirement,
    WaitingForClarification,
    Planning,
    WaitingForPlanApproval,
    Implementing,
    Building,
    GeneratingTests,
    Testing,
    Reviewing,
    Fixing,
    ReadyForCommit,
    Committing,
    Completed,
    Failed,
    Cancelled,
    Committed
}
