using ProjectArrange.Core.Repos;

namespace ProjectArrange.Core.Abstractions;

public interface IRepoRegistryService
{
    Task<Result<IReadOnlyList<RepoRegistryEntry>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result> UpsertAsync(RepoRegistryEntry entry, CancellationToken cancellationToken = default);
    Task<Result> RemoveAsync(string path, CancellationToken cancellationToken = default);

    Task<Result> SaveGitSnapshotAsync(RepoGitSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<Result<RepoGitSnapshot?>> GetLastGitSnapshotAsync(string path, CancellationToken cancellationToken = default);

    Task<Result> SaveSecretScanSnapshotAsync(RepoSecretScanSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<Result<RepoSecretScanSnapshot?>> GetLastSecretScanSnapshotAsync(string path, CancellationToken cancellationToken = default);

    Task<Result> ExportSnapshotAsync(string filePath, CancellationToken cancellationToken = default);
    Task<Result<int>> ImportSnapshotAsync(string filePath, CancellationToken cancellationToken = default);
}
