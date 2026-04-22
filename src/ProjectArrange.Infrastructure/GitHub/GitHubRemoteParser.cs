using ProjectArrange.Core.GitHub;

namespace ProjectArrange.Infrastructure.GitHub;

public static class GitHubRemoteParser
{
    public static bool TryParseRepo(string remoteUrl, out GitHubRepoRef repo)
    {
        repo = default!;
        if (string.IsNullOrWhiteSpace(remoteUrl)) return false;

        var url = remoteUrl.Trim();

        if (url.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
        {
            var part = url.Substring("git@github.com:".Length);
            return TryParseOwnerRepo(part, out repo);
        }

        if (url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            var path = uri.AbsolutePath.Trim('/');
            return TryParseOwnerRepo(path, out repo);
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var any))
        {
            if (!any.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return false;
            var path = any.AbsolutePath.Trim('/');
            return TryParseOwnerRepo(path, out repo);
        }

        return false;
    }

    private static bool TryParseOwnerRepo(string path, out GitHubRepoRef repo)
    {
        repo = default!;
        var part = path.Trim().Trim('/');
        if (part.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            part = part.Substring(0, part.Length - 4);
        }

        var pieces = part.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (pieces.Length < 2) return false;
        var owner = pieces[0];
        var name = pieces[1];
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(name)) return false;
        repo = new GitHubRepoRef(owner, name);
        return true;
    }
}
