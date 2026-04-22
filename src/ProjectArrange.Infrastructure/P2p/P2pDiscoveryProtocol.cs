using System.Net;
using System.Text;
using System.Text.Json;
using ProjectArrange.Infrastructure.P2p;

namespace ProjectArrange.Infrastructure.P2p;

public static class P2pDiscoveryProtocol
{
    private sealed record DiscoveryMessage(
        string DeviceId,
        string DeviceName,
        string CertificateThumbprint,
        int ListenPort,
        string TimestampUtc);

    public static byte[] Encode(string deviceId, string deviceName, string thumbprint, int listenPort)
    {
        var msg = new DiscoveryMessage(
            deviceId,
            deviceName,
            P2pPairingCodec.NormalizeThumbprint(thumbprint),
            listenPort,
            DateTimeOffset.UtcNow.ToString("O"));

        var json = JsonSerializer.Serialize(msg);
        return Encoding.UTF8.GetBytes(json);
    }

    public static bool TryDecode(byte[] bytes, IPEndPoint remoteEndPoint, out (string DeviceId, string DeviceName, string Thumbprint, string Ip, int Port, DateTimeOffset SeenUtc) peer)
    {
        peer = default;
        try
        {
            var json = Encoding.UTF8.GetString(bytes);
            var msg = JsonSerializer.Deserialize<DiscoveryMessage>(json);
            if (msg is null) return false;
            if (string.IsNullOrWhiteSpace(msg.DeviceId)) return false;
            if (string.IsNullOrWhiteSpace(msg.DeviceName)) return false;
            if (string.IsNullOrWhiteSpace(msg.CertificateThumbprint)) return false;
            if (msg.ListenPort <= 0 || msg.ListenPort > 65535) return false;
            var ip = remoteEndPoint.Address.ToString();
            peer = (msg.DeviceId, msg.DeviceName, P2pPairingCodec.NormalizeThumbprint(msg.CertificateThumbprint), ip, msg.ListenPort, DateTimeOffset.UtcNow);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
