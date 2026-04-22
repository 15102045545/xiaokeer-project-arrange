using ProjectArrange.Core.Git;

namespace ProjectArrange.Infrastructure.Git;

public static class GitStatusParser
{
    public static (GitBranchStatus Branch, GitWorkingTreeStatus WorkingTree) ParsePorcelainV1(string statusOutput)
    {
        var lines = statusOutput
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .ToArray();

        string branch = "unknown";
        var ahead = 0;
        var behind = 0;
        var hasUpstream = false;
        var detached = false;

        var staged = 0;
        var unstaged = 0;
        var untracked = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith("## "))
            {
                var header = line.Substring(3);

                if (header.StartsWith("HEAD ("))
                {
                    detached = true;
                    branch = "HEAD";
                    continue;
                }

                var parts = header.Split(new[] { "...", " " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0) branch = parts[0];

                hasUpstream = header.Contains("...");

                var brStart = header.IndexOf('[');
                var brEnd = header.IndexOf(']');
                if (brStart >= 0 && brEnd > brStart)
                {
                    var bracket = header.Substring(brStart + 1, brEnd - brStart - 1);
                    var items = bracket.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    foreach (var item in items)
                    {
                        if (item.StartsWith("ahead "))
                        {
                            int.TryParse(item.Substring("ahead ".Length), out ahead);
                        }
                        else if (item.StartsWith("behind "))
                        {
                            int.TryParse(item.Substring("behind ".Length), out behind);
                        }
                    }
                }
            }
            else if (line.StartsWith("?? "))
            {
                untracked++;
            }
            else if (line.Length >= 2)
            {
                var x = line[0];
                var y = line[1];
                if (x != ' ' && x != '?') staged++;
                if (y != ' ') unstaged++;
            }
        }

        var workingTree = new GitWorkingTreeStatus(
            IsClean: staged == 0 && unstaged == 0 && untracked == 0,
            StagedChanges: staged,
            UnstagedChanges: unstaged,
            UntrackedFiles: untracked);

        var branchStatus = new GitBranchStatus(
            Branch: branch,
            Ahead: ahead,
            Behind: behind,
            HasUpstream: hasUpstream,
            IsDetachedHead: detached);

        return (branchStatus, workingTree);
    }
}
