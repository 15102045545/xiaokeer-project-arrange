namespace ProjectArrange.Core.Abstractions;

public sealed record GitHubUser(string Login, long Id);

public interface IGitHubAccountService
{
    Task<Result<GitHubUser>> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}
