using ProjectArrange.App.Mvvm;
using ProjectArrange.Core.Abstractions;

namespace ProjectArrange.App.ViewModels;

public sealed class P2pViewModel : ObservableObject
{
    private readonly IP2pService _p2p;

    private string _pairingCode = "";
    private string _inputCode = "";
    private string _summary = "";
    private string _details = "";

    public P2pViewModel(IP2pService p2p)
    {
        _p2p = p2p;
        StartCommand = new AsyncCommand(StartAsync);
        StopCommand = new AsyncCommand(StopAsync);
        CreateCodeCommand = new AsyncCommand(CreateCodeAsync);
        TrustCommand = new AsyncCommand(TrustAsync);
        RefreshCommand = new AsyncCommand(RefreshAsync);
    }

    public string PairingCode
    {
        get => _pairingCode;
        set => SetProperty(ref _pairingCode, value);
    }

    public string InputCode
    {
        get => _inputCode;
        set => SetProperty(ref _inputCode, value);
    }

    public string Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    public string Details
    {
        get => _details;
        set => SetProperty(ref _details, value);
    }

    public AsyncCommand StartCommand { get; }
    public AsyncCommand StopCommand { get; }
    public AsyncCommand CreateCodeCommand { get; }
    public AsyncCommand TrustCommand { get; }
    public AsyncCommand RefreshCommand { get; }

    private async Task StartAsync()
    {
        var r = await _p2p.StartAsync();
        Summary = r.IsSuccess ? "P2P started" : "P2P start failed";
        Details = r.Error ?? "";
        await RefreshAsync();
    }

    private async Task StopAsync()
    {
        var r = await _p2p.StopAsync();
        Summary = r.IsSuccess ? "P2P stopped" : "P2P stop failed";
        Details = r.Error ?? "";
        await RefreshAsync();
    }

    private async Task CreateCodeAsync()
    {
        var r = await _p2p.CreatePairingCodeAsync();
        if (!r.IsSuccess)
        {
            Summary = "Create code failed";
            Details = r.Error ?? "";
            return;
        }

        PairingCode = r.Value!.Value;
        Summary = "Pairing code created";
        Details = "";
    }

    private async Task TrustAsync()
    {
        var r = await _p2p.TrustPeerFromPairingCodeAsync(InputCode);
        Summary = r.IsSuccess ? "Peer trusted" : "Trust failed";
        Details = r.Error ?? "";
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var r = await _p2p.GetStatusAsync();
        if (!r.IsSuccess)
        {
            Details = r.Error ?? "";
            return;
        }

        var s = r.Value!;
        Summary = $"Self: {s.Self.DeviceName}  Listen: {s.ListenPort}  Discovered: {s.DiscoveredPeers.Count}  Trusted: {s.TrustedPeers.Count}  Connected: {s.Connections.Count}";

        var discovered = string.Join(Environment.NewLine, s.DiscoveredPeers.Select(p => $"D {p.DeviceName} {p.Ip}:{p.Port} {p.CertificateThumbprint} {p.LastSeenUtc:O}"));
        var trusted = string.Join(Environment.NewLine, s.TrustedPeers.Select(p => $"T {p.DeviceName} {p.DeviceId} {p.CertificateThumbprint}"));
        var connected = string.Join(Environment.NewLine, s.Connections.Select(c => $"C {c.Peer.DeviceName} {c.Peer.CertificateThumbprint} {c.LastSeenUtc:O}"));

        Details =
            $"PairingCode: {PairingCode}{Environment.NewLine}" +
            $"Trusted:{Environment.NewLine}{trusted}{Environment.NewLine}" +
            $"Discovered:{Environment.NewLine}{discovered}{Environment.NewLine}" +
            $"Connected:{Environment.NewLine}{connected}";
    }
}
