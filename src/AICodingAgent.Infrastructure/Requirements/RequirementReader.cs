using AICodingAgent.Application.Abstractions;

namespace AICodingAgent.Infrastructure.Requirements;

public sealed class RequirementReader : IRequirementReader
{
    private readonly HttpClient _httpClient;

    public RequirementReader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> ReadAsync(
        string source,
        CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return await _httpClient.GetStringAsync(uri, cancellationToken);
        }

        if (!File.Exists(source))
        {
            throw new FileNotFoundException(
                "Requirement file was not found.",
                source);
        }

        return await File.ReadAllTextAsync(
            source,
            cancellationToken);
    }
}
