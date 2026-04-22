using Microsoft.Extensions.Configuration;
using ProjectArrange.App.Mvvm;
using ProjectArrange.Core.Abstractions;
using ProjectArrange.Infrastructure.P2p;

namespace ProjectArrange.App.ViewModels;

public sealed class ConfigViewModel : ObservableObject
{
    private readonly IConfiguration _configuration;
    private readonly IUserConfigStore _store;

    private string _userJson = "";
    private string _effective = "";
    private string _status = "";

    public ConfigViewModel(IConfiguration configuration, IUserConfigStore store)
    {
        _configuration = configuration;
        _store = store;

        LoadUserConfigCommand = new AsyncCommand(LoadUserConfigAsync);
        SaveUserConfigCommand = new AsyncCommand(SaveUserConfigAsync);
        RefreshEffectiveCommand = new AsyncCommand(RefreshEffectiveAsync);

        _ = LoadUserConfigAsync();
        _ = RefreshEffectiveAsync();
    }

    public string UserJson
    {
        get => _userJson;
        set => SetProperty(ref _userJson, value);
    }

    public string Effective
    {
        get => _effective;
        set => SetProperty(ref _effective, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public AsyncCommand LoadUserConfigCommand { get; }
    public AsyncCommand SaveUserConfigCommand { get; }
    public AsyncCommand RefreshEffectiveCommand { get; }

    private async Task LoadUserConfigAsync()
    {
        var res = await _store.ReadUserConfigJsonAsync();
        if (!res.IsSuccess)
        {
            Status = $"Load failed: {res.Error}";
            return;
        }

        UserJson = res.Value ?? "{\n}\n";
        Status = $"Loaded: {_store.UserConfigPath}";
    }

    private async Task SaveUserConfigAsync()
    {
        var res = await _store.WriteUserConfigJsonAsync(UserJson);
        Status = res.IsSuccess ? $"Saved: {_store.UserConfigPath}" : $"Save failed: {res.Error}";
        await RefreshEffectiveAsync();
    }

    private Task RefreshEffectiveAsync()
    {
        var tools = new[]
        {
            ("git", _configuration["Tools:git"]),
            ("gh", _configuration["Tools:gh"]),
            ("gitleaks", _configuration["Tools:gitleaks"]),
            ("python", _configuration["Tools:python"])
        };

        var p2p = _configuration.GetSection("P2p").Get<P2pConfig>() ?? new P2pConfig();
        var updateBaseUrl = _configuration["UpdateFeed:BaseUrl"] ?? "";
        var aiEndpoint = _configuration["Ai:Endpoint"] ?? "";
        var syncEnabled = _configuration["P2pRegistrySync:Enabled"] ?? "";
        var syncInterval = _configuration["P2pRegistrySync:IntervalSeconds"] ?? "";

        Effective = string.Join(Environment.NewLine, new[]
        {
            $"UserConfigPath={_store.UserConfigPath}",
            "",
            "Tools:",
            string.Join(Environment.NewLine, tools.Select(t => $"- {t.Item1}={t.Item2}")),
            "",
            $"UpdateFeed:BaseUrl={updateBaseUrl}",
            "",
            $"Ai:Endpoint={aiEndpoint}",
            "",
            $"P2pRegistrySync:Enabled={syncEnabled}",
            $"P2pRegistrySync:IntervalSeconds={syncInterval}",
            "",
            "P2p:",
            $"DiscoveryPort={p2p.DiscoveryPort}",
            $"ListenPort={p2p.ListenPort}",
            $"BroadcastIntervalMilliseconds={p2p.BroadcastIntervalMilliseconds}",
            $"PeerTtlMilliseconds={p2p.PeerTtlMilliseconds}",
            $"FileChunkBytes={p2p.FileChunkBytes}",
            $"MaxSendBytesPerSecond={p2p.MaxSendBytesPerSecond}"
        });

        return Task.CompletedTask;
    }
}

