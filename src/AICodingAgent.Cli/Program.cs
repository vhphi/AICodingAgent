using AICodingAgent.Application.Workflows;
using AICodingAgent.Domain.Requirements;
using AICodingAgent.Domain.Workflows;
using AICodingAgent.Infrastructure;
using AICodingAgent.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

// For debugging, you can run the CLI with the following command:
// dotnet run --project src/AICodingAgent.Cli -- run --workspace ./sample --requirement ./requirements/example.md
args = [ "run", "--workspace", "C:\\Users\\PhiVo\\Downloads\\AICodingAgent\\AICodingAgent\\sample", "--requirement", "C:\\Users\\PhiVo\\Downloads\\AICodingAgent\\AICodingAgent\\requirements\\example.md"];
var argsList = args.ToList();

if (argsList.Count == 0 || argsList.Contains("--help"))
{
    PrintHelp();
    return;
}

var command = argsList[0];

if (!string.Equals(command, "run", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Unknown command.");
    PrintHelp();
    return;
}

var workspace = GetOption(argsList, "--workspace")
    ?? Directory.GetCurrentDirectory();

var requirementFile = GetOption(argsList, "--requirement");

if (string.IsNullOrWhiteSpace(requirementFile))
{
    Console.Error.WriteLine("--requirement is required.");
    return;
}

var provider = Environment.GetEnvironmentVariable("AICODINGAGENT__PROVIDER")
    ?? "Mock";

var model = Environment.GetEnvironmentVariable("AICODINGAGENT__MODEL")
    ?? "gpt-5.6-luna";

var apiKey = Environment.GetEnvironmentVariable("AICODINGAGENT__APIKEY")
    ?? string.Empty;

var baseUrl = Environment.GetEnvironmentVariable("AICODINGAGENT__BASEURL")
    ?? "https://api.openai.com/v1";

var options = new AgentOptions
{
    Provider = provider,
    Model = model,
    ApiKey = apiKey,
    BaseUrl = baseUrl
};

var services = new ServiceCollection();

services.AddAICodingAgent(options, workspace);

using var serviceProvider = services.BuildServiceProvider();

var reader = serviceProvider.GetRequiredService<AICodingAgent.Application.Abstractions.IRequirementReader>();
var workflow = serviceProvider.GetRequiredService<SoftwareImplementationWorkflow>();

var requirementText = await reader.ReadAsync(
    requirementFile,
    CancellationToken.None);

var title = Path.GetFileNameWithoutExtension(requirementFile);

var session = new AgentSession(
    Guid.NewGuid(),
    title,
    Path.GetFullPath(workspace));

var requirement = new Requirement
{
    Id = title,
    Title = title,
    Description = requirementText,
    Source = new RequirementSource
    {
        Type = "file",
        Location = requirementFile,
        Content = requirementText
    }
};

var autoCommit = argsList.Contains(
    "--auto-commit",
    StringComparer.OrdinalIgnoreCase);

var result = await workflow.ExecuteAsync(
    session,
    requirement,
    autoCommit,
    CancellationToken.None);

Console.WriteLine();
Console.WriteLine("====================================");
Console.WriteLine($"Session : {session.Id}");
Console.WriteLine($"State   : {session.State}");
Console.WriteLine($"Branch  : {session.GitBranch ?? "(not created)"}");
Console.WriteLine("====================================");

static string? GetOption(
    IReadOnlyList<string> args,
    string name)
{
    var index = -1;
    for (var i = 0; i < args.Count; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            index = i;
            break;
        }
    }

    if (index < 0 || index + 1 >= args.Count)
    {
        return null;
    }

    return args[index + 1];
}

static void PrintHelp()
{
    Console.WriteLine("""
    AICodingAgent CLI

    Usage:
      dotnet run --project src/AICodingAgent.Cli -- run \
        --workspace <path> \
        --requirement <file> \
        [--auto-commit]

    Environment:
      AICODINGAGENT__PROVIDER=Mock|OpenAI
      AICODINGAGENT__MODEL=gpt-5.6-luna
      AICODINGAGENT__APIKEY=<api-key>
      AICODINGAGENT__BASEURL=https://api.openai.com/v1

    Examples:
      dotnet run --project src/AICodingAgent.Cli -- run \
        --workspace ./sample \
        --requirement ./requirements/example.md

      AICODINGAGENT__PROVIDER=OpenAI \
      AICODINGAGENT__APIKEY=... \
      dotnet run --project src/AICodingAgent.Cli -- run \
        --workspace ./sample \
        --requirement ./requirements/example.md
    """);
}
