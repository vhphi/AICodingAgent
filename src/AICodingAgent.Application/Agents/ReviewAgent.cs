using AICodingAgent.Application.Abstractions;
using AICodingAgent.Domain.Agents;
using AICodingAgent.Domain.Requirements;

namespace AICodingAgent.Application.Agents;

public sealed class ReviewAgent
{
    private readonly ILanguageModel _llm;

    public ReviewAgent(ILanguageModel llm)
    {
        _llm = llm;
    }

    public async Task<CodeReviewResult> ReviewAsync(
        Requirement requirement,
        string diff,
        string testOutput,
        CancellationToken cancellationToken)
    {
        const string system = """
            You are a strict senior code reviewer.
            Review only against the requirement, diff, tests and engineering quality.
            Return ONLY valid JSON:
            {
              "findings":[
                {
                  "id":"CR-001",
                  "severity":"Critical|High|Medium|Low|Info",
                  "file":"...",
                  "line":123,
                  "description":"...",
                  "suggestedFix":"..."
                }
              ]
            }
            Do not invent issues without evidence.
            """;

        var prompt = $"""
            REQUIREMENT:
            {requirement.Description}

            DIFF:
            {diff}

            TEST OUTPUT:
            {testOutput}
            """;

        var response = await _llm.CompleteAsync(
            new CompletionRequest(system, prompt),
            cancellationToken);

        return JsonHelper.Deserialize<CodeReviewResult>(response.Text);
    }
}
