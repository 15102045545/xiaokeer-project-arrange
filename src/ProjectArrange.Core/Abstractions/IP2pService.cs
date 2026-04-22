using ProjectArrange.Core.P2p;

namespace ProjectArrange.Core.Abstractions;

public sealed record FileTransferProgress(
    string TransferId,
    string Stage,
    long BytesTransferred,
    long TotalBytes);

public sealed record FileTransferResult(
    string TransferId,
    bool Success,
    string? Error);

public sealed record P2pStatus(
    PeerIdentity Self,
    int ListenPort,
    IReadOnlyList<PeerEndpoint> DiscoveredPeers,
    IReadOnlyList<PeerIdentity> TrustedPeers,
    IReadOnlyList<P2pConnection> Connections);

public interface IP2pService
{
    Task<Result> StartAsync(CancellationToken cancellationToken = default);
    Task<Result> StopAsync(CancellationToken cancellationToken = default);

    Task<Result<PairingCode>> CreatePairingCodeAsync(CancellationToken cancellationToken = default);
    Task<Result> TrustPeerFromPairingCodeAsync(string pairingCode, CancellationToken cancellationToken = default);

    Task<Result<P2pStatus>> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<Result<FileTransferResult>> SendFileAsync(
        string peerCertificateThumbprint,
        string filePath,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
