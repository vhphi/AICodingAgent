using AICodingAgent.Application.Abstractions;
using AICodingAgent.Application.Agents;
using AICodingAgent.Domain.Agents;
using AICodingAgent.Domain.Requirements;
using AICodingAgent.Domain.Workflows;

namespace AICodingAgent.Application.Workflows;

public sealed class SoftwareImplementationWorkflow
{
    private readonly RequirementAgent _requirementAgent;
    private readonly PlanningAgent _planningAgent;
    private readonly CodingAgent _codingAgent;
    private readonly TestingAgent _testingAgent;
    private readonly ReviewAgent _reviewAgent;
    private readonly ICodeSearch _codeSearch;
    private readonly IBuildRunner _buildRunner;
    private readonly ITestRunner _testRunner;
    private readonly IGitService _git;
    private readonly IAgentEventSink _events;

    public SoftwareImplementationWorkflow(
        RequirementAgent requirementAgent,
        PlanningAgent planningAgent,
        CodingAgent codingAgent,
        TestingAgent testingAgent,
        ReviewAgent reviewAgent,
        ICodeSearch codeSearch,
        IBuildRunner buildRunner,
        ITestRunner testRunner,
        IGitService git,
        IAgentEventSink events)
    {
        _requirementAgent = requirementAgent;
        _planningAgent = planningAgent;
        _codingAgent = codingAgent;
        _testingAgent = testingAgent;
        _reviewAgent = reviewAgent;
        _codeSearch = codeSearch;
        _buildRunner = buildRunner;
        _testRunner = testRunner;
        _git = git;
        _events = events;
    }

