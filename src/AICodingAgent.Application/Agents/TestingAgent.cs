using AICodingAgent.Application.Abstractions;
using AICodingAgent.Domain.Agents;
using AICodingAgent.Domain.Requirements;

namespace AICodingAgent.Application.Agents;

public sealed class TestingAgent
{
    private readonly ILanguageModel _llm;
    private readonly IWorkspace _workspace;

    public TestingAgent(ILanguageModel llm, IWorkspace workspace)
    {
        _llm = llm;
        _workspace = workspace;
    }

    public async Task<ImplementationResult> GenerateAsync(
        Requirement requirement,
        ProjectContext context,
        CancellationToken cancellationToken)
    {
        const string system = """
            You are a senior .NET test engineer.
            Generate unit tests for the implemented change.
            Return ONLY valid JSON:
            {
              "summary":"...",
              "changes":[{"path":"relative/path","action":"create|modify","content":"FULL FILE CONTENT"}]
            }
            Prefer the test framework and conventions already present in the repository.
            If no test project exists, create tests only if the existing solution structure clearly supports it.
            """;

        var prompt = $"""
            REQUIREMENT:
            {requirement.Description}

            ACCEPTANCE CRITERIA:
            {string.Join("\n- ", requirement.AcceptanceCriteria.Select(x => x.Text))}

            PROJECT CONTEXT:
            {context.ContextText}
            """;

        var response = await _llm.CompleteAsync(
            new CompletionRequest(system, prompt),
            cancellationToken);

        var result = JsonHelper.Deserialize<ImplementationResult>(response.Text);

        foreach (var change in result.Changes)
        {
            await _workspace.WriteFileAsync(change.Path, change.Content, cancellationToken);
        }

        return result;
    }
}
