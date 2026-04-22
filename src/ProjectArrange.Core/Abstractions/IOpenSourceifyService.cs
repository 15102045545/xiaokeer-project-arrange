using ProjectArrange.Core.OpenSourceify;

namespace ProjectArrange.Core.Abstractions;

public interface IOpenSourceifyService
{
    Task<Result<OpenSourceifyPlan>> BuildPlanAsync(string repoPath, OpenSourceifyRecipe recipe, bool dryRun, CancellationToken cancellationToken = default);
}
