namespace ProjectArrange.Core.OpenSourceify;

public enum ExistingFilePolicy
{
    Skip,
    Overwrite,
    Append
}

public sealed record EnsureFileRule(
    string RelativePath,
    ExistingFilePolicy OnExisting,
    string ContentWhenMissing);

public sealed record OpenSourceifyRecipe(
    IReadOnlyList<EnsureFileRule> EnsureFiles,
    IReadOnlyList<string> GitignoreRules);

public sealed record OpenSourceifyAction(
    string Kind,
    string Target,
    string Summary);

public sealed record OpenSourceifyPlan(
    string RepoRoot,
    bool DryRun,
    IReadOnlyList<OpenSourceifyAction> Actions,
    IReadOnlyList<string> BadTrackedFiles);
