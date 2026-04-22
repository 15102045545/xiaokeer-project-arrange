using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using ProjectArrange.Core;
using ProjectArrange.Core.Abstractions;
using ProjectArrange.Core.P2p;

namespace ProjectArrange.Infrastructure.P2p;

public sealed class LanP2pService(
    P2pIdentityProvider identityProvider,
    P2pStorage storage,
    P2pConfig config) : IP2pService
{
    private readonly ConcurrentDictionary<string, PeerEndpoint> _discovered = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, P2pConnection> _connections = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileSends = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _cts;
    private UdpClient? _udp;
    private TcpListener? _listener;
    private Task? _broadcastTask;
    private Task? _receiveTask;
    private Task? _acceptTask;
    private int _listenPort;

    public async Task<Result> StartAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is not null) return Result.Ok();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ct = _cts.Token;

        var cert = identityProvider.GetOrCreateCertificate();
        _listenPort = config.ListenPort ?? GetFreeTcpPort();
        _listener = new TcpListener(IPAddress.Any, _listenPort);
        _listener.Start();

        _udp = new UdpClient(AddressFamily.InterNetwork)
        {
            EnableBroadcast = true
        };
        _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udp.Client.Bind(new IPEndPoint(IPAddress.Any, config.DiscoveryPort));

        _broadcastTask = Task.Run(() => BroadcastLoopAsync(cert, ct), ct);
        _receiveTask = Task.Run(() => ReceiveLoopAsync(ct), ct);
        _acceptTask = Task.Run(() => AcceptLoopAsync(cert, ct), ct);

        return Result.Ok();
    }

    public Task<Result> StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is null) return Task.FromResult(Result.Ok());

        try
        {
            _cts.Cancel();
        }
        catch
        {
        }

        try
        {
            _udp?.Dispose();
        }
        catch
        {
        }

        try
        {
            _listener?.Stop();
        }
        catch
        {
        }

        _cts.Dispose();
        _cts = null;

        return Task.FromResult(Result.Ok());
    }

    public async Task<Result<PairingCode>> CreatePairingCodeAsync(CancellationToken cancellationToken = default)
    {
        var cert = identityProvider.GetOrCreateCertificate();
        var code = P2pPairingCodec.Encode(
            identityProvider.GetOrCreateDeviceId(),
            identityProvider.GetDeviceName(),
            cert.GetCertHashString());
        return Result<PairingCode>.Ok(code);
    }

    public async Task<Result> TrustPeerFromPairingCodeAsync(string pairingCode, CancellationToken cancellationToken = default)
    {
        if (!P2pPairingCodec.TryDecode(pairingCode, out var payload))
        {
            return Result.Fail("Invalid pairing code.");
        }

        await storage.UpsertTrustedPeerAsync(payload.Thumbprint, payload.DeviceId, payload.DeviceName, cancellationToken);
        return Result.Ok();
    }

    public async Task<Result<P2pStatus>> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var cert = identityProvider.GetOrCreateCertificate();
        var self = new PeerIdentity(
            identityProvider.GetOrCreateDeviceId(),
            identityProvider.GetDeviceName(),
            P2pPairingCodec.NormalizeThumbprint(cert.GetCertHashString()));

        var trusted = await storage.GetTrustedPeersAsync(cancellationToken);
        var trustedPeers = trusted
            .Select(t => new PeerIdentity(t.DeviceId, t.DeviceName, P2pPairingCodec.NormalizeThumbprint(t.Thumbprint)))
            .ToArray();

        PrunePeers();

        return Result<P2pStatus>.Ok(new P2pStatus(
            Self: self,
            ListenPort: _listenPort,
            DiscoveredPeers: _discovered.Values.OrderByDescending(p => p.LastSeenUtc).ToArray(),
            TrustedPeers: trustedPeers,
            Connections: _connections.Values.OrderByDescending(c => c.LastSeenUtc).ToArray()));
    }

    public async Task<Result<FileTransferResult>> SendFileAsync(
        string peerCertificateThumbprint,
        string filePath,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var thumb = P2pPairingCodec.NormalizeThumbprint(peerCertificateThumbprint);
            PeerEndpoint endpoint;
            var selfCert = identityProvider.GetOrCreateCertificate();
            var selfThumb = P2pPairingCodec.NormalizeThumbprint(selfCert.GetCertHashString());
            if (thumb.Equals(selfThumb, StringComparison.OrdinalIgnoreCase))
            {
                endpoint = new PeerEndpoint(
                    DeviceId: identityProvider.GetOrCreateDeviceId(),
                    DeviceName: identityProvider.GetDeviceName(),
                    CertificateThumbprint: selfThumb,
                    Ip: "127.0.0.1",
                    Port: _listenPort,
                    LastSeenUtc: DateTimeOffset.UtcNow);
            }
            else if (!_discovered.TryGetValue(thumb, out endpoint!))
            {
                return Result<FileTransferResult>.Fail("Peer not discovered on LAN.");
            }

            if (!File.Exists(filePath))
            {
                return Result<FileTransferResult>.Fail("File not found.");
            }

            var sem = _fileSends.GetOrAdd(thumb, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync(cancellationToken);
            try
            {
                return await SendFileInternalAsync(endpoint, filePath, progress, cancellationToken);
            }
            finally
            {
                sem.Release();
            }
        }
        catch (Exception ex)
        {
            return Result<FileTransferResult>.Fail(ex.Message);
        }
    }

    private async Task BroadcastLoopAsync(X509Certificate2 cert, CancellationToken cancellationToken)
    {
        var id = identityProvider.GetOrCreateDeviceId();
        var name = identityProvider.GetDeviceName();
        var thumb = cert.Thumbprint ?? "";

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var bytes = P2pDiscoveryProtocol.Encode(id, name, thumb, _listenPort);
                await _udp!.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, config.DiscoveryPort));
            }
            catch
            {
            }

            try
            {
                await Task.Delay(config.BroadcastIntervalMilliseconds, cancellationToken);
            }
            catch
            {
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await _udp!.ReceiveAsync(cancellationToken);
            }
            catch
            {
                continue;
            }

            if (!P2pDiscoveryProtocol.TryDecode(received.Buffer, received.RemoteEndPoint, out var peer)) continue;

            var myId = identityProvider.GetOrCreateDeviceId();
            if (peer.DeviceId.Equals(myId, StringComparison.OrdinalIgnoreCase)) continue;

            var endpoint = new PeerEndpoint(
                peer.DeviceId,
                peer.DeviceName,
                peer.Thumbprint,
                peer.Ip,
                peer.Port,
                peer.SeenUtc);

            _discovered.AddOrUpdate(endpoint.CertificateThumbprint, endpoint, (_, __) => endpoint);

            _ = TryConnectAsync(endpoint, cancellationToken);
        }
    }

    private async Task AcceptLoopAsync(X509Certificate2 cert, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener!.AcceptTcpClientAsync(cancellationToken);
            }
            catch
            {
                continue;
            }

            _ = Task.Run(() => HandleIncomingAsync(client, cert, cancellationToken), cancellationToken);
        }
    }

    private async Task HandleIncomingAsync(TcpClient client, X509Certificate2 cert, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = client.GetStream();

            var trusted = await storage.GetTrustedPeersAsync(cancellationToken);
            var trustedThumbprints = new HashSet<string>(trusted.Select(t => P2pPairingCodec.NormalizeThumbprint(t.Thumbprint)), StringComparer.OrdinalIgnoreCase);

            using var ssl = new SslStream(stream, leaveInnerStreamOpen: false, (sender, certificate, chain, errors) =>
            {
                if (certificate is null) return false;
                var thumb = P2pPairingCodec.NormalizeThumbprint(certificate.GetCertHashString());
                return trustedThumbprints.Contains(thumb);
            });

            try
            {
                await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = cert,
                    ClientCertificateRequired = true,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                }, cancellationToken);
            }
            catch
            {
                return;
            }

            var remoteCert = ssl.RemoteCertificate;
            if (remoteCert is null) return;

            var remoteThumb = P2pPairingCodec.NormalizeThumbprint(remoteCert.GetCertHashString());
            var known = trusted.FirstOrDefault(t => P2pPairingCodec.NormalizeThumbprint(t.Thumbprint).Equals(remoteThumb, StringComparison.OrdinalIgnoreCase));
            if (known == default) return;

            var peer = new PeerIdentity(known.DeviceId, known.DeviceName, remoteThumb);
            var conn = new P2pConnection(peer, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            _connections.AddOrUpdate(peer.CertificateThumbprint, conn, (_, __) => conn);

            var firstLine = await LineWire.ReadLineAsync(ssl, maxBytes: 8192, cancellationToken);
            if (firstLine is null)
            {
                _connections.TryRemove(peer.CertificateThumbprint, out _);
                return;
            }

            if (firstLine.StartsWith("FT1 ", StringComparison.Ordinal))
            {
                await HandleFileTransferAsync(ssl, peer, firstLine, cancellationToken);
                _connections.TryRemove(peer.CertificateThumbprint, out _);
                return;
            }

            await RunConnectionLoopAsync(ssl, peer, firstLine, cancellationToken);
        }
        finally
        {
            try
            {
                client.Dispose();
            }
            catch
            {
            }
        }
    }

    private async Task TryConnectAsync(PeerEndpoint endpoint, CancellationToken cancellationToken)
    {
        if (_connections.ContainsKey(endpoint.CertificateThumbprint)) return;

        var trusted = await storage.GetTrustedPeersAsync(cancellationToken);
        var trustedThumbprints = new HashSet<string>(trusted.Select(t => P2pPairingCodec.NormalizeThumbprint(t.Thumbprint)), StringComparer.OrdinalIgnoreCase);
        if (!trustedThumbprints.Contains(endpoint.CertificateThumbprint)) return;

        using TcpClient client = new();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(IPAddress.Parse(endpoint.Ip), endpoint.Port, cts.Token);
        }
        catch
        {
            return;
        }

        var cert = identityProvider.GetOrCreateCertificate();
        using var stream = client.GetStream();
        using var ssl = new SslStream(stream, leaveInnerStreamOpen: false, (sender, certificate, chain, errors) =>
        {
            if (certificate is null) return false;
            var thumb = P2pPairingCodec.NormalizeThumbprint(certificate.GetCertHashString());
            return thumb.Equals(endpoint.CertificateThumbprint, StringComparison.OrdinalIgnoreCase);
        });

        try
        {
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "ProjectArrange",
                ClientCertificates = new X509CertificateCollection { cert },
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            }, cancellationToken);
        }
        catch
        {
            client.Dispose();
            return;
        }

        var peer = new PeerIdentity(endpoint.DeviceId, endpoint.DeviceName, endpoint.CertificateThumbprint);
        var conn = new P2pConnection(peer, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _connections.AddOrUpdate(peer.CertificateThumbprint, conn, (_, __) => conn);

        await RunConnectionLoopAsync(ssl, peer, firstLine: null, cancellationToken);
    }

    private async Task RunConnectionLoopAsync(SslStream ssl, PeerIdentity peer, string? firstLine, CancellationToken cancellationToken)
    {
        var hello = $"HELLO {identityProvider.GetOrCreateDeviceId()} {identityProvider.GetDeviceName()}";
        try
        {
            await LineWire.WriteLineAsync(ssl, hello, cancellationToken);
        }
        catch
        {
            _connections.TryRemove(peer.CertificateThumbprint, out _);
            return;
        }

        _ = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await LineWire.WriteLineAsync(ssl, $"PING {DateTimeOffset.UtcNow:O}", cancellationToken);
                }
                catch
                {
                    break;
                }

                try
                {
                    await Task.Delay(3000, cancellationToken);
                }
                catch
                {
                    break;
                }
            }
        }, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = firstLine ?? await LineWire.ReadLineAsync(ssl, maxBytes: 8192, cancellationToken);
                firstLine = null;
            }
            catch
            {
                break;
            }

            if (line is null) break;
            _connections.AddOrUpdate(peer.CertificateThumbprint,
                _ => new P2pConnection(peer, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
                (_, existing) => existing with { LastSeenUtc = DateTimeOffset.UtcNow });
        }

        _connections.TryRemove(peer.CertificateThumbprint, out _);
    }

    private async Task<Result<FileTransferResult>> SendFileInternalAsync(
        PeerEndpoint endpoint,
        string filePath,
        IProgress<FileTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var transferId = Guid.NewGuid().ToString("N");
        var fileInfo = new FileInfo(filePath);
        var totalBytes = fileInfo.Length;

        progress?.Report(new FileTransferProgress(transferId, "Hashing", 0, totalBytes));
        var sha256 = await Ft1Protocol.ComputeSha256HexAsync(filePath, cancellationToken);

        using TcpClient client = new();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(IPAddress.Parse(endpoint.Ip), endpoint.Port, cts.Token);
        }
        catch
        {
            return Result<FileTransferResult>.Fail("Connect failed.");
        }

        var cert = identityProvider.GetOrCreateCertificate();
        using var stream = client.GetStream();
        using var ssl = new SslStream(stream, leaveInnerStreamOpen: false, (sender, certificate, chain, errors) =>
        {
            if (certificate is null) return false;
            var thumb = P2pPairingCodec.NormalizeThumbprint(certificate.GetCertHashString());
            return thumb.Equals(endpoint.CertificateThumbprint, StringComparison.OrdinalIgnoreCase);
        });

        try
        {
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "ProjectArrange",
                ClientCertificates = new X509CertificateCollection { cert },
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            }, cancellationToken);
        }
        catch
        {
            return Result<FileTransferResult>.Fail("TLS handshake failed.");
        }

        var offer = new Ft1Protocol.Offer(
            TransferId: transferId,
            FileName: fileInfo.Name,
            SizeBytes: totalBytes,
            Sha256Hex: sha256,
            ChunkBytes: config.FileChunkBytes);

        progress?.Report(new FileTransferProgress(transferId, "Offering", 0, totalBytes));
        await LineWire.WriteLineAsync(ssl, Ft1Protocol.ToOfferLine(offer), cancellationToken);

        var acceptLine = await LineWire.ReadLineAsync(ssl, 8192, cancellationToken);
        if (acceptLine is null) return Result<FileTransferResult>.Ok(new FileTransferResult(transferId, false, "Peer closed."));

        if (Ft1Protocol.TryParseFail(acceptLine, out var fail0) && fail0.TransferId == transferId)
        {
            return Result<FileTransferResult>.Ok(new FileTransferResult(transferId, false, fail0.Reason));
        }

        if (!Ft1Protocol.TryParseAccept(acceptLine, out var accept) || accept.TransferId != transferId)
        {
            return Result<FileTransferResult>.Ok(new FileTransferResult(transferId, false, "Invalid ACCEPT."));
        }

        var offset = accept.Offset;
        if (offset < 0) offset = 0;
        if (offset > totalBytes) offset = 0;

        var chunk = new byte[config.FileChunkBytes];
        var sent = offset;

        progress?.Report(new FileTransferProgress(transferId, "Sending", sent, totalBytes));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        long budgetBytes = 0;

        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        fs.Seek(offset, SeekOrigin.Begin);

        while (sent < totalBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var toRead = (int)Math.Min(chunk.Length, totalBytes - sent);
            var read = await fs.ReadAsync(chunk.AsMemory(0, toRead), cancellationToken);
            if (read <= 0) break;

            await LineWire.WriteLineAsync(ssl, Ft1Protocol.DataLine(transferId, sent, read), cancellationToken);
            await ssl.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            await ssl.FlushAsync(cancellationToken);

            sent += read;
            progress?.Report(new FileTransferProgress(transferId, "Sending", sent, totalBytes));

            if (config.MaxSendBytesPerSecond is long max && max > 0)
            {
                budgetBytes += read;
                var expectedMs = (budgetBytes * 1000.0) / max;
                var actualMs = stopwatch.Elapsed.TotalMilliseconds;
                if (expectedMs > actualMs)
                {
                    var delay = (int)Math.Min(2000, expectedMs - actualMs);
                    if (delay > 0) await Task.Delay(delay, cancellationToken);
                }
            }
        }

        progress?.Report(new FileTransferProgress(transferId, "Finalizing", sent, totalBytes));
        await LineWire.WriteLineAsync(ssl, Ft1Protocol.DoneLine(transferId, sha256), cancellationToken);

        var finalLine = await LineWire.ReadLineAsync(ssl, 8192, cancellationToken);
        if (finalLine is null) return Result<FileTransferResult>.Ok(new FileTransferResult(transferId, false, "Peer closed."));

        if (Ft1Protocol.TryParseOk(finalLine, out var okId) && okId == transferId)
        {
            progress?.Report(new FileTransferProgress(transferId, "Done", totalBytes, totalBytes));
            return Result<FileTransferResult>.Ok(new FileTransferResult(transferId, true, null));
        }

        if (Ft1Protocol.TryParseFail(finalLine, out var fail) && fail.TransferId == transferId)
        {
            return Result<FileTransferResult>.Ok(new FileTransferResult(transferId, false, fail.Reason));
        }

        return Result<FileTransferResult>.Ok(new FileTransferResult(transferId, false, "Invalid response."));
    }

    private async Task HandleFileTransferAsync(SslStream ssl, PeerIdentity peer, string firstLine, CancellationToken cancellationToken)
    {
        if (!Ft1Protocol.TryParseOffer(firstLine, out var offer))
        {
            await LineWire.WriteLineAsync(ssl, Ft1Protocol.FailLine("unknown", "Invalid OFFER"), cancellationToken);
            return;
        }

        var root = Path.Combine(ProjectArrange.Infrastructure.Configuration.AppPaths.GetAppDataRoot(), "transfers", "incoming");
        Directory.CreateDirectory(root);

        var safeName = string.Join("_", offer.FileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "file.bin";

        var partPath = Path.Combine(root, $"{offer.TransferId}.part");
        var finalPath = Path.Combine(root, safeName);

        long offset = 0;
        if (File.Exists(partPath))
        {
            var len = new FileInfo(partPath).Length;
            if (len >= 0 && len <= offer.SizeBytes) offset = len;
        }

        await LineWire.WriteLineAsync(ssl, Ft1Protocol.AcceptLine(offer.TransferId, offset), cancellationToken);

        await using var fs = new FileStream(partPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        fs.Seek(offset, SeekOrigin.Begin);

        var received = offset;
        var buffer = new byte[Math.Max(offer.ChunkBytes, 4096)];

        while (received < offer.SizeBytes)
        {
            var line = await LineWire.ReadLineAsync(ssl, 8192, cancellationToken);
            if (line is null)
            {
                return;
            }

            if (Ft1Protocol.TryParseDone(line, out var done) && done.TransferId == offer.TransferId)
            {
                break;
            }

            if (!Ft1Protocol.TryParseData(line, out var data) || data.TransferId != offer.TransferId)
            {
                await LineWire.WriteLineAsync(ssl, Ft1Protocol.FailLine(offer.TransferId, "Invalid DATA"), cancellationToken);
                return;
            }

            if (data.Offset != received)
            {
                await LineWire.WriteLineAsync(ssl, Ft1Protocol.FailLine(offer.TransferId, "Offset mismatch"), cancellationToken);
                return;
            }

            if (data.Count == 0) continue;
            if (data.Count > buffer.Length) buffer = new byte[data.Count];

            await LineWire.ReadExactAsync(ssl, buffer, 0, data.Count, cancellationToken);
            await fs.WriteAsync(buffer.AsMemory(0, data.Count), cancellationToken);
            await fs.FlushAsync(cancellationToken);

            received += data.Count;
        }

        fs.Close();

        var actualHash = await Ft1Protocol.ComputeSha256HexAsync(partPath, cancellationToken);
        if (!actualHash.Equals(offer.Sha256Hex, StringComparison.OrdinalIgnoreCase))
        {
            await LineWire.WriteLineAsync(ssl, Ft1Protocol.FailLine(offer.TransferId, "Hash mismatch"), cancellationToken);
            return;
        }

        var uniqueFinal = finalPath;
        if (File.Exists(uniqueFinal))
        {
            var nameOnly = Path.GetFileNameWithoutExtension(finalPath);
            var ext = Path.GetExtension(finalPath);
            uniqueFinal = Path.Combine(root, $"{nameOnly}-{offer.TransferId}{ext}");
        }

        File.Move(partPath, uniqueFinal, overwrite: true);
        await LineWire.WriteLineAsync(ssl, Ft1Protocol.OkLine(offer.TransferId), cancellationToken);
    }

    private void PrunePeers()
    {
        var cutoff = DateTimeOffset.UtcNow.AddMilliseconds(-config.PeerTtlMilliseconds);
        foreach (var kv in _discovered)
        {
            if (kv.Value.LastSeenUtc < cutoff)
            {
                _discovered.TryRemove(kv.Key, out _);
            }
        }
    }

    private static int GetFreeTcpPort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}
