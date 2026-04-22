using Microsoft.Data.Sqlite;
using ProjectArrange.Core;
using ProjectArrange.Core.Abstractions;
using ProjectArrange.Infrastructure.Configuration;

namespace ProjectArrange.Infrastructure.Presence;

public sealed class SqlitePresenceService(MachineIdentityProvider identityProvider) : IPresenceService
{
    private readonly string _dbPath = AppPaths.GetDatabasePath();

    public async Task<Result<MachinePresence>> PublishHeartbeatAsync(string? note = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var me = new MachinePresence(
                MachineId: identityProvider.GetMachineId(),
                MachineName: identityProvider.GetMachineName(),
                LastSeenUtc: DateTimeOffset.UtcNow,
                Note: note);

            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
            await using var conn = new SqliteConnection($"Data Source={_dbPath}");
            await conn.OpenAsync(cancellationToken);

            var cmd = conn.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO MachineHeartbeat(MachineId, MachineName, LastSeenUtc, Note)
                              VALUES ($id, $name, $ts, $note)
                              ON CONFLICT(MachineId) DO UPDATE SET
                                MachineName=excluded.MachineName,
                                LastSeenUtc=excluded.LastSeenUtc,
                                Note=excluded.Note;
                              """;
            cmd.Parameters.AddWithValue("$id", me.MachineId);
            cmd.Parameters.AddWithValue("$name", me.MachineName);
            cmd.Parameters.AddWithValue("$ts", me.LastSeenUtc.ToString("O"));
            cmd.Parameters.AddWithValue("$note", (object?)me.Note ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            return Result<MachinePresence>.Ok(me);
        }
        catch (Exception ex)
        {
            return Result<MachinePresence>.Fail(ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<MachinePresence>>> GetPeersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_dbPath)) return Result<IReadOnlyList<MachinePresence>>.Ok(Array.Empty<MachinePresence>());
            await using var conn = new SqliteConnection($"Data Source={_dbPath}");
            await conn.OpenAsync(cancellationToken);

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT MachineId, MachineName, LastSeenUtc, Note FROM MachineHeartbeat ORDER BY LastSeenUtc DESC;";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            var list = new List<MachinePresence>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetString(0);
                var name = reader.GetString(1);
                var ts = DateTimeOffset.Parse(reader.GetString(2));
                var note = reader.IsDBNull(3) ? null : reader.GetString(3);
                list.Add(new MachinePresence(id, name, ts, note));
            }

            return Result<IReadOnlyList<MachinePresence>>.Ok(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<MachinePresence>>.Fail(ex.Message);
        }
    }
}
