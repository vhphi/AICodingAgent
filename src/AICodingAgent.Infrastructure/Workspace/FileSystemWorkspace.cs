using AICodingAgent.Application.Abstractions;

namespace AICodingAgent.Infrastructure.Workspace;

public sealed class FileSystemWorkspace : IWorkspace
{
    private readonly string _root;

    private static readonly string[] IgnoredDirectories =
    {
        ".git",
        ".vs",
        "bin",
        "obj",
        "node_modules"
    };

    private static readonly string[] AllowedExtensions =
    {
        ".cs", ".csproj", ".sln", ".json", ".xml", ".config",
        ".md", ".txt", ".ts", ".tsx", ".js", ".jsx", ".sql",
        ".yml", ".yaml", ".props", ".targets"
    };

    public FileSystemWorkspace(string root)
    {
        _root = NormalizeWorkspacePath(root);
        Directory.CreateDirectory(_root);
    }
    static string NormalizeWorkspacePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return File.Exists(fullPath)
            ? Path.GetDirectoryName(fullPath) ?? fullPath
            : fullPath;
    }
    public string ResolvePath(string relativePath)
    {
        var fullPath = Path.GetFullPath(
            Path.Combine(_root, relativePath));

        if (!fullPath.StartsWith(
            _root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, _root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Path escapes workspace: {relativePath}");
        }

        return fullPath;
    }

    public async Task<string> ReadFileAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        var path = ResolvePath(relativePath);
        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    public async Task WriteFileAsync(
        string relativePath,
        string content,
        CancellationToken cancellationToken)
    {
        var path = ResolvePath(relativePath);

        if (Path.GetExtension(path) is not null &&
            !AllowedExtensions.Contains(
                Path.GetExtension(path),
                StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"File type is not allowed: {relativePath}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await File.WriteAllTextAsync(
            path,
            content,
            cancellationToken);
    }

    public Task DeleteFileAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        var path = ResolvePath(relativePath);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> FindFilesAsync(
        CancellationToken cancellationToken)
    {
        var files = Directory
            .EnumerateFiles(_root, "*", SearchOption.AllDirectories)
            .Where(x => !IsIgnored(x))
            .Where(x => AllowedExtensions.Contains(
                Path.GetExtension(x),
                StringComparer.OrdinalIgnoreCase))
            .Select(x => Path.GetRelativePath(_root, x))
            .OrderBy(x => x)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    private bool IsIgnored(string path)
    {
        var relative = Path.GetRelativePath(_root, path);

        return relative
            .Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries)
            .Any(part => IgnoredDirectories.Contains(
                part,
                StringComparer.OrdinalIgnoreCase));
    }
}
