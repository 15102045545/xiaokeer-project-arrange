using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ProjectArrange.Core;
using ProjectArrange.Core.Abstractions;
using ProjectArrange.Core.Repos;
using ProjectArrange.Infrastructure.Configuration;

namespace ProjectArrange.App.Services;

public sealed class P2pRegistrySyncHostedService(
    IConfiguration configuration,
    IP2pService p2p,
    IRepoRegistryService registry,
    TaskCenter tasks) : BackgroundService
{
    private sealed record DigestPayload(
        string FromThumbprint,
        string FromDeviceId,
        string FromDeviceName,
        string Digest,
        int RepoCount,
        DateTimeOffset Utc);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var enabled = configuration.GetValue<bool>("P2pRegistrySync:Enabled", false);
            var intervalSeconds = configuration.GetValue<int>("P2pRegistrySync:IntervalSeconds", 15);
            if (intervalSeconds < 3) intervalSeconds = 3;

            if (enabled)
            {
                await TickAsync(stoppingToken);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch
            {
            }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        await ProcessIncomingAsync(cancellationToken);

        var statusRes = await p2p.GetStatusAsync(cancellationToken);
        if (!statusRes.IsSuccess || statusRes.Value is null) return;

        var reposRes = await registry.GetAllAsync(cancellationToken);
        if (!reposRes.IsSuccess || reposRes.Value is null) return;

        var digest = ComputeDigest(reposRes.Value);
        var trusted = new HashSet<string>(statusRes.Value.TrustedPeers.Select(p => p.CertificateThumbprint), StringComparer.OrdinalIgnoreCase);
        var peers = statusRes.Value.DiscoveredPeers.Where(p => trusted.Contains(p.CertificateThumbprint)).ToArray();

        foreach (var peer in peers)
        {
            EnqueueSendDigest(peer.CertificateThumbprint, statusRes.Value.Self, digest, reposRes.Value.Count);
        }
    }

    private void EnqueueSendDigest(string peerThumbprint, Core.P2p.PeerIdentity self, string digest, int repoCount)
    {
        var dk = $"p2p-registry-digest|{peerThumbprint}|{digest}";
        tasks.Enqueue(
            TaskKind.P2pRegistrySyncSend,
            $"p2p-registry-digest {peerThumbprint}",
            targetKey: peerThumbprint,
            groupKey: $"p2p-registry-send|{peerThumbprint}",
            work: async (ctx, ct) =>
            {
                ctx.Log($"digest={digest} repos={repoCount}");

                var outgoingRoot = Path.Combine(AppPaths.GetAppDataRoot(), "transfers", "outgoing");
                Directory.CreateDirectory(outgoingRoot);
                var fileName = $"repo-registry.digest.{self.CertificateThumbprint}.json";
                var filePath = Path.Combine(outgoingRoot, $"{Guid.NewGuid():N}.{fileName}");

                var payload = new DigestPayload(
                    FromThumbprint: self.CertificateThumbprint,
                    FromDeviceId: self.DeviceId,
                    FromDeviceName: self.DeviceName,
                    Digest: digest,
                    RepoCount: repoCount,
                    Utc: DateTimeOffset.UtcNow);

                await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }), ct);

                var send = await p2p.SendFileAsync(peerThumbprint, filePath, progress: null, ct);
                if (!send.IsSuccess || send.Value is null || !send.Value.Success)
                {
                    return Result.Fail(send.IsSuccess ? (send.Value?.Error ?? "send failed") : (send.Error ?? "send failed"));
                }

                try { File.Delete(filePath); } catch { }
                return Result.Ok();
            },
            maxAttempts: 3,
            dedupeKey: dk);
    }

    private void EnqueueSendSnapshot(string peerThumbprint, Core.P2p.PeerIdentity self, string localDigest)
    {
        var dk = $"p2p-registry-snapshot|{peerThumbprint}|{localDigest}";
        tasks.Enqueue(
            TaskKind.P2pRegistrySyncSend,
            $"p2p-registry-snapshot {peerThumbprint}",
            targetKey: peerThumbprint,
            groupKey: $"p2p-registry-send|{peerThumbprint}",
            work: async (ctx, ct) =>
            {
                var outgoingRoot = Path.Combine(AppPaths.GetAppDataRoot(), "transfers", "outgoing");
                Directory.CreateDirectory(outgoingRoot);
                var fileName = $"repo-registry.snapshot.{self.CertificateThumbprint}.json";
                var filePath = Path.Combine(outgoingRoot, $"{Guid.NewGuid():N}.{fileName}");

                ctx.Log("export snapshot");
                var export = await registry.ExportSnapshotAsync(filePath, ct);
                if (!export.IsSuccess) return Result.Fail(export.Error ?? "export failed");

                ctx.Log("send snapshot");
                var send = await p2p.SendFileAsync(peerThumbprint, filePath, progress: null, ct);
                if (!send.IsSuccess || send.Value is null || !send.Value.Success)
                {
                    return Result.Fail(send.IsSuccess ? (send.Value?.Error ?? "send failed") : (send.Error ?? "send failed"));
                }

                try { File.Delete(filePath); } catch { }
                return Result.Ok();
            },
            maxAttempts: 3,
            dedupeKey: dk);
    }

    private async Task ProcessIncomingAsync(CancellationToken cancellationToken)
    {
        var incomingRoot = Path.Combine(AppPaths.GetAppDataRoot(), "transfers", "incoming");
        if (!Directory.Exists(incomingRoot)) return;

        var processedRoot = Path.Combine(incomingRoot, "processed");
        Directory.CreateDirectory(processedRoot);

        foreach (var file in Directory.EnumerateFiles(incomingRoot, "repo-registry.digest.*.json", SearchOption.TopDirectoryOnly))
        {
            await ProcessDigestFileAsync(file, processedRoot, cancellationToken);
        }

        foreach (var file in Directory.EnumerateFiles(incomingRoot, "repo-registry.snapshot.*.json", SearchOption.TopDirectoryOnly))
        {
            await ProcessSnapshotFileAsync(file, processedRoot, cancellationToken);
        }
    }

    private async Task ProcessDigestFileAsync(string filePath, string processedRoot, CancellationToken cancellationToken)
    {
        string json;
        try
        {
            json = await File.ReadAllTextAsync(filePath, cancellationToken);
        }
        catch
        {
            return;
        }

        DigestPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<DigestPayload>(json);
        }
        catch
        {
            payload = null;
        }

        await MoveToProcessedAsync(filePath, processedRoot);
        if (payload is null) return;

        var statusRes = await p2p.GetStatusAsync(cancellationToken);
        if (!statusRes.IsSuccess || statusRes.Value is null) return;

        var reposRes = await registry.GetAllAsync(cancellationToken);
        if (!reposRes.IsSuccess || reposRes.Value is null) return;

        var localDigest = ComputeDigest(reposRes.Value);
        if (payload.Digest.Equals(localDigest, StringComparison.OrdinalIgnoreCase)) return;

        var self = statusRes.Value.Self;
        EnqueueSendSnapshot(payload.FromThumbprint, self, localDigest);
    }

    private Task ProcessSnapshotFileAsync(string filePath, string processedRoot, CancellationToken cancellationToken)
    {
        tasks.Enqueue(
            TaskKind.P2pRegistrySyncImport,
            $"p2p-registry-import {Path.GetFileName(filePath)}",
            targetKey: filePath,
            groupKey: "p2p-registry-import",
            work: async (ctx, ct) =>
            {
                ctx.Log("import snapshot");
                var res = await registry.ImportSnapshotAsync(filePath, ct);
                if (!res.IsSuccess) return Result.Fail(res.Error ?? "import failed");
                ctx.Log($"imported={res.Value}");
                await MoveToProcessedAsync(filePath, processedRoot);
                return Result.Ok();
            },
            maxAttempts: 1,
            dedupeKey: $"p2p-registry-import|{filePath}");

        return Task.CompletedTask;
    }

    private static Task MoveToProcessedAsync(string filePath, string processedRoot)
    {
        try
        {
            var dest = Path.Combine(processedRoot, Path.GetFileName(filePath));
            if (File.Exists(dest))
            {
                dest = Path.Combine(processedRoot, $"{Path.GetFileNameWithoutExtension(filePath)}.{Guid.NewGuid():N}{Path.GetExtension(filePath)}");
            }
            File.Move(filePath, dest);
        }
        catch
        {
        }

        return Task.CompletedTask;
    }

    private static string ComputeDigest(IReadOnlyList<RepoRegistryEntry> repos)
    {
        using var sha = SHA256.Create();
        var ordered = repos.OrderBy(r => r.Path, StringComparer.OrdinalIgnoreCase);
        foreach (var r in ordered)
        {
            var line = $"{r.Path}\u001f{r.OriginUrl}\u001f{r.GitHubFullName}\u001f{r.AddedUtc:O}\u001f{r.LastSeenUtc:O}\n";
            var bytes = Encoding.UTF8.GetBytes(line);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }
}
