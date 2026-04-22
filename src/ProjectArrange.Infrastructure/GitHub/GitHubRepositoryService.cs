using Octokit;
using ProjectArrange.Core;
using ProjectArrange.Core.Abstractions;
using ProjectArrange.Core.GitHub;

namespace ProjectArrange.Infrastructure.GitHub;

public sealed class GitHubRepositoryService(GitHubClientFactory factory) : IGitHubRepositoryService
{
    public async Task<Result> UpdateRepositoryAsync(GitHubRepoRef repo, GitHubRepoUpdate update, CancellationToken cancellationToken = default)
    {
        var clientRes = await factory.CreateAsync(cancellationToken);
        if (!clientRes.IsSuccess) return Result.Fail(clientRes.Error!);

        try
        {
            var current = await clientRes.Value!.Repository.Get(repo.Owner, repo.Name);
            var req = new RepositoryUpdate
            {
                Name = current.Name
            };

            if (update.Description is not null) req.Description = update.Description;
            if (update.IsPrivate is not null) req.Private = update.IsPrivate.Value;

            await clientRes.Value!.Repository.Edit(repo.Owner, repo.Name, req);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }
}
