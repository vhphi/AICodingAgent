using AICodingAgent.Application.Abstractions;
using AICodingAgent.Domain.Agents;
using AICodingAgent.Domain.Requirements;
using System.Numerics;

namespace AICodingAgent.Application.Agents;

public sealed class CodingAgent
{
    private readonly ICodingExecutor _executor;
    private readonly ILanguageModel _llm;
    private readonly IWorkspaceFactory _workspaceFactory;

    public CodingAgent(ICodingExecutor executor, ILanguageModel llm, IWorkspaceFactory workspaceFactory)
    {
        _executor = executor;
        _llm = llm;
        _workspaceFactory = workspaceFactory;
    }

    public Task<CodingExecutionResult> ImplementCodexAsync(
        Requirement requirement,
        ImplementationPlan implementationPlan, ProjectContext context,
        CancellationToken cancellationToken)
    {
        var instruction =
            "Implement this requirement in the current repository.\n\n" +
            "REQUIREMENT:\n" +
            requirement.Description +
            "\n\n" +
            "PLAN:\n" +
            System.Text.Json.JsonSerializer.Serialize(implementationPlan) +
            "\n\n" +
            "Rules:\n" +
            "- Inspect the existing code first.\n" +
            "- Follow the existing architecture.\n" +
            "- Modify only necessary files.\n" +
            "- Add or update tests.\n" +
            "- Run the tests.\n" +
            "- Fix compilation and test failures.\n" +
            "- Do not commit Git changes.";

        return _executor.ExecuteAsync(
            context.WorkspacePath,
            instruction,
            cancellationToken);
    }

    public async Task<ImplementationResult> FixCodexAsync(
        string failureOutput,
        ProjectContext context,
        CancellationToken cancellationToken)
    {
        const string system = """
            You are a senior .NET debugging engineer.
            Diagnose the build/test failure and return ONLY valid JSON:
            {
              "summary":"...",
              "changes":[{"path":"relative/path","action":"modify","content":"FULL FILE CONTENT"}]
            }
            Only change files that are necessary to fix the failure.
            """;

        var prompt = $"""
            FAILURE:
            {failureOutput}

            PROJECT CONTEXT:
            {context.ContextText}
            """;

        var response = await _llm.CompleteAsync(
            new CompletionRequest(system, prompt),
            cancellationToken);

        var result = JsonHelper.Deserialize<ImplementationResult>(response.Text);

        var workspace = _workspaceFactory.Create(context.WorkspacePath);

        foreach (var change in result.Changes)
        {
            await workspace.WriteFileAsync(change.Path, change.Content, cancellationToken);
        }

        return result;
    }

    public async Task<ImplementationResult> ImplementAsync(
        Requirement requirement,
        ImplementationPlan plan,
        ProjectContext context,
        CancellationToken cancellationToken)
    {
        const string system = """
            You are an expert software engineer.
            Implement the approved plan.
            Return ONLY valid JSON:
            {
              "summary":"...",
              "changes":[
                {
                  "path":"relative/path",
                  "action":"create|modify",
                  "content":"FULL FILE CONTENT"
                }
              ]
            }

            Rules:
            - Never return a path outside the workspace.
            - Return complete file content, not patches.
            - Modify only files required by the plan.
            - Preserve existing coding style and target framework.
            - Do not include markdown fences.
            """;

        var prompt = $"""
            REQUIREMENT:
            {requirement.Description}

            PLAN:
            {System.Text.Json.JsonSerializer.Serialize(plan)}

            RELEVANT PROJECT CONTEXT:
            {context.ContextText}
            """;

        var response = await _llm.CompleteAsync(
            new CompletionRequest(system, prompt),
            cancellationToken);

        var result = JsonHelper.Deserialize<ImplementationResult>(response.Text);

        var workspace = _workspaceFactory.Create(context.WorkspacePath);

        foreach (var change in result.Changes)
        {
            if (!string.Equals(change.Action, "create", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(change.Action, "modify", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            await workspace.WriteFileAsync(
                change.Path,
                change.Content,
                cancellationToken);
        }

        return result;
    }

    public async Task<ImplementationResult> FixAsync(
        string failureOutput,
        ProjectContext context,
        CancellationToken cancellationToken)
    {
        const string system = """
            You are a senior .NET debugging engineer.
            Diagnose the build/test failure and return ONLY valid JSON:
            {
              "summary":"...",
              "changes":[{"path":"relative/path","action":"modify","content":"FULL FILE CONTENT"}]
            }
            Only change files that are necessary to fix the failure.
            """;

        var prompt = $"""
            FAILURE:
            {failureOutput}

            PROJECT CONTEXT:
            {context.ContextText}
            """;

        var response = await _llm.CompleteAsync(
            new CompletionRequest(system, prompt),
            cancellationToken);

        var result = JsonHelper.Deserialize<ImplementationResult>(response.Text);

        var workspace = _workspaceFactory.Create(context.WorkspacePath);

        foreach (var change in result.Changes)
        {
            await workspace.WriteFileAsync(change.Path, change.Content, cancellationToken);
        }

        return result;
    }
}
