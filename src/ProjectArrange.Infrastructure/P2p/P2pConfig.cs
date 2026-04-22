namespace ProjectArrange.Infrastructure.P2p;

public sealed class P2pConfig
{
    public int DiscoveryPort { get; set; } = 45454;
    public int? ListenPort { get; set; } = null;
    public int BroadcastIntervalMilliseconds { get; set; } = 2000;
    public int PeerTtlMilliseconds { get; set; } = 12_000;
    public int FileChunkBytes { get; set; } = 262_144;
    public long? MaxSendBytesPerSecond { get; set; } = null;
}
