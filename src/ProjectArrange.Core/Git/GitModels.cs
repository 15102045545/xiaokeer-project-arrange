namespace ProjectArrange.Core.Git;

public sealed record GitRemote(string Name, string FetchUrl, string PushUrl);

public sealed record GitBranchStatus(
    string Branch,
    int Ahead,
    int Behind,
    bool HasUpstream,
    bool IsDetachedHead);

public sealed record GitWorkingTreeStatus(
    bool IsClean,
    int StagedChanges,
    int UnstagedChanges,
    int UntrackedFiles);

public sealed record GitRepositoryStatus(
    string RootPath,
    GitBranchStatus Branch,
    GitWorkingTreeStatus WorkingTree,
    IReadOnlyList<GitRemote> Remotes);
