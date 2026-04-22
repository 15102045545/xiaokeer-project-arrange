using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectArrange.Infrastructure;
using ProjectArrange.Infrastructure.Configuration;
using Serilog;

namespace ProjectArrange.App;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureAppConfiguration((ctx, cfg) =>
            {
                cfg.SetBasePath(AppContext.BaseDirectory);
                cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                cfg.AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                cfg.AddJsonFile(Path.Combine(AppPaths.GetAppDataRoot(), "appsettings.user.json"), optional: true, reloadOnChange: true);
            })
            .UseSerilog((ctx, _, lc) =>
            {
                Directory.CreateDirectory(AppPaths.GetLogsPath());
                lc.MinimumLevel.Information()
                    .WriteTo.File(Path.Combine(AppPaths.GetLogsPath(), "app-.log"), rollingInterval: RollingInterval.Day);
            })
            .ConfigureServices((ctx, services) =>
            {
                services.AddProjectArrangeInfrastructure(ctx.Configuration);

                services.AddSingleton<Services.OperationLog>();
                services.AddSingleton<Services.TaskQueue>();
                services.AddSingleton<Services.TaskCenter>();
                services.AddSingleton<ViewModels.TaskCenterViewModel>();
                services.AddSingleton<ViewModels.ConfigViewModel>();
                services.AddSingleton<ViewModels.AiViewModel>();
                services.AddSingleton<ViewModels.GitStatusViewModel>();
                services.AddSingleton<ViewModels.GhStatusViewModel>();
                services.AddSingleton<ViewModels.GitHubAuthViewModel>();
                services.AddSingleton<ViewModels.DatabaseViewModel>();
                services.AddSingleton<ViewModels.PresenceViewModel>();
                services.AddSingleton<ViewModels.P2pViewModel>();
                services.AddSingleton<ViewModels.FileTransferViewModel>();
                services.AddSingleton<ViewModels.DiagnosticsViewModel>();
                services.AddSingleton<ViewModels.ProcessViewModel>();
                services.AddSingleton<ViewModels.IdentityViewModel>();
                services.AddSingleton<ViewModels.GitParserViewModel>();
                services.AddSingleton<ViewModels.P2pInternalsViewModel>();
                services.AddSingleton<ViewModels.Repos.RepoBackofficeViewModel>();
                services.AddSingleton<ViewModels.LogsViewModel>();
                services.AddSingleton<ViewModels.SecretScanViewModel>();
                services.AddSingleton<ViewModels.UpdateViewModel>();
                services.AddSingleton<MainViewModel>();
                services.AddTransient<MainWindow>();

                services.AddHostedService<Services.P2pRegistrySyncHostedService>();
            })
            .Build();

        _host.Start();

        var migrator = _host.Services.GetRequiredService<ProjectArrange.Core.Abstractions.IDatabaseMigrator>();
        migrator.MigrateAsync().GetAwaiter().GetResult();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

