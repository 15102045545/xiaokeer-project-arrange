using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProjectArrange.Core.Abstractions;
using ProjectArrange.Infrastructure.Ai;
using ProjectArrange.Infrastructure.Configuration;
using ProjectArrange.Infrastructure.Git;
using ProjectArrange.Infrastructure.GitHub;
using ProjectArrange.Infrastructure.OpenSourceify;
using ProjectArrange.Infrastructure.Presence;
using ProjectArrange.Infrastructure.Process;
using ProjectArrange.Infrastructure.P2p;
using ProjectArrange.Infrastructure.Security;
using ProjectArrange.Infrastructure.Repos;
using ProjectArrange.Infrastructure.Storage;
using ProjectArrange.Infrastructure.Update;

namespace ProjectArrange.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProjectArrangeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var updateUrl = configuration["UpdateFeed:BaseUrl"] ?? "http://localhost:5123";
        var updateFeedBaseUri = new Uri(updateUrl);

        services.AddSingleton(configuration);
        services.AddSingleton<IUserConfigStore, UserConfigStore>();

        services.AddSingleton<IToolLocator, ToolLocator>();
        services.AddSingleton(sp => configuration.GetSection("P2p").Get<P2pConfig>() ?? new P2pConfig());

        services.AddSingleton<IProcessRunner, ProcessRunner>();

        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<IGhCliService, GhCliService>();

        services.AddSingleton<IAppSecretsStore, DpapiSecretsStore>();
        services.AddSingleton<GitHubClientFactory>();
        services.AddSingleton<IGitHubAccountService, GitHubAccountService>();
        services.AddSingleton<IGitHubRepositoryService, GitHubRepositoryService>();

        services.AddSingleton<IDatabaseMigrator, SqliteDatabaseMigrator>();
        services.AddSingleton<MachineIdentityProvider>();
        services.AddSingleton<IPresenceService, SqlitePresenceService>();

        services.AddSingleton<ISecretScanner, GitleaksSecretScanner>();

        services.AddSingleton<IRepoRegistryService, SqliteRepoRegistryService>();
        services.AddSingleton<IOpenSourceifyService, OpenSourceifyService>();
        services.AddSingleton<P2pIdentityProvider>();
        services.AddSingleton<P2pStorage>();
        services.AddSingleton<IP2pService, LanP2pService>();

        services.AddHttpClient("update-feed");
        services.AddHttpClient("ai");
        services.AddSingleton<IAiService, PythonHttpAiService>();
        services.AddSingleton<IUpdateService>(sp =>
            new HttpUpdateService(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("update-feed"),
                updateFeedBaseUri));

        return services;
    }
}
