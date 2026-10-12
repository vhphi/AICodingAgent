using System.Text.Json;
using AICodingAgent.Application.Abstractions;

namespace AICodingAgent.Infrastructure.LLM;

public sealed class MockLanguageModel : ILanguageModel
{
    public Task<CompletionResult> CompleteAsync(
        CompletionRequest request,
        CancellationToken cancellationToken)
    {
        var system = request.SystemPrompt;

        if (system.Contains("senior software analyst", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new CompletionResult("""
            {
              "isReadyForImplementation": true,
              "questions": [],
              "assumptions": ["Use the existing project patterns."],
              "risks": []
            }
            """, "mock"));
        }

        if (system.Contains("solution architect", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new CompletionResult("""
            {
              "steps": [],
              "filesToCreate": [],
              "filesToModify": [],
              "filesToDelete": [],
              "risks": ["Mock provider does not generate implementation plans."]
            }
            """, "mock"));
        }

        if (system.Contains("code reviewer", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new CompletionResult("""
            {
              "findings": []
            }
            """, "mock"));
        }

        return Task.FromResult(new CompletionResult(
            JsonSerializer.Serialize(new
            {
                summary = "Mock provider: no source changes were generated. Configure OpenAI for real implementation.",
                changes = Array.Empty<object>()
            }),
            "mock"));
    }
}
