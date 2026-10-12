// Suppress GHCP001 for PermissionDecision usage
#pragma warning disable GHCP001 // 'GitHub.Copilot.Rpc.PermissionDecision' is for evaluation purposes only and is subject to change or removal in future updates.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AICodingAgent.Application.Abstractions;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;

namespace AICodingAgent.Infrastructure.Copilot
{
    public sealed class CopilotCodingExecutor : ICodingExecutor
    {
        public async Task<CodingExecutionResult> ExecuteAsync(
            string workspacePath,
            string instruction,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(workspacePath))
            {
                throw new ArgumentException(
                    "Workspace path is required.",
                    nameof(workspacePath));
            }

            if (string.IsNullOrWhiteSpace(instruction))
            {
                throw new ArgumentException(
                    "Instruction is required.",
                    nameof(instruction));
            }

            if (!Directory.Exists(workspacePath))
            {
                throw new DirectoryNotFoundException(
                    "Workspace directory was not found: " + workspacePath);
            }

            try
            {
                await using (var client = new CopilotClient())
                {
                    await using (var session =
                        await client.CreateSessionAsync(
                            new SessionConfig
                            {
                                WorkingDirectory = Path.GetFullPath(workspacePath),

                                // "auto" lets Copilot select an available model.
                                // This is useful when using Copilot Free.
                                Model = "auto",

                                OnPermissionRequest =
                                    (request, invocation) =>
                                        Task.FromResult(
                                            PermissionDecision.ApproveOnce())
                            },
                            cancellationToken))
                    {
                        var response =
                            await session.SendAndWaitAsync(
                               instruction,
                                cancellationToken: cancellationToken);

                        var content = response != null &&
                                      response.Data != null
                            ? response.Data.Content
                            : string.Empty;

                        return new CodingExecutionResult
                        {
                            Success = true,
                            ExitCode = 0,
                            Output = content ?? string.Empty,
                            Error = string.Empty
                        };
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return new CodingExecutionResult
                {
                    Success = false,
                    ExitCode = -1,
                    Output = string.Empty,
                    Error = "Copilot execution was cancelled."
                };
            }
            catch (Exception ex)
            {
                return new CodingExecutionResult
                {
                    Success = false,
                    ExitCode = -1,
                    Output = string.Empty,
                    Error = ex.ToString()
                };
            }
        }
    }
}

#pragma warning restore GHCP001 // 'GitHub.Copilot.Rpc.PermissionDecision' is for evaluation purposes only and is subject to change or removal in future updates.