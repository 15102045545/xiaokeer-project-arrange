using ProjectArrange.App.Mvvm;
using ProjectArrange.Infrastructure.P2p;

namespace ProjectArrange.App.ViewModels;

public sealed class P2pInternalsViewModel : ObservableObject
{
    private readonly P2pConfig _config;
    private readonly P2pStorage _storage;

    private string _text = "";

    public P2pInternalsViewModel(P2pConfig config, P2pStorage storage)
    {
        _config = config;
        _storage = storage;
        RefreshCommand = new AsyncCommand(RefreshAsync);
    }

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    public AsyncCommand RefreshCommand { get; }

    private async Task RefreshAsync()
    {
        var trusted = await _storage.GetTrustedPeersAsync(CancellationToken.None);

        var cfg =
            $"DiscoveryPort={_config.DiscoveryPort}{Environment.NewLine}" +
            $"ListenPort={_config.ListenPort?.ToString() ?? ""}{Environment.NewLine}" +
            $"BroadcastIntervalMs={_config.BroadcastIntervalMilliseconds}{Environment.NewLine}" +
            $"PeerTtlMs={_config.PeerTtlMilliseconds}{Environment.NewLine}" +
            $"FileChunkBytes={_config.FileChunkBytes}{Environment.NewLine}" +
            $"MaxSendBytesPerSecond={_config.MaxSendBytesPerSecond?.ToString() ?? ""}";

        var peers = string.Join(Environment.NewLine, trusted.Select(t =>
            $"{t.DeviceName} {t.DeviceId} {t.Thumbprint}"));

        Text =
            $"Config:{Environment.NewLine}{cfg}{Environment.NewLine}{Environment.NewLine}" +
            $"TrustedPeers:{Environment.NewLine}{peers}";
    }
}

