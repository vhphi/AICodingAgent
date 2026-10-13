using AICodingAgent.Application.Abstractions;
using AICodingAgent.Application.Agents;
using AICodingAgent.Application.Workflows;
using AICodingAgent.Domain.Requirements;
using AICodingAgent.Domain.Workflows;
using AICodingAgent.Infrastructure;
using AICodingAgent.Infrastructure.Configuration;
using AICodingAgent.Infrastructure.Workspace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClient", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var options = new AgentOptions();
builder.Configuration.GetSection("Agent").Bind(options);

// default workspace is no longer supplied at startup.
// Each API request must provide `WorkspacePath`.
builder.Services.AddAICodingAgent(options);

var app = builder.Build();

app.UseCors("WebClient");

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
        State = session.State.ToString(),
        session.GitBranch,
        result
    });
});

app.MapPost("/api/ask", async (
    AskRequest request,
    ILanguageModel llm,
    ICodeSearch codeSearch,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request?.Prompt))
    {
        return Results.BadRequest(new { error = "Prompt is required." });
    }

    var systemPrompt = string.IsNullOrWhiteSpace(request.SystemPrompt)
        ? "You are a helpful assistant. Provide concise, accurate answers. If the question relates to code or the provided project workspace, include actionable guidance."
        : request.SystemPrompt;

    var userPrompt = request.Prompt ?? string.Empty;

    if (!string.IsNullOrWhiteSpace(request.WorkspacePath))
    {
        try
        {
            var context = await codeSearch.BuildContextAsync(
                request.WorkspacePath,
                userPrompt,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(context.ContextText))
            {
                userPrompt += "\n\nPROJECT CONTEXT:\n" + context.ContextText;
            }
        }
        catch (Exception ex)
        {
            // don't fail the whole request if context build fails; return answer without project context
            userPrompt += $"\n\n[Context build failed: {ex.Message}]";
        }
    }

    var response = await llm.CompleteAsync(
        new CompletionRequest(systemPrompt, userPrompt),
        cancellationToken);

    return Results.Ok(new
    {
        answer = response.Text,
        model = response.Model
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

public sealed class AskRequest
{
    /// <summary>
    /// Optional system prompt to guide the assistant; if omitted a sensible default is used.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// User question or instruction (required).
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>
    /// Optional workspace path. If provided the API will try to include project context in the prompt.
    /// </summary>
    public string? WorkspacePath { get; set; }
}