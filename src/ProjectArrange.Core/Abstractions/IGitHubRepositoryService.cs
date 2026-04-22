using ProjectArrange.Core.GitHub;

namespace ProjectArrange.Core.Abstractions;

public interface IGitHubRepositoryService
{
    Task<Result> UpdateRepositoryAsync(GitHubRepoRef repo, GitHubRepoUpdate update, CancellationToken cancellationToken = default);
}
