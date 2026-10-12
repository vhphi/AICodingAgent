using AICodingAgent.Application.Abstractions;
using AICodingAgent.Domain.Agents;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AICodingAgent.Infrastructure.Codex
{
    public sealed class CodexCliService : ICodingExecutor
    {
        public async Task<CodingExecutionResult> ExecuteAsync(
            string workspacePath,
            string instruction,
            CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "codex",
                Arguments = BuildArguments(instruction),
                WorkingDirectory = workspacePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var output = new StringBuilder();
            var error = new StringBuilder();

            using (var process = new Process())
            {
                process.StartInfo = psi;

                process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        output.AppendLine(args.Data);
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        error.AppendLine(args.Data);
                    }
                };

                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(cancellationToken);

                return new CodingExecutionResult
                {
                    Success = process.ExitCode == 0,
                    ExitCode = process.ExitCode,
                    Output = output.ToString(),
                    Error = error.ToString()
                };
            }
        }

        private static string BuildArguments(string instruction)
        {
            return "exec " + Quote(instruction);
        }

        private static string Quote(string value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            return "\"" +
                   value.Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\r", "\\r")
                        .Replace("\n", "\\n") +
                   "\"";
        }
    }
}