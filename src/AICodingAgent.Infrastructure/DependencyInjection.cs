using AICodingAgent.Application.Abstractions;
using AICodingAgent.Application.Agents;
using AICodingAgent.Application.Workflows;
using AICodingAgent.Infrastructure.CodeSearch;
using AICodingAgent.Infrastructure.Codex;
using AICodingAgent.Infrastructure.Configuration;
using AICodingAgent.Infrastructure.Copilot;
using AICodingAgent.Infrastructure.Events;
using AICodingAgent.Infrastructure.Execution;
using AICodingAgent.Infrastructure.Git;
using AICodingAgent.Infrastructure.LLM;
using AICodingAgent.Infrastructure.Requirements;
using AICodingAgent.Infrastructure.Workspace;
using GitHub.Copilot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AICodingAgent.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAICodingAgent(
        this IServiceCollection services,
        AgentOptions options,
        string workspacePath)
    {
        services.AddSingleton(options);

        services.AddHttpClient();

        services.AddSingleton<IWorkspace>(
            new FileSystemWorkspace(workspacePath));

        services.AddSingleton<ProcessRunner>();
        services.AddSingleton<ICodeSearch, SimpleCodeSearch>();
        services.AddSingleton<IBuildRunner, DotNetBuildRunner>();
        services.AddSingleton<ITestRunner, DotNetTestRunner>();
        services.AddSingleton<IGitService, GitCliService>();
        services.AddSingleton<IRequirementReader, RequirementReader>();
        services.AddSingleton<IAgentEventSink, ConsoleAgentEventSink>();
        services.AddSingleton<CopilotClient, CopilotClient>();
        services.AddSingleton<ICodingExecutor, CodexCliService>();
        //services.AddSingleton<ICodingExecutor, CopilotCodingExecutor>();

        if (string.Equals(options.Provider, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ILanguageModel>(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                return new OpenAiLanguageModel(
                    factory.CreateClient(),
                    options);
            });
        }
        if (string.Equals(options.Provider, "GitHubCopilot", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ILanguageModel>(sp =>
            {
                var factory = sp.GetRequiredService<CopilotClient>();
                return new GitHubCopilotLanguageModel(
                    factory,
                    options);
            });
        }
        else
        {
            services.AddSingleton<ILanguageModel, MockLanguageModel>();
        }

        services.AddSingleton<RequirementAgent>();
        services.AddSingleton<PlanningAgent>();
        services.AddSingleton<CodingAgent>();
        services.AddSingleton<TestingAgent>();
        services.AddSingleton<ReviewAgent>();
        services.AddSingleton<SoftwareImplementationWorkflow>();

        return services;
    }
}
