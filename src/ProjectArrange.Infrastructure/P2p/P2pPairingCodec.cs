using System.Text;
using System.Text.Json;
using ProjectArrange.Core.P2p;

namespace ProjectArrange.Infrastructure.P2p;

public static class P2pPairingCodec
{
    private sealed record PairingPayload(string DeviceId, string DeviceName, string CertificateThumbprint);

    public static PairingCode Encode(string deviceId, string deviceName, string thumbprint)
    {
        var json = JsonSerializer.Serialize(new PairingPayload(deviceId, deviceName, thumbprint));
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        return new PairingCode(b64);
    }

    public static bool TryDecode(string code, out (string DeviceId, string DeviceName, string Thumbprint) payload)
    {
        payload = default;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(code.Trim()));
            var p = JsonSerializer.Deserialize<PairingPayload>(json);
            if (p is null) return false;
            if (string.IsNullOrWhiteSpace(p.DeviceId)) return false;
            if (string.IsNullOrWhiteSpace(p.DeviceName)) return false;
            if (string.IsNullOrWhiteSpace(p.CertificateThumbprint)) return false;
            payload = (p.DeviceId, p.DeviceName, NormalizeThumbprint(p.CertificateThumbprint));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string NormalizeThumbprint(string thumbprint) =>
        new string(thumbprint.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
