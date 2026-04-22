namespace ProjectArrange.Core.Abstractions;

public interface IAppSecretsStore
{
    Task<Result> SaveGitHubTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<Result<string?>> GetGitHubTokenAsync(CancellationToken cancellationToken = default);
    Task<Result> ClearGitHubTokenAsync(CancellationToken cancellationToken = default);
}
