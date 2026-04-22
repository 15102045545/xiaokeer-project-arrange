using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ProjectArrange.Infrastructure.Configuration;

namespace ProjectArrange.Infrastructure.P2p;

public sealed class P2pIdentityProvider
{
    private readonly string _certPath;
    private readonly string _deviceIdPath;

    public P2pIdentityProvider(string? certPath = null, string? deviceIdPath = null)
    {
        var root = AppPaths.GetAppDataRoot();
        Directory.CreateDirectory(root);
        _certPath = certPath ?? Path.Combine(root, "p2p_cert.pfx");
        _deviceIdPath = deviceIdPath ?? Path.Combine(root, "p2p_device_id.txt");
    }

    public string GetOrCreateDeviceId()
    {
        if (File.Exists(_deviceIdPath))
        {
            var id = File.ReadAllText(_deviceIdPath).Trim();
            if (!string.IsNullOrWhiteSpace(id)) return id;
        }

        var created = Guid.NewGuid().ToString("N");
        File.WriteAllText(_deviceIdPath, created);
        return created;
    }

    public string GetDeviceName() => Environment.MachineName;

    public X509Certificate2 GetOrCreateCertificate()
    {
        if (File.Exists(_certPath))
        {
            var bytes = File.ReadAllBytes(_certPath);
            return new X509Certificate2(bytes, (string?)null, X509KeyStorageFlags.Exportable);
        }

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest(
            $"CN=ProjectArrange-{GetOrCreateDeviceId()}",
            ecdsa,
            HashAlgorithmName.SHA256);

        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
        var pfx = cert.Export(X509ContentType.Pfx);
        File.WriteAllBytes(_certPath, pfx);

        return new X509Certificate2(pfx, (string?)null, X509KeyStorageFlags.Exportable);
    }
}
