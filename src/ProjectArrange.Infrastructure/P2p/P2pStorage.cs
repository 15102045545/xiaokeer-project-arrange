using Microsoft.Data.Sqlite;
using ProjectArrange.Infrastructure.Configuration;

namespace ProjectArrange.Infrastructure.P2p;

public sealed class P2pStorage
{
    private readonly string _dbPath;

    public P2pStorage(string? dbPath = null)
    {
        _dbPath = dbPath ?? AppPaths.GetDatabasePath();
    }

    public async Task EnsureAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync(cancellationToken);
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
                          CREATE TABLE IF NOT EXISTS TrustedPeer(
                            CertificateThumbprint TEXT NOT NULL PRIMARY KEY,
                            DeviceId TEXT NOT NULL,
                            DeviceName TEXT NOT NULL,
                            AddedUtc TEXT NOT NULL
                          );
                          """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpsertTrustedPeerAsync(string thumbprint, string deviceId, string deviceName, CancellationToken cancellationToken)
    {
        await EnsureAsync(cancellationToken);
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync(cancellationToken);

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
                          INSERT INTO TrustedPeer(CertificateThumbprint, DeviceId, DeviceName, AddedUtc)
                          VALUES ($t, $id, $name, $utc)
                          ON CONFLICT(CertificateThumbprint) DO UPDATE SET
                            DeviceId=excluded.DeviceId,
                            DeviceName=excluded.DeviceName;
                          """;
        cmd.Parameters.AddWithValue("$t", thumbprint);
        cmd.Parameters.AddWithValue("$id", deviceId);
        cmd.Parameters.AddWithValue("$name", deviceName);
        cmd.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(string Thumbprint, string DeviceId, string DeviceName)>> GetTrustedPeersAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_dbPath)) return Array.Empty<(string, string, string)>();
        await EnsureAsync(cancellationToken);

        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync(cancellationToken);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CertificateThumbprint, DeviceId, DeviceName FROM TrustedPeer;";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var list = new List<(string, string, string)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        return list;
    }
}
