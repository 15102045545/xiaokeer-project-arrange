namespace ProjectArrange.Core.Repos;

public sealed record RepoRegistryEntry(
    string Path,
    string? OriginUrl,
    string? GitHubFullName,
    DateTimeOffset AddedUtc,
    DateTimeOffset LastSeenUtc);

public sealed record RepoGitSnapshot(
    string Path,
    string Branch,
    int Ahead,
    int Behind,
    bool IsClean,
    int StagedChanges,
    int UnstagedChanges,
    int UntrackedFiles,
    DateTimeOffset Utc);

public sealed record RepoSecretScanSnapshot(
    string Path,
    bool ToolAvailable,
    int FindingsCount,
    string RawOutput,
    DateTimeOffset Utc);

public sealed record RepoRegistrySnapshot(
    IReadOnlyList<RepoRegistryEntry> Repos,
    DateTimeOffset Utc);
