using AICodingAgent.Application.Abstractions;
using AICodingAgent.Application.Agents;
using AICodingAgent.Application.Workflows;
using AICodingAgent.Domain.Requirements;
using AICodingAgent.Domain.Workflows;
using AICodingAgent.Infrastructure;
using AICodingAgent.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

var options = new AgentOptions();
builder.Configuration.GetSection("Agent").Bind(options);

var defaultWorkspace = builder.Configuration["Agent:WorkspacePath"]
    ?? Directory.GetCurrentDirectory();

builder.Services.AddAICodingAgent(options, defaultWorkspace);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "AICodingAgent.Api",
    timeUtc = DateTime.UtcNow
}));

app.MapPost("/api/analyze", async (
    AnalyzeRequest request,
    IRequirementReader reader,
    ICodeSearch codeSearch,
    RequirementAgent agent,
    CancellationToken cancellationToken) =>
{
    var content = request.Content;

    if (string.IsNullOrWhiteSpace(content) &&
        !string.IsNullOrWhiteSpace(request.Source))
    {
        content = await reader.ReadAsync(
            request.Source,
            cancellationToken);
    }

    if (string.IsNullOrWhiteSpace(content))
    {
        return Results.BadRequest(new { error = "Requirement content/source is required." });
    }

    var requirement = new Requirement
    {
        Title = request.Title ?? "Requirement",
        Description = content,
        Source = new RequirementSource
        {
            Type = string.IsNullOrWhiteSpace(request.Source) ? "text" : "file-or-url",
            Location = request.Source ?? string.Empty,
            Content = content
        }
    };

    var context = await codeSearch.BuildContextAsync(
        request.WorkspacePath ?? Directory.GetCurrentDirectory(),
        content,
        cancellationToken);

    var result = await agent.AnalyzeAsync(
        requirement,
        context,
        cancellationToken);

    return Results.Ok(result);
});

app.MapPost("/api/run", async (
    RunRequest request,
    IRequirementReader reader,
    SoftwareImplementationWorkflow workflow,
    CancellationToken cancellationToken) =>
{
    var content = request.Content;

    if (string.IsNullOrWhiteSpace(content) &&
        !string.IsNullOrWhiteSpace(request.Source))
    {
        content = await reader.ReadAsync(
            request.Source,
            cancellationToken);
    }

    if (string.IsNullOrWhiteSpace(content))
    {
        return Results.BadRequest(new { error = "Requirement content/source is required." });
    }

    if (string.IsNullOrWhiteSpace(request.WorkspacePath))
    {
        return Results.BadRequest(new { error = "WorkspacePath is required." });
    }

    var session = new AgentSession(
        Guid.NewGuid(),
        request.Title ?? "AI Task",
        request.WorkspacePath);

    var requirement = new Requirement
    {
        Id = request.RequirementId ?? Guid.NewGuid().ToString("N"),
        Title = request.Title ?? "AI Task",
        Description = content,
        Source = new RequirementSource
        {
            Type = string.IsNullOrWhiteSpace(request.Source) ? "text" : "file-or-url",
            Location = request.Source ?? string.Empty,
            Content = content
        }
    };

    var result = await workflow.ExecuteAsync(
        session,
        requirement,
        request.AutoCommit,
        cancellationToken);

    return Results.Ok(new
    {
        session.Id,
        session.State,
        session.GitBranch,
        result
    });
});

app.Run();

public class AnalyzeRequest
{
    public string? Title { get; set; }
    public string? Source { get; set; }
    public string? Content { get; set; }
    public string? WorkspacePath { get; set; }
}

public sealed class RunRequest : AnalyzeRequest
{
    public string? RequirementId { get; set; }
    public bool AutoCommit { get; set; }
}
