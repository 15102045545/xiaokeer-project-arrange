namespace ProjectArrange.Core.Abstractions;

public sealed record UpdateInfo(
    string CurrentVersion,
    string LatestVersion,
    Uri? DownloadUrl,
    string RawJson);

public interface IUpdateService
{
    Task<Result<UpdateInfo>> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
}
