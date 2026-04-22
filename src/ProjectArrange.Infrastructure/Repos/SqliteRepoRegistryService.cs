using System.Text.Json;
using Microsoft.Data.Sqlite;
using ProjectArrange.Core;
using ProjectArrange.Core.Abstractions;
using ProjectArrange.Core.Repos;
using ProjectArrange.Infrastructure.Configuration;

namespace ProjectArrange.Infrastructure.Repos;

public sealed class SqliteRepoRegistryService : IRepoRegistryService
{
    private readonly string _dbPath;

    public SqliteRepoRegistryService(string? dbPath = null)
    {
        _dbPath = dbPath ?? AppPaths.GetDatabasePath();
    }

    public async Task<Result<IReadOnlyList<RepoRegistryEntry>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_dbPath)) return Result<IReadOnlyList<RepoRegistryEntry>>.Ok(Array.Empty<RepoRegistryEntry>());
            await using var conn = new SqliteConnection($"Data Source={_dbPath}");
            await conn.OpenAsync(cancellationToken);

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Path, OriginUrl, GitHubFullName, AddedUtc, LastSeenUtc FROM RepoRegistry ORDER BY Path;";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var list = new List<RepoRegistryEntry>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var path = reader.GetString(0);
                var origin = reader.IsDBNull(1) ? null : reader.GetString(1);
                var gh = reader.IsDBNull(2) ? null : reader.GetString(2);
                var added = DateTimeOffset.Parse(reader.GetString(3));
                var seen = DateTimeOffset.Parse(reader.GetString(4));
                list.Add(new RepoRegistryEntry(path, origin, gh, added, seen));
            }
            return Result<IReadOnlyList<RepoRegistryEntry>>.Ok(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<RepoRegistryEntry>>.Fail(ex.Message);
        }
    }

    public async Task<Result> UpsertAsync(RepoRegistryEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
            await using var conn = new SqliteConnection($"Data Source={_dbPath}");
            await conn.OpenAsync(cancellationToken);

            var cmd = conn.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO RepoRegistry(Path, OriginUrl, GitHubFullName, AddedUtc, LastSeenUtc)
                              VALUES ($p, $o, $g, $a, $s)
                              ON CONFLICT(Path) DO UPDATE SET
                                OriginUrl=excluded.OriginUrl,
                                GitHubFullName=excluded.GitHubFullName,
                                LastSeenUtc=excluded.LastSeenUtc;
                              """;
            cmd.Parameters.AddWithValue("$p", entry.Path);
            cmd.Parameters.AddWithValue("$o", (object?)entry.OriginUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$g", (object?)entry.GitHubFullName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$a", entry.AddedUtc.ToString("O"));
            cmd.Parameters.AddWithValue("$s", entry.LastSeenUtc.ToString("O"));
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public async Task<Result> RemoveAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_dbPath)) return Result.Ok();
            await using var conn = new SqliteConnection($"Data Source={_dbPath}");
            await conn.OpenAsync(cancellationToken);

            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM RepoRegistry WHERE Path=$p;";
            cmd.Parameters.AddWithValue("$p", path);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public async Task<Result> SaveGitSnapshotAsync(RepoGitSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
            await using var conn = new SqliteConnection($"Data Source={_dbPath}");
            await conn.OpenAsync(cancellationToken);

            var cmd = conn.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO RepoGitSnapshot(Path, Branch, Ahead, Behind, IsClean, StagedChanges, UnstagedChanges, UntrackedFiles, Utc)
                              VALUES ($p, $b, $a, $bh, $c, $st, $us, $un, $utc)
                              ON CONFLICT(Path) DO UPDATE SET
                                Branch=excluded.Branch,
                                Ahead=excluded.Ahead,
                                Behind=excluded.Behind,
                                IsClean=excluded.IsClean,
                                StagedChanges=excluded.StagedChanges,
                                UnstagedChanges=excluded.UnstagedChanges,
                                UntrackedFiles=excluded.UntrackedFiles,
                                Utc=excluded.Utc;
                              """;
            cmd.Parameters.AddWithValue("$p", snapshot.Path);
            cmd.Parameters.AddWithValue("$b", snapshot.Branch);
            cmd.Parameters.AddWithValue("$a", snapshot.Ahead);
            cmd.Parameters.AddWithValue("$bh", snapshot.Behind);
            cmd.Parameters.AddWithValue("$c", snapshot.IsClean ? 1 : 0);
            cmd.Parameters.AddWithValue("$st", snapshot.StagedChanges);
            cmd.Parameters.AddWithValue("$us", snapshot.UnstagedChanges);
            cmd.Parameters.AddWithValue("$un", snapshot.UntrackedFiles);
            cmd.Parameters.AddWithValue("$utc", snapshot.Utc.ToString("O"));
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public async Task<Result<RepoGitSnapshot?>> GetLastGitSnapshotAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_dbPath)) return Result<RepoGitSnapshot?>.Ok(null);
            await using var conn = new SqliteConnection($"Data Source={_dbPath}");
            await conn.OpenAsync(cancellationToken);

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Path, Branch, Ahead, Behind, IsClean, StagedChanges, UnstagedChanges, UntrackedFiles, Utc FROM RepoGitSnapshot WHERE Path=$p;";
            cmd.Parameters.AddWithValue("$p", path);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return Result<RepoGitSnapshot?>.Ok(null);
            var snap = new RepoGitSnapshot(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4) != 0,
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                DateTimeOffset.Parse(reader.GetString(8)));
            return Result<RepoGitSnapshot?>.Ok(snap);
        }
        catch (Exception ex)
        {
            return Result<RepoGitSnapshot?>.Fail(ex.Message);
        }
    }

    public async Task<Result> SaveSecretScanSnapshotAsync(RepoSecretScanSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
            await using var conn = new SqliteConnection($"Data Source={_dbPath}");
            await conn.OpenAsync(cancellationToken);

            var cmd = conn.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO RepoSecretScanSnapshot(Path, ToolAvailable, FindingsCount, RawOutput, Utc)
                              VALUES ($p, $t, $c, $r, $utc)
                              ON CONFLICT(Path) DO UPDATE SET
                                ToolAvailable=excluded.ToolAvailable,
                                FindingsCount=excluded.FindingsCount,
                                RawOutput=excluded.RawOutput,
                                Utc=excluded.Utc;
                              """;
            cmd.Parameters.AddWithValue("$p", snapshot.Path);
            cmd.Parameters.AddWithValue("$t", snapshot.ToolAvailable ? 1 : 0);
            cmd.Parameters.AddWithValue("$c", snapshot.FindingsCount);
            cmd.Parameters.AddWithValue("$r", snapshot.RawOutput ?? "");
            cmd.Parameters.AddWithValue("$utc", snapshot.Utc.ToString("O"));
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public async Task<Result<RepoSecretScanSnapshot?>> GetLastSecretScanSnapshotAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_dbPath)) return Result<RepoSecretScanSnapshot?>.Ok(null);
            await using var conn = new SqliteConnection($"Data Source={_dbPath}");
            await conn.OpenAsync(cancellationToken);

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Path, ToolAvailable, FindingsCount, RawOutput, Utc FROM RepoSecretScanSnapshot WHERE Path=$p;";
            cmd.Parameters.AddWithValue("$p", path);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return Result<RepoSecretScanSnapshot?>.Ok(null);
            var snap = new RepoSecretScanSnapshot(
                reader.GetString(0),
                reader.GetInt32(1) != 0,
                reader.GetInt32(2),
                reader.GetString(3),
                DateTimeOffset.Parse(reader.GetString(4)));
            return Result<RepoSecretScanSnapshot?>.Ok(snap);
        }
        catch (Exception ex)
        {
            return Result<RepoSecretScanSnapshot?>.Fail(ex.Message);
        }
    }

    public async Task<Result> ExportSnapshotAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var reposRes = await GetAllAsync(cancellationToken);
            if (!reposRes.IsSuccess) return Result.Fail(reposRes.Error!);
            var snap = new RepoRegistrySnapshot(reposRes.Value!, DateTimeOffset.UtcNow);
            var json = JsonSerializer.Serialize(snap, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public async Task<Result<int>> ImportSnapshotAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var snap = JsonSerializer.Deserialize<RepoRegistrySnapshot>(json);
            if (snap is null) return Result<int>.Fail("Invalid snapshot.");

            var count = 0;
            foreach (var r in snap.Repos)
            {
                var up = await UpsertAsync(r with { LastSeenUtc = DateTimeOffset.UtcNow }, cancellationToken);
                if (up.IsSuccess) count++;
            }

            return Result<int>.Ok(count);
        }
        catch (Exception ex)
        {
            return Result<int>.Fail(ex.Message);
        }
    }
}

