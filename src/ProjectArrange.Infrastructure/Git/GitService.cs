using ProjectArrange.Core;
using ProjectArrange.Core.Abstractions;
using ProjectArrange.Core.Git;
using ProjectArrange.Infrastructure.Process;

namespace ProjectArrange.Infrastructure.Git;

public sealed class GitService(IProcessRunner runner, IToolLocator tools) : IGitService
{
    public async Task<Result<bool>> IsGitRepositoryAsync(string path, CancellationToken cancellationToken = default)
    {
        var spec = new ProcessSpec(
            FileName: tools.Resolve("git"),
            Arguments: "rev-parse --is-inside-work-tree",
            WorkingDirectory: path,
            TimeoutMilliseconds: 10_000);

        var res = await runner.RunAsync(spec, cancellationToken);
        if (res.TimedOut) return Result<bool>.Fail("git rev-parse timed out.");
        if (res.ExitCode != 0) return Result<bool>.Ok(false);

        return Result<bool>.Ok(res.StdOut.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Result<GitRepositoryStatus>> GetRepositoryStatusAsync(string path, CancellationToken cancellationToken = default)
    {
        var rootRes = await runner.RunAsync(
            new ProcessSpec(tools.Resolve("git"), "rev-parse --show-toplevel", path, TimeoutMilliseconds: 10_000),
            cancellationToken);

        if (rootRes.TimedOut) return Result<GitRepositoryStatus>.Fail("git rev-parse --show-toplevel timed out.");
        if (rootRes.ExitCode != 0) return Result<GitRepositoryStatus>.Fail(string.IsNullOrWhiteSpace(rootRes.StdErr) ? rootRes.StdOut : rootRes.StdErr);

        var root = rootRes.StdOut.Trim();

        var statusRes = await runner.RunAsync(
            new ProcessSpec(tools.Resolve("git"), "status --porcelain=v1 -b", root, TimeoutMilliseconds: 30_000),
            cancellationToken);

        if (statusRes.TimedOut) return Result<GitRepositoryStatus>.Fail("git status timed out.");
        if (statusRes.ExitCode != 0) return Result<GitRepositoryStatus>.Fail(statusRes.CombinedOutput);

        var (branch, workingTree) = GitStatusParser.ParsePorcelainV1(statusRes.StdOut);

        var remotesRes = await runner.RunAsync(
            new ProcessSpec(tools.Resolve("git"), "remote -v", root, TimeoutMilliseconds: 10_000),
            cancellationToken);

        var remotes = remotesRes.ExitCode == 0
            ? ParseRemotes(remotesRes.StdOut)
            : Array.Empty<GitRemote>();

        return Result<GitRepositoryStatus>.Ok(new GitRepositoryStatus(root, branch, workingTree, remotes));
    }

    private static IReadOnlyList<GitRemote> ParseRemotes(string output)
    {
        var fetch = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var push = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;

            var name = parts[0];
            var url = parts[1];
            var type = parts[2].Trim('(', ')');

            if (type.Equals("fetch", StringComparison.OrdinalIgnoreCase))
                fetch[name] = url;
            else if (type.Equals("push", StringComparison.OrdinalIgnoreCase))
                push[name] = url;
        }

        return fetch.Keys
            .Union(push.Keys, StringComparer.OrdinalIgnoreCase)
            .Select(n => new GitRemote(n, fetch.GetValueOrDefault(n, string.Empty), push.GetValueOrDefault(n, string.Empty)))
            .ToArray();
    }
}
