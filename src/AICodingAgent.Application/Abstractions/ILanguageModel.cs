using System.Text.Json;

namespace AICodingAgent.Application.Abstractions;

public sealed record CompletionRequest(string SystemPrompt, string UserPrompt);

public sealed record CompletionResult(string Text, string? Model = null);

public interface ILanguageModel
{
    Task<CompletionResult> CompleteAsync(
        CompletionRequest request,
        CancellationToken cancellationToken);
}