    public async Task<WorkflowResult> ExecuteAsync(
        AgentSession session,
        Requirement requirement,
        bool autoCommit,
        CancellationToken cancellationToken)
    {
        session.SetRequirement(requirement.Id);

        await Publish(session, "state", new { state = AgentState.LoadingRequirement.ToString() }, cancellationToken);
        session.ChangeState(AgentState.AnalyzingRequirement);

        var context = await _codeSearch.BuildContextAsync(
            session.WorkspacePath,
            requirement.Description,
            cancellationToken);

        await Publish(session, "context", new
        {
            files = context.RelevantFiles
        }, cancellationToken);

        var analysis = await _requirementAgent.AnalyzeAsync(
            requirement,
            context,
            cancellationToken);

        if (!analysis.IsReadyForImplementation)
        {
            session.ChangeState(AgentState.WaitingForClarification);
            await Publish(session, "clarification_required", analysis, cancellationToken);

            return new WorkflowResult
            {
                SessionId = session.Id,
                State = session.State,
                Analysis = analysis
            };
        }

        session.ChangeState(AgentState.Planning);

        var plan = await _planningAgent.CreatePlanAsync(
            requirement,
            context,
            cancellationToken);

        await Publish(session, "plan", plan, cancellationToken);

        session.ChangeState(AgentState.Implementing);

        var implementation = await _codingAgent.ImplementAsync(
            requirement,
            plan,
            context,
            cancellationToken);

        await Publish(session, "implementation", implementation, cancellationToken);

        session.ChangeState(AgentState.Building);

        var build = await _buildRunner.BuildAsync(
            session.WorkspacePath,
            cancellationToken);

        await Publish(session, "build", build, cancellationToken);

        if (!build.Success)
        {
            session.ChangeState(AgentState.Fixing);
            context = await _codeSearch.BuildContextAsync(
                session.WorkspacePath,
                requirement.Description + "\nBUILD FAILURE:\n" + build.Output,
                cancellationToken);

            await _codingAgent.FixAsync(
                build.Output,
                context,
                cancellationToken);

            build = await _buildRunner.BuildAsync(
                session.WorkspacePath,
                cancellationToken);

            await Publish(session, "build_retry", build, cancellationToken);

            if (!build.Success)
            {
                session.ChangeState(AgentState.Failed);
                return new WorkflowResult
                {
                    SessionId = session.Id,
                    State = session.State,
                    Plan = plan,
                    Implementation = implementation,
                    Build = build
                };
            }
        }

        session.ChangeState(AgentState.GeneratingTests);

        var tests = await _testingAgent.GenerateAsync(
            requirement,
            context,
            cancellationToken);

        await Publish(session, "tests_generated", tests, cancellationToken);

        session.ChangeState(AgentState.Testing);

        var testResult = await _testRunner.TestAsync(
            session.WorkspacePath,
            cancellationToken);

        await Publish(session, "tests", testResult, cancellationToken);

        if (!testResult.Success)
        {
            session.ChangeState(AgentState.Fixing);

            context = await _codeSearch.BuildContextAsync(
                session.WorkspacePath,
                requirement.Description + "\nTEST FAILURE:\n" + testResult.Output,
                cancellationToken);

            await _codingAgent.FixAsync(
                testResult.Output,
                context,
                cancellationToken);

            testResult = await _testRunner.TestAsync(
                session.WorkspacePath,
                cancellationToken);

            await Publish(session, "tests_retry", testResult, cancellationToken);

            if (!testResult.Success)
            {
                session.ChangeState(AgentState.Failed);
                return new WorkflowResult
                {
                    SessionId = session.Id,
                    State = session.State,
                    Plan = plan,
                    Implementation = implementation,
                    Build = build,
                    Tests = testResult
                };
            }
        }

        session.ChangeState(AgentState.Reviewing);

        var diff = await _git.GetDiffAsync(
            session.WorkspacePath,
            cancellationToken);

        var review = await _reviewAgent.ReviewAsync(
            requirement,
            diff.Text,
            testResult.Output,
            cancellationToken);

        await Publish(session, "review", review, cancellationToken);

        if (!review.Passed)
        {
            session.ChangeState(AgentState.Failed);

            return new WorkflowResult
            {
                SessionId = session.Id,
                State = session.State,
                Plan = plan,
                Implementation = implementation,
                Build = build,
                Tests = testResult,
                Review = review
            };
        }

        session.ChangeState(AgentState.ReadyForCommit);

        if (autoCommit)
        {
            var branch = "feature/" + SanitizeBranchName(
                requirement.Id + "-" + requirement.Title);

            await _git.CreateBranchAsync(
                session.WorkspacePath,
                branch,
                cancellationToken);

            session.SetGitBranch(branch);

            await _git.StageAllAsync(
                session.WorkspacePath,
                cancellationToken);

            var commitMessage = "feat: " + SanitizeCommitMessage(requirement.Title);

            await _git.CommitAsync(
                session.WorkspacePath,
                commitMessage,
                cancellationToken);

            session.ChangeState(AgentState.Committed);
        }

        return new WorkflowResult
        {
            SessionId = session.Id,
            State = session.State,
            Plan = plan,
            Implementation = implementation,
            Build = build,
            Tests = testResult,
            Review = review
        };
    }

    private async Task Publish(
        AgentSession session,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        await _events.PublishAsync(
            session.Id,
            eventName,
            payload,
            cancellationToken);
    }

    private static string SanitizeBranchName(string value)
    {
        var chars = value
            .ToLowerInvariant()
            .Select(x => char.IsLetterOrDigit(x) ? x : '-')
            .ToArray();

        return new string(chars).Trim('-');
    }

    private static string SanitizeCommitMessage(string value)
    {
        var text = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length > 60 ? text[..60] : text;
    }
}

public sealed class WorkflowResult
{
    public Guid SessionId { get; init; }
    public AgentState State { get; init; }
    public RequirementAnalysis? Analysis { get; init; }
    public ImplementationPlan? Plan { get; init; }
    public ImplementationResult? Implementation { get; init; }
    public BuildResult? Build { get; init; }
    public TestRunResult? Tests { get; init; }
    public CodeReviewResult? Review { get; init; }
}
