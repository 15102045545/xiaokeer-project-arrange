namespace ProjectArrange.Core.Abstractions;

public sealed record MachinePresence(
    string MachineId,
    string MachineName,
    DateTimeOffset LastSeenUtc,
    string? Note);

public interface IPresenceService
{
    Task<Result<MachinePresence>> PublishHeartbeatAsync(string? note = null, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MachinePresence>>> GetPeersAsync(CancellationToken cancellationToken = default);
}
