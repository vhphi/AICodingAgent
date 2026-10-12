using AICodingAgent.Domain.Requirements;

namespace AICodingAgent.Domain.Agents;

public sealed class RequirementAnalysis
{
    public bool IsReadyForImplementation { get; init; }
    public IReadOnlyList<RequirementQuestion> Questions { get; init; } = Array.Empty<RequirementQuestion>();
    public IReadOnlyList<string> Assumptions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Risks { get; init; } = Array.Empty<string>();
}

public sealed class RequirementQuestion
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Question { get; init; } = string.Empty;
    public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();
}

public sealed class RequirementAnswer
{
    public string QuestionId { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
}

public sealed class ImplementationPlan
{
    public IReadOnlyList<PlanStep> Steps { get; init; } = Array.Empty<PlanStep>();
    public IReadOnlyList<string> FilesToCreate { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> FilesToModify { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> FilesToDelete { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Risks { get; init; } = Array.Empty<string>();
}

public sealed class PlanStep
{
    public int Order { get; init; }
    public string Action { get; init; } = string.Empty;
    public string File { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class FileChange
{
    public string Path { get; init; } = string.Empty;
    public string Action { get; init; } = "modify";
    public string Content { get; init; } = string.Empty;
}

public sealed class ImplementationResult
{
    public IReadOnlyList<FileChange> Changes { get; init; } = Array.Empty<FileChange>();
    public string Summary { get; init; } = string.Empty;
}

public enum ReviewSeverity
{
    Critical,
    High,
    Medium,
    Low,
    Info
}

public sealed class ReviewFinding
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public ReviewSeverity Severity { get; init; }
    public string File { get; init; } = string.Empty;
    public int? Line { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? SuggestedFix { get; init; }
}

public sealed class CodeReviewResult
{
    public IReadOnlyList<ReviewFinding> Findings { get; init; } = Array.Empty<ReviewFinding>();
    public bool Passed => Findings.All(x => x.Severity is not ReviewSeverity.Critical and not ReviewSeverity.High);
}

public sealed class ProjectContext
{
    public string WorkspacePath { get; init; } = string.Empty;
    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RelevantFiles { get; init; } = Array.Empty<string>();
    public string ContextText { get; init; } = string.Empty;
}

public sealed class BuildResult
{
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
}

public sealed class TestRunResult
{
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
}

public sealed class GitStatus
{
    public bool IsRepository { get; init; }
    public bool IsDirty { get; init; }
    public string Output { get; init; } = string.Empty;
}

public sealed class GitDiff
{
    public string Text { get; init; } = string.Empty;
}
