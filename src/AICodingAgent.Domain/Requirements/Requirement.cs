namespace AICodingAgent.Domain.Requirements;

public sealed class Requirement
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public RequirementSource Source { get; init; } = new();
    public IReadOnlyList<string> FunctionalRequirements { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> BusinessRules { get; init; } = Array.Empty<string>();
    public IReadOnlyList<AcceptanceCriterion> AcceptanceCriteria { get; init; } = Array.Empty<AcceptanceCriterion>();
}

public sealed class RequirementSource
{
    public string Type { get; init; } = "text";
    public string Location { get; init; } = string.Empty;
    public string? Content { get; init; }
}

public sealed class AcceptanceCriterion
{
    public string Text { get; init; } = string.Empty;
}
