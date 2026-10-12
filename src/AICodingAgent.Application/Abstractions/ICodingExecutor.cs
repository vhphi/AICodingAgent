using AICodingAgent.Domain.Agents;

namespace AICodingAgent.Application.Abstractions
{
    public interface ICodingExecutor
    {
        Task<CodingExecutionResult> ExecuteAsync(
            string workspacePath,
            string instruction,
            CancellationToken cancellationToken);
    }

    public sealed class CodingExecutionResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
    }
}