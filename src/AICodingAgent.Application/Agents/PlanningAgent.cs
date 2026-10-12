using AICodingAgent.Application.Abstractions;
using AICodingAgent.Domain.Agents;
using AICodingAgent.Domain.Requirements;

namespace AICodingAgent.Application.Agents;

public sealed class PlanningAgent
{
    private readonly ILanguageModel _llm;

    public PlanningAgent(ILanguageModel llm)
    {
        _llm = llm;
    }

    public async Task<ImplementationPlan> CreatePlanAsync(
        Requirement requirement,
        ProjectContext context,
        CancellationToken cancellationToken)
    {
        const string system = """
            You are a senior .NET solution architect.
            Create a minimal implementation plan that follows the existing project patterns.
            Return ONLY valid JSON:
            {
              "steps":[{"order":1,"action":"create|modify|delete","file":"relative/path","description":"..."}],
              "filesToCreate":[],
              "filesToModify":[],
              "filesToDelete":[],
              "risks":[]
            }
            Do not invent frameworks or dependencies unless necessary.
            """;

        var prompt = $"""
            REQUIREMENT:
            {requirement.Description}

            ACCEPTANCE:
            {string.Join("\n- ", requirement.AcceptanceCriteria.Select(x => x.Text))}

            PROJECT FILES:
            {string.Join("\n", context.RelevantFiles)}

            CONTEXT:
            {context.ContextText}
            """;

        var response = await _llm.CompleteAsync(
            new CompletionRequest(system, prompt),
            cancellationToken);

        return JsonHelper.Deserialize<ImplementationPlan>(response.Text);
    }
}
