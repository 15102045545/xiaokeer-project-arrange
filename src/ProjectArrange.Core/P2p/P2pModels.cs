namespace ProjectArrange.Core.P2p;

public sealed record PeerIdentity(
    string DeviceId,
    string DeviceName,
    string CertificateThumbprint);

public sealed record PeerEndpoint(
    string DeviceId,
    string DeviceName,
    string CertificateThumbprint,
    string Ip,
    int Port,
    DateTimeOffset LastSeenUtc);

public sealed record PairingCode(string Value);

public sealed record P2pConnection(
    PeerIdentity Peer,
    DateTimeOffset ConnectedUtc,
    DateTimeOffset LastSeenUtc);
