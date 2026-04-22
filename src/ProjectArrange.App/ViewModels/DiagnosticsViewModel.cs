using System.IO;
using ProjectArrange.App.Mvvm;
using ProjectArrange.Infrastructure.Configuration;
using ProjectArrange.Infrastructure.Process;

namespace ProjectArrange.App.ViewModels;

public sealed class DiagnosticsViewModel : ObservableObject
{
    private readonly IProcessRunner _runner;

    private string _paths = "";
    private string _tools = "";
    private string _probe = "";

    public DiagnosticsViewModel(IProcessRunner runner)
    {
        _runner = runner;
        RefreshCommand = new AsyncCommand(RefreshAsync);
        WriteProbeCommand = new AsyncCommand(WriteProbeAsync);
    }

    public string Paths
    {
        get => _paths;
        set => SetProperty(ref _paths, value);
    }

    public string Tools
    {
        get => _tools;
        set => SetProperty(ref _tools, value);
    }

    public string Probe
    {
        get => _probe;
        set => SetProperty(ref _probe, value);
    }

    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand WriteProbeCommand { get; }

    private async Task RefreshAsync()
    {
        var dataRoot = AppPaths.GetAppDataRoot();
        Paths =
            $"PROJECTARRANGE_DATA_ROOT={Environment.GetEnvironmentVariable("PROJECTARRANGE_DATA_ROOT") ?? ""}{Environment.NewLine}" +
            $"AppDataRoot={dataRoot}{Environment.NewLine}" +
            $"SecretsPath={AppPaths.GetSecretsPath()}{Environment.NewLine}" +
            $"DatabasePath={AppPaths.GetDatabasePath()}{Environment.NewLine}" +
            $"LogsPath={AppPaths.GetLogsPath()}{Environment.NewLine}" +
            $"MigrationsRoot={AppPaths.GetMigrationsRoot()}{Environment.NewLine}" +
            $"PromptsRoot={AppPaths.GetPromptsRoot()}";

        Tools =
            await ToolCheckAsync("git", "--version") +
            await ToolCheckAsync("gh", "--version") +
            await ToolCheckAsync("gitleaks", "version");
    }

    private async Task<string> ToolCheckAsync(string exe, string args)
    {
        var res = await _runner.RunAsync(new ProcessSpec(exe, args, TimeoutMilliseconds: 8000));
        var status = res.ExitCode == 0 ? "OK" : "FAIL";
        return
            $"{exe} {args} -> {status} exit={res.ExitCode} timedOut={res.TimedOut}{Environment.NewLine}" +
            $"{res.StdOut}{Environment.NewLine}" +
            $"{res.StdErr}{Environment.NewLine}" +
            $"----{Environment.NewLine}";
    }

    private Task WriteProbeAsync()
    {
        try
        {
            var root = AppPaths.GetAppDataRoot();
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, $"probe-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.txt");
            File.WriteAllText(path, $"utc={DateTimeOffset.UtcNow:O}");
            Probe = $"OK: {path}";
        }
        catch (Exception ex)
        {
            Probe = ex.Message;
        }

        return Task.CompletedTask;
    }
}
