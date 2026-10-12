using System.Text;
using AICodingAgent.Application.Abstractions;
using AICodingAgent.Domain.Agents;

namespace AICodingAgent.Infrastructure.CodeSearch;

public sealed class SimpleCodeSearch : ICodeSearch
{
    private readonly IWorkspace _workspace;

    public SimpleCodeSearch(IWorkspace workspace)
    {
        _workspace = workspace;
    }

    public async Task<IReadOnlyList<string>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var results = new List<string>();

        foreach (var file in await _workspace.FindFilesAsync(cancellationToken))
        {
            var extension = Path.GetExtension(file);

            if (string.IsNullOrEmpty(extension))
            {
                continue;
            }

            string text;

            try
            {
                text = await _workspace.ReadFileAsync(file, cancellationToken);
            }
            catch
            {
                continue;
            }

            if (text.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(file);
            }
        }

        return results;
    }

    public async Task<ProjectContext> BuildContextAsync(
        string workspacePath,
        string requirementText,
        CancellationToken cancellationToken)
    {
        var files = await _workspace.FindFilesAsync(cancellationToken);

        var keywords = requirementText
            .Split(new[] { ' ', '\r', '\n', '\t', '.', ',', ':', ';', '(', ')' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(25)
            .ToArray();

        var scored = new List<(string File, int Score, string Text)>();

        foreach (var file in files)
        {
            string text;

            try
            {
                text = await _workspace.ReadFileAsync(file, cancellationToken);
            }
            catch
            {
                continue;
            }

            var score = keywords.Count(k =>
                file.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                text.Contains(k, StringComparison.OrdinalIgnoreCase));

            if (score > 0)
            {
                scored.Add((file, score, text));
            }
        }

        var relevant = scored
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.File)
            .Take(20)
            .ToList();

        var builder = new StringBuilder();

        foreach (var item in relevant)
        {
            var text = item.Text.Length > 12000
                ? item.Text[..12000]
                : item.Text;

            builder.AppendLine($"===== {item.File} =====");
            builder.AppendLine(text);
            builder.AppendLine();
        }

        return new ProjectContext
        {
            WorkspacePath = workspacePath,
            Files = files,
            RelevantFiles = relevant.Select(x => x.File).ToList(),
            ContextText = builder.ToString()
        };
    }
}
