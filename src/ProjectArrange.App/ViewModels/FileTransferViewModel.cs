using Microsoft.Win32;
using ProjectArrange.App.Mvvm;
using ProjectArrange.Core.Abstractions;

namespace ProjectArrange.App.ViewModels;

public sealed record PeerPick(string Thumbprint, string Display);

public sealed class FileTransferViewModel : ObservableObject
{
    private readonly IP2pService _p2p;

    private string _filePath = "";
    private string _status = "";
    private string _log = "";
    private string? _selectedPeerThumbprint;
    private List<PeerPick> _peers = new();

    public FileTransferViewModel(IP2pService p2p)
    {
        _p2p = p2p;
        RefreshPeersCommand = new AsyncCommand(RefreshPeersAsync);
        SelectFileCommand = new AsyncCommand(SelectFileAsync);
        SendCommand = new AsyncCommand(SendAsync, () => !string.IsNullOrWhiteSpace(FilePath) && !string.IsNullOrWhiteSpace(SelectedPeerThumbprint));
    }

    public List<PeerPick> Peers
    {
        get => _peers;
        private set
        {
            if (SetProperty(ref _peers, value))
            {
                SendCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? SelectedPeerThumbprint
    {
        get => _selectedPeerThumbprint;
        set
        {
            if (SetProperty(ref _selectedPeerThumbprint, value))
            {
                SendCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string FilePath
    {
        get => _filePath;
        set
        {
            if (SetProperty(ref _filePath, value))
            {
                SendCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string Log
    {
        get => _log;
        set => SetProperty(ref _log, value);
    }

    public AsyncCommand RefreshPeersCommand { get; }
    public AsyncCommand SelectFileCommand { get; }
    public AsyncCommand SendCommand { get; }

    private async Task RefreshPeersAsync()
    {
        await _p2p.StartAsync();
        var status = await _p2p.GetStatusAsync();
        if (!status.IsSuccess)
        {
            Status = "P2P status error";
            Log = status.Error ?? "";
            return;
        }

        var s = status.Value!;
        var connectedThumbprints = new HashSet<string>(s.Connections.Select(c => c.Peer.CertificateThumbprint), StringComparer.OrdinalIgnoreCase);
        var picks = s.DiscoveredPeers
            .Where(p => connectedThumbprints.Contains(p.CertificateThumbprint))
            .OrderBy(p => p.DeviceName)
            .Select(p => new PeerPick(p.CertificateThumbprint, $"{p.DeviceName}  {p.Ip}:{p.Port}  {p.CertificateThumbprint}"))
            .ToList();

        Peers = picks;
        if (SelectedPeerThumbprint is null && picks.Count > 0) SelectedPeerThumbprint = picks[0].Thumbprint;
        Status = $"Peers: {picks.Count}";
        Log = Status;
    }

    private Task SelectFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select file to send"
        };

        if (dialog.ShowDialog() == true)
        {
            FilePath = dialog.FileName;
        }

        return Task.CompletedTask;
    }

    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedPeerThumbprint))
        {
            Status = "Select a peer";
            return;
        }
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            Status = "Select a file";
            return;
        }

        await _p2p.StartAsync();

        var progress = new Progress<FileTransferProgress>(p =>
        {
            Status = $"{p.Stage}  {p.BytesTransferred}/{p.TotalBytes}";
            Log = Status;
        });

        Status = "Sending...";
        var res = await _p2p.SendFileAsync(SelectedPeerThumbprint, FilePath, progress);
        if (!res.IsSuccess)
        {
            Status = "Send failed";
            Log = res.Error ?? "";
            return;
        }

        Status = res.Value!.Success ? "Send OK" : "Send FAIL";
        Log = res.Value.Error ?? "";
    }
}
