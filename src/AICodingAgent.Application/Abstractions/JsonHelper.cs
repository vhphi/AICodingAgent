using System.Text.Json;

namespace AICodingAgent.Application.Abstractions;

public static class JsonHelper
{
    public static string StripMarkdownFence(string value)
    {
        var text = value.Trim();

        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = text.IndexOf('\n');
            var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);

            if (firstNewLine >= 0 && lastFence > firstNewLine)
            {
                return text[(firstNewLine + 1)..lastFence].Trim();
            }
        }

        return text;
    }

    public static T Deserialize<T>(string value)
    {
        var json = StripMarkdownFence(value);

        return JsonSerializer.Deserialize<T>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("LLM returned empty JSON.");
    }
}
