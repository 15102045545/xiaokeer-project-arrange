using ProjectArrange.Core;
using ProjectArrange.Core.Abstractions;
using ProjectArrange.Core.OpenSourceify;
using ProjectArrange.Infrastructure.Process;

namespace ProjectArrange.Infrastructure.OpenSourceify;

public sealed class OpenSourceifyService(IProcessRunner runner, IToolLocator tools) : IOpenSourceifyService
{
    public async Task<Result<OpenSourceifyPlan>> BuildPlanAsync(
        string repoPath,
        OpenSourceifyRecipe recipe,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        var rootRes = await runner.RunAsync(
            new ProcessSpec(tools.Resolve("git"), "rev-parse --show-toplevel", repoPath, TimeoutMilliseconds: 10_000),
            cancellationToken);

        if (rootRes.TimedOut) return Result<OpenSourceifyPlan>.Fail("git rev-parse timed out.");
        if (rootRes.ExitCode != 0) return Result<OpenSourceifyPlan>.Fail(rootRes.CombinedOutput);

        var root = rootRes.StdOut.Trim();
        var actions = new List<OpenSourceifyAction>();

        foreach (var f in recipe.EnsureFiles)
        {
            var target = Path.Combine(root, f.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(target))
            {
                actions.Add(new OpenSourceifyAction("EnsureFile", f.RelativePath, "Create"));
            }
            else
            {
                actions.Add(new OpenSourceifyAction("EnsureFile", f.RelativePath, f.OnExisting.ToString()));
            }
        }

        var gitignorePath = Path.Combine(root, ".gitignore");
        var existingGitignore = File.Exists(gitignorePath) ? await File.ReadAllTextAsync(gitignorePath, cancellationToken) : "";
        var missingRules = recipe.GitignoreRules
            .Where(r => !ContainsLine(existingGitignore, r))
            .ToArray();
        if (missingRules.Length > 0)
        {
            actions.Add(new OpenSourceifyAction("EnsureGitignore", ".gitignore", $"Add {missingRules.Length} rules"));
        }

        var badTracked = await DetectBadTrackedFilesAsync(root, missingRules, cancellationToken);
        if (badTracked.IsSuccess && badTracked.Value!.Count > 0)
        {
            actions.Add(new OpenSourceifyAction("DetectBadTrackedFiles", "git", $"BadTracked={badTracked.Value.Count}"));
            foreach (var p in badTracked.Value) actions.Add(new OpenSourceifyAction("WouldGitRmCached", p, "git rm --cached"));
        }
        else
        {
            actions.Add(new OpenSourceifyAction("DetectBadTrackedFiles", "git", "BadTracked=0"));
        }

        return Result<OpenSourceifyPlan>.Ok(new OpenSourceifyPlan(
            RepoRoot: root,
            DryRun: dryRun,
            Actions: actions,
            BadTrackedFiles: badTracked.IsSuccess ? badTracked.Value! : Array.Empty<string>()));
    }

    private async Task<Result<IReadOnlyList<string>>> DetectBadTrackedFilesAsync(
        string repoRoot,
        IReadOnlyList<string> proposedGitignoreRules,
        CancellationToken cancellationToken)
    {
        var trackedRes = await runner.RunAsync(
            new ProcessSpec(tools.Resolve("git"), "ls-files -z", repoRoot, TimeoutMilliseconds: 30_000),
            cancellationToken);
        if (trackedRes.TimedOut) return Result<IReadOnlyList<string>>.Fail("git ls-files timed out.");
        if (trackedRes.ExitCode != 0) return Result<IReadOnlyList<string>>.Fail(trackedRes.CombinedOutput);

        var tracked = trackedRes.StdOut.Split('\0', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tracked.Length == 0) return Result<IReadOnlyList<string>>.Ok(Array.Empty<string>());

        var stdin = string.Join('\0', tracked) + "\0";
        var excludeFile = "";
        try
        {
            if (proposedGitignoreRules.Count > 0)
            {
                excludeFile = Path.Combine(Path.GetTempPath(), $"projectarrange-gitignore-{Guid.NewGuid():N}.txt");
                await File.WriteAllLinesAsync(excludeFile, proposedGitignoreRules, cancellationToken);
            }

            var args = "check-ignore -z --stdin --exclude-standard";
            if (!string.IsNullOrWhiteSpace(excludeFile))
            {
                args += $" --exclude-from \"{excludeFile}\"";
            }

            var res = await runner.RunAsync(
                new ProcessSpec(
                    tools.Resolve("git"),
                    args,
                    repoRoot,
                    TimeoutMilliseconds: 30_000,
                    StdInText: stdin),
                cancellationToken);

            if (res.TimedOut) return Result<IReadOnlyList<string>>.Fail("git check-ignore timed out.");
            if (res.ExitCode is not (0 or 1)) return Result<IReadOnlyList<string>>.Fail(res.CombinedOutput);

            var ignored = res.StdOut.Split('\0', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return Result<IReadOnlyList<string>>.Ok(ignored);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<string>>.Fail(ex.Message);
        }
        finally
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(excludeFile) && File.Exists(excludeFile)) File.Delete(excludeFile);
            }
            catch
            {
            }
        }
    }

    private static bool ContainsLine(string text, string line)
    {
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return lines.Contains(line.Trim());
    }
}

