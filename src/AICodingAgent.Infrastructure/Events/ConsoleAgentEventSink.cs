using System.Text.Json;
using AICodingAgent.Application.Abstractions;

namespace AICodingAgent.Infrastructure.Events;

public sealed class ConsoleAgentEventSink : IAgentEventSink
{
    public Task PublishAsync(
        Guid sessionId,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss}] [{eventName}] " +
            JsonSerializer.Serialize(payload));

        return Task.CompletedTask;
    }
}
