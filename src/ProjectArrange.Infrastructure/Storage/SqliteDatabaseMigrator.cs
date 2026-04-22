using Microsoft.Data.Sqlite;
using ProjectArrange.Core;
using ProjectArrange.Core.Abstractions;
using ProjectArrange.Infrastructure.Configuration;

namespace ProjectArrange.Infrastructure.Storage;

public sealed class SqliteDatabaseMigrator : IDatabaseMigrator
{
    private readonly string _dbPath;
    private readonly string _migrationsRoot;

    public SqliteDatabaseMigrator(string? dbPath = null, string? migrationsRoot = null)
    {
        _dbPath = dbPath ?? AppPaths.GetDatabasePath();
        _migrationsRoot = migrationsRoot ?? AppPaths.GetMigrationsRoot();
    }

    public async Task<Result> MigrateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
            await using var conn = new SqliteConnection($"Data Source={_dbPath}");
            await conn.OpenAsync(cancellationToken);

            await EnsureSchemaTableAsync(conn, cancellationToken);
            var current = await GetSchemaVersionInternalAsync(conn, cancellationToken);

            if (!Directory.Exists(_migrationsRoot)) return Result.Fail($"Migrations directory not found: {_migrationsRoot}");

            var scripts = Directory.EnumerateFiles(_migrationsRoot, "*.sql", SearchOption.TopDirectoryOnly)
                .Select(p => new { Path = p, Version = ParseVersion(Path.GetFileName(p)) })
                .Where(x => x.Version > 0)
                .OrderBy(x => x.Version)
                .ToArray();

            foreach (var script in scripts.Where(s => s.Version > current))
            {
                var sql = await File.ReadAllTextAsync(script.Path, cancellationToken);
                await using var tx = conn.BeginTransaction();

                foreach (var statement in SplitSqlStatements(sql))
                {
                    if (string.IsNullOrWhiteSpace(statement)) continue;
                    var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = statement;
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                var upCmd = conn.CreateCommand();
                upCmd.Transaction = tx;
                upCmd.CommandText = "INSERT INTO SchemaVersion(Version, AppliedUtc) VALUES ($v, $t);";
                upCmd.Parameters.AddWithValue("$v", script.Version);
                upCmd.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow.ToString("O"));
                await upCmd.ExecuteNonQueryAsync(cancellationToken);

                await tx.CommitAsync(cancellationToken);
                current = script.Version;
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public async Task<Result<long>> GetSchemaVersionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_dbPath)) return Result<long>.Ok(0);
            await using var conn = new SqliteConnection($"Data Source={_dbPath}");
            await conn.OpenAsync(cancellationToken);
            await EnsureSchemaTableAsync(conn, cancellationToken);
            var v = await GetSchemaVersionInternalAsync(conn, cancellationToken);
            return Result<long>.Ok(v);
        }
        catch (Exception ex)
        {
            return Result<long>.Fail(ex.Message);
        }
    }

    private static async Task EnsureSchemaTableAsync(SqliteConnection conn, CancellationToken cancellationToken)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
                          CREATE TABLE IF NOT EXISTS SchemaVersion(
                            Version INTEGER NOT NULL,
                            AppliedUtc TEXT NOT NULL
                          );
                          """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> GetSchemaVersionInternalAsync(SqliteConnection conn, CancellationToken cancellationToken)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM SchemaVersion;";
        var scalar = await cmd.ExecuteScalarAsync(cancellationToken);
        return scalar is long l ? l : Convert.ToInt64(scalar);
    }

    private static int ParseVersion(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return 0;
        var digits = new string(fileName.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var v) ? v : 0;
    }

    private static IEnumerable<string> SplitSqlStatements(string sql)
    {
        return sql.Split(';')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0);
    }
}
