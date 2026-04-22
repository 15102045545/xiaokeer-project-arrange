using ProjectArrange.Infrastructure.Git;
using ProjectArrange.Infrastructure.P2p;
using ProjectArrange.Infrastructure.Storage;

namespace ProjectArrange.Tests;

public class UnitTest1
{
    [Fact]
    public void GitStatusParser_parses_branch_and_counts()
    {
        var output = """
                     ## main...origin/main [ahead 2, behind 1]
                     M  a.txt
                      M b.txt
                     ?? c.txt
                     """;

        var (branch, tree) = GitStatusParser.ParsePorcelainV1(output);

        Assert.Equal("main", branch.Branch);
        Assert.True(branch.HasUpstream);
        Assert.Equal(2, branch.Ahead);
        Assert.Equal(1, branch.Behind);

        Assert.False(tree.IsClean);
        Assert.Equal(1, tree.StagedChanges);
        Assert.Equal(1, tree.UnstagedChanges);
        Assert.Equal(1, tree.UntrackedFiles);
    }

    [Fact]
    public async Task SqliteDatabaseMigrator_applies_sql_scripts_in_order()
    {
        var root = Path.Combine(Path.GetTempPath(), "projectarrange-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var migrations = Path.Combine(root, "migrations");
        Directory.CreateDirectory(migrations);

        await File.WriteAllTextAsync(Path.Combine(migrations, "0001_init.sql"), "CREATE TABLE IF NOT EXISTS T1(Id INTEGER PRIMARY KEY);");
        await File.WriteAllTextAsync(Path.Combine(migrations, "0002_add.sql"), "CREATE TABLE IF NOT EXISTS T2(Id INTEGER PRIMARY KEY);");

        var dbPath = Path.Combine(root, "test.db");
        var migrator = new SqliteDatabaseMigrator(dbPath: dbPath, migrationsRoot: migrations);

        var r = await migrator.MigrateAsync();
        Assert.True(r.IsSuccess, r.Error);

        var v = await migrator.GetSchemaVersionAsync();
        Assert.True(v.IsSuccess, v.Error);
        Assert.Equal(2, v.Value);
    }

    [Fact]
    public void P2pPairingCodec_roundtrip()
    {
        var code = P2pPairingCodec.Encode("dev1", "pc1", "aa bb cc");
        Assert.True(P2pPairingCodec.TryDecode(code.Value, out var payload));
        Assert.Equal("dev1", payload.DeviceId);
        Assert.Equal("pc1", payload.DeviceName);
        Assert.Equal("AABBCC", payload.Thumbprint);
    }
}
