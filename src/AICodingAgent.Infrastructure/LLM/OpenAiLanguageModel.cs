using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AICodingAgent.Application.Abstractions;
using AICodingAgent.Infrastructure.Configuration;

namespace AICodingAgent.Infrastructure.LLM;

public sealed class OpenAiLanguageModel : ILanguageModel
{
    private readonly HttpClient _httpClient;
    private readonly AgentOptions _options;

    public OpenAiLanguageModel(HttpClient httpClient, AgentOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<CompletionResult> CompleteAsync(
        CompletionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "AICODINGAGENT__APIKEY is required when Provider=OpenAI.");
        }

        var endpoint = _options.BaseUrl.TrimEnd('/') + "/responses";

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            endpoint);

        httpRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var payload = new
        {
            model = _options.Model,
            input = new object[]
            {
                new
                {
                    role = "developer",
                    content = request.SystemPrompt
                },
                new
                {
                    role = "user",
                    content = request.UserPrompt
                }
            }
        };

        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(
            httpRequest,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OpenAI request failed: {(int)response.StatusCode} {response.ReasonPhrase}\n{body}");
        }

        using var document = JsonDocument.Parse(body);

        var outputText = TryGetOutputText(document.RootElement);

        if (string.IsNullOrWhiteSpace(outputText))
        {
            throw new InvalidOperationException(
                "The LLM response did not contain output text.");
        }

        return new CompletionResult(outputText, _options.Model);
    }

    private static string? TryGetOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var outputText) &&
            outputText.ValueKind == JsonValueKind.String)
        {
            return outputText.GetString();
        }

        if (!root.TryGetProperty("output", out var output) ||
            output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var parts = new List<string>();

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in content.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var text) &&
                    text.ValueKind == JsonValueKind.String)
                {
                    parts.Add(text.GetString() ?? string.Empty);
                }
            }
        }

        return parts.Count == 0 ? null : string.Join("\n", parts);
    }
}
