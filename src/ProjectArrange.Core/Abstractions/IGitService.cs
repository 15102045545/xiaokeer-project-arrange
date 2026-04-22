using ProjectArrange.Core.Git;

namespace ProjectArrange.Core.Abstractions;

public interface IGitService
{
    Task<Result<bool>> IsGitRepositoryAsync(string path, CancellationToken cancellationToken = default);
    Task<Result<GitRepositoryStatus>> GetRepositoryStatusAsync(string path, CancellationToken cancellationToken = default);
}
