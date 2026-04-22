using Octokit;
using ProjectArrange.Core;
using ProjectArrange.Core.Abstractions;

namespace ProjectArrange.Infrastructure.GitHub;

public sealed class GitHubClientFactory(IAppSecretsStore secretsStore)
{
    public async Task<Result<GitHubClient>> CreateAsync(CancellationToken cancellationToken = default)
    {
        var tokenRes = await secretsStore.GetGitHubTokenAsync(cancellationToken);
        if (!tokenRes.IsSuccess) return Result<GitHubClient>.Fail(tokenRes.Error!);

        var token = tokenRes.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result<GitHubClient>.Fail("GitHub token not configured.");
        }

        var client = new GitHubClient(new ProductHeaderValue("ProjectArrange"))
        {
            Credentials = new Credentials(token)
        };

        return Result<GitHubClient>.Ok(client);
    }
}
