using AICodingAgent.Domain.Workflows;
using Xunit;

namespace AICodingAgent.Application.Tests;

public sealed class AgentStateTests
{
    [Fact]
    public void NewSession_StartsInCreatedState()
    {
        var session = new AgentSession(
            Guid.NewGuid(),
            "test",
            Directory.GetCurrentDirectory());

        Assert.Equal(
            AgentState.Created,
            session.State);
    }

    [Fact]
    public void ChangeState_UpdatesState()
    {
        var session = new AgentSession(
            Guid.NewGuid(),
            "test",
            Directory.GetCurrentDirectory());

        session.ChangeState(AgentState.Planning);

        Assert.Equal(
            AgentState.Planning,
            session.State);
    }
}
