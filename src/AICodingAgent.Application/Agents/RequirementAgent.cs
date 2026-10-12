using AICodingAgent.Application.Abstractions;
using AICodingAgent.Domain.Agents;
using AICodingAgent.Domain.Requirements;

namespace AICodingAgent.Application.Agents;

public sealed class RequirementAgent
{
    private readonly ILanguageModel _llm;

    public RequirementAgent(ILanguageModel llm)
    {
        _llm = llm;
    }

    public async Task<RequirementAnalysis> AnalyzeAsync(
        Requirement requirement,
        ProjectContext context,
        CancellationToken cancellationToken)
    {
        const string system = """
            You are a senior software analyst.
            Analyze the requirement and determine whether it is specific enough to implement.
            Return ONLY valid JSON:
            {
              "isReadyForImplementation": true,
              "questions": [{"id":"q1","question":"...","options":["..."]}],
              "assumptions": ["..."],
              "risks": ["..."]
            }
            Ask questions only when missing information could materially change implementation.
            """;

        var prompt = $"""
            REQUIREMENT:
            {requirement.Description}

            FUNCTIONAL REQUIREMENTS:
            {string.Join("\n- ", requirement.FunctionalRequirements)}

            BUSINESS RULES:
            {string.Join("\n- ", requirement.BusinessRules)}

            ACCEPTANCE CRITERIA:
            {string.Join("\n- ", requirement.AcceptanceCriteria.Select(x => x.Text))}

            PROJECT CONTEXT:
            {context.ContextText}
            """;

        var response = await _llm.CompleteAsync(
            new CompletionRequest(system, prompt),
            cancellationToken);

        return JsonHelper.Deserialize<RequirementAnalysis>(response.Text);
    }
}
