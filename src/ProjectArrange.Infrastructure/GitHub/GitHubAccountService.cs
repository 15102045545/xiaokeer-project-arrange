using ProjectArrange.Core;
using ProjectArrange.Core.Abstractions;

namespace ProjectArrange.Infrastructure.GitHub;

public sealed class GitHubAccountService(GitHubClientFactory factory) : IGitHubAccountService
{
    public async Task<Result<GitHubUser>> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var clientRes = await factory.CreateAsync(cancellationToken);
        if (!clientRes.IsSuccess) return Result<GitHubUser>.Fail(clientRes.Error!);

        try
        {
            var user = await clientRes.Value!.User.Current();
            return Result<GitHubUser>.Ok(new GitHubUser(user.Login, user.Id));
        }
        catch (Exception ex)
        {
            return Result<GitHubUser>.Fail(ex.Message);
        }
    }
}
