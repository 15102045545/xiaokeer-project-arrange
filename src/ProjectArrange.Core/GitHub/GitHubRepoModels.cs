namespace ProjectArrange.Core.GitHub;

public sealed record GitHubRepoRef(string Owner, string Name)
{
    public string FullName => $"{Owner}/{Name}";
}

public sealed record GitHubRepoUpdate(
    string? Description = null,
    bool? IsPrivate = null,
    IReadOnlyList<string>? Topics = null);
