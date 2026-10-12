using AICodingAgent.Application.Abstractions;
using AICodingAgent.Infrastructure.Configuration;
using GitHub.Copilot;

namespace AICodingAgent.Infrastructure.LLM;

public sealed class GitHubCopilotLanguageModel : ILanguageModel
{
    private readonly AgentOptions _options;
    private readonly CopilotClient _client;

    public GitHubCopilotLanguageModel(CopilotClient client, AgentOptions options)
    {
        _options = options;
        this._client = client;
    }

    public async Task<CompletionResult> CompleteAsync(
        CompletionRequest request,
        CancellationToken cancellationToken)
    {
        var client = new CopilotClient();
        await client.StartAsync();

        try
        {
            var session = await client.CreateSessionAsync(
                new SessionConfig
                {
                    Model = _options.Model
                },
                cancellationToken);

            var prompt = BuildPrompt(request);

            var response = await session.SendAndWaitAsync(prompt,
                cancellationToken:cancellationToken);

            var outputText = response?.Data?.Content;

            if (string.IsNullOrWhiteSpace(outputText))
            {
                throw new InvalidOperationException(
                    "The GitHub Copilot response did not contain output text.");
            }

            return new CompletionResult(
                outputText,
                _options.Model);
        }
        finally
        {
            await client.StopAsync();
        }
    }

    private static string BuildPrompt(CompletionRequest request)
    {
        return $"""
            <system>
            {request.SystemPrompt}
            </system>

            <user>
            {request.UserPrompt}
            </user>
            """;
    }
}