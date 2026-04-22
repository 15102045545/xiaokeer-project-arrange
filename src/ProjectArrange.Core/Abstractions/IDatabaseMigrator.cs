namespace ProjectArrange.Core.Abstractions;

public interface IDatabaseMigrator
{
    Task<Result> MigrateAsync(CancellationToken cancellationToken = default);
    Task<Result<long>> GetSchemaVersionAsync(CancellationToken cancellationToken = default);
}
