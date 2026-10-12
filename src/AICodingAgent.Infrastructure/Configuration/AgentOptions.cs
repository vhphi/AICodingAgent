namespace AICodingAgent.Infrastructure.Configuration;

public sealed class AgentOptions
{
    public string Provider { get; set; } = "Mock";
    public string Model { get; set; } = "gpt-5.6-luna";
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public int MaxFileSizeKb { get; set; } = 256;
    public int MaxContextFiles { get; set; } = 20;
}
