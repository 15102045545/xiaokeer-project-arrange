namespace ProjectArrange.Core.Abstractions;

public sealed record GhAuthStatus(
    bool IsInstalled,
    bool IsLoggedIn,
    string? User,
    string RawOutput);

public interface IGhCliService
{
    Task<Result<GhAuthStatus>> GetAuthStatusAsync(CancellationToken cancellationToken = default);
}
