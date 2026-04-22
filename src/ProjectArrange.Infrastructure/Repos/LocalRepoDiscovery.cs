namespace ProjectArrange.Infrastructure.Repos;

public static class LocalRepoDiscovery
{
    public static IReadOnlyList<string> DiscoverGitRepositories(
        IEnumerable<string> roots,
        int maxDepth = 6,
        int maxRepos = 500)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            var normalized = NormalizeDir(root.Trim());
            if (normalized is null) continue;
            Walk(normalized, depth: 0);
            if (result.Count >= maxRepos) break;
        }

        return result;

        void Walk(string dir, int depth)
        {
            if (result.Count >= maxRepos) return;
            if (depth > maxDepth) return;

            if (!seen.Add(dir)) return;

            if (Directory.Exists(Path.Combine(dir, ".git")))
            {
                result.Add(dir);
                return;
            }

            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(dir);
            }
            catch
            {
                return;
            }

            foreach (var child in children)
            {
                var name = Path.GetFileName(child);
                if (name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals(".git", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals("bin", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals("obj", StringComparison.OrdinalIgnoreCase)) continue;

                Walk(child, depth + 1);
                if (result.Count >= maxRepos) return;
            }
        }
    }

    private static string? NormalizeDir(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            if (!Directory.Exists(full)) return null;
            return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return null;
        }
    }
}

