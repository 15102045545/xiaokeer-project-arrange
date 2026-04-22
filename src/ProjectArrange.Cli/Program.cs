using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectArrange.Core.Abstractions;
using ProjectArrange.Infrastructure;
using ProjectArrange.Infrastructure.Configuration;
using Serilog;

var host = Host.CreateDefaultBuilder(args)
    .UseContentRoot(AppContext.BaseDirectory)
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg.SetBasePath(AppContext.BaseDirectory);
        cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        cfg.AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: false);
        cfg.AddJsonFile(Path.Combine(AppPaths.GetAppDataRoot(), "appsettings.user.json"), optional: true, reloadOnChange: false);
    })
    .UseSerilog((ctx, _, lc) =>
    {
        Directory.CreateDirectory(AppPaths.GetLogsPath());
        lc.MinimumLevel.Information()
            .WriteTo.File(Path.Combine(AppPaths.GetLogsPath(), "cli-.log"), rollingInterval: RollingInterval.Day);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddProjectArrangeInfrastructure(ctx.Configuration);
    })
    .Build();

await host.StartAsync();

var command = args.FirstOrDefault() ?? "help";
var rest = args.Skip(1).ToArray();

switch (command.ToLowerInvariant())
{
    case "git-status":
    {
        var path = rest.FirstOrDefault() ?? Environment.CurrentDirectory;
        var git = host.Services.GetRequiredService<IGitService>();
        var status = await git.GetRepositoryStatusAsync(path);
        Console.WriteLine(status.IsSuccess ? status.Value : status.Error);
        break;
    }
    case "gh-status":
    {
        var gh = host.Services.GetRequiredService<IGhCliService>();
        var s = await gh.GetAuthStatusAsync();
        Console.WriteLine(s.IsSuccess ? s.Value : s.Error);
        break;
    }
    case "db-migrate":
    {
        var db = host.Services.GetRequiredService<IDatabaseMigrator>();
        var r = await db.MigrateAsync();
        Console.WriteLine(r.IsSuccess ? "OK" : r.Error);
        break;
    }
    case "presence-publish":
    {
        var note = rest.FirstOrDefault();
        var p = host.Services.GetRequiredService<IPresenceService>();
        var r = await p.PublishHeartbeatAsync(note);
        Console.WriteLine(r.IsSuccess ? r.Value : r.Error);
        break;
    }
    case "presence-peers":
    {
        var p = host.Services.GetRequiredService<IPresenceService>();
        var r = await p.GetPeersAsync();
        Console.WriteLine(r.IsSuccess ? string.Join(Environment.NewLine, r.Value!) : r.Error);
        break;
    }
    case "scan":
    {
        var path = rest.FirstOrDefault() ?? Environment.CurrentDirectory;
        var sc = host.Services.GetRequiredService<ISecretScanner>();
        var r = await sc.ScanAsync(path);
        Console.WriteLine(r.IsSuccess ? $"Findings: {r.Value!.Findings.Count}" : r.Error);
        break;
    }
    case "update-check":
    {
        var up = host.Services.GetRequiredService<IUpdateService>();
        var r = await up.CheckForUpdatesAsync();
        Console.WriteLine(r.IsSuccess ? r.Value : r.Error);
        break;
    }
    case "p2p-start":
    {
        var p2p = host.Services.GetRequiredService<IP2pService>();
        var r = await p2p.StartAsync();
        Console.WriteLine(r.IsSuccess ? "OK" : r.Error);
        await Task.Delay(TimeSpan.FromSeconds(10));
        break;
    }
    case "p2p-status":
    {
        var p2p = host.Services.GetRequiredService<IP2pService>();
        await p2p.StartAsync();
        var r = await p2p.GetStatusAsync();
        Console.WriteLine(r.IsSuccess ? r.Value : r.Error);
        break;
    }
    case "p2p-code":
    {
        var p2p = host.Services.GetRequiredService<IP2pService>();
        await p2p.StartAsync();
        var r = await p2p.CreatePairingCodeAsync();
        Console.WriteLine(r.IsSuccess ? r.Value!.Value : r.Error);
        break;
    }
    case "p2p-trust":
    {
        var code = rest.FirstOrDefault() ?? "";
        var p2p = host.Services.GetRequiredService<IP2pService>();
        var r = await p2p.TrustPeerFromPairingCodeAsync(code);
        Console.WriteLine(r.IsSuccess ? "OK" : r.Error);
        break;
    }
    case "p2p-send":
    {
        var thumb = rest.ElementAtOrDefault(0) ?? "";
        var path = rest.ElementAtOrDefault(1) ?? "";
        var p2p = host.Services.GetRequiredService<IP2pService>();
        await p2p.StartAsync();
        var progress = new Progress<FileTransferProgress>(p => Console.WriteLine($"{p.TransferId} {p.Stage} {p.BytesTransferred}/{p.TotalBytes}"));
        var r = await p2p.SendFileAsync(thumb, path, progress);
        Console.WriteLine(r.IsSuccess ? r.Value : r.Error);
        break;
    }
    default:
    {
        Console.WriteLine(
            "Commands:\n" +
            "  git-status [path]\n" +
            "  gh-status\n" +
            "  db-migrate\n" +
            "  presence-publish [note]\n" +
            "  presence-peers\n" +
            "  scan [path]\n" +
            "  update-check\n" +
            "  p2p-start\n" +
            "  p2p-status\n" +
            "  p2p-code\n" +
            "  p2p-trust <pairingCode>\n" +
            "  p2p-send <peerThumbprint> <filePath>\n");
        break;
    }
}

await host.StopAsync(TimeSpan.FromSeconds(2));
