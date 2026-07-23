using Microsoft.Data.Sqlite;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.State;

namespace OneDriveServerTransfer.Tests.Destination;

/// <summary>
/// Verifies the SQLite-backed path collision registry: persistence and reuse across
/// "restarts", ordinal case-insensitive lookups, and unchanged PathMapperV1 collision
/// behavior against the persisted seam (contract section 11, rule 10).
/// </summary>
public class SqlitePathCollisionRegistryTests : IDisposable
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"odst-m5-reg-{Guid.NewGuid():N}", "TransferState.db");

    private SqlitePathCollisionRegistry CreateOpenRegistry()
    {
        var registry = new SqlitePathCollisionRegistry(new SqliteTransferStateSchemaInitializer());
        registry.Open(_databasePath);
        return registry;
    }

    [Fact]
    public void RegisterAndFindRoundTrip()
    {
        var registry = CreateOpenRegistry();

        registry.Register("parent", "report.txt", new PathCollisionEntry("item-1", MappedItemKind.File));

        var found = registry.Find("parent", "report.txt");
        Assert.NotNull(found);
        Assert.Equal("item-1", found!.SourceItemId);
        Assert.Equal(MappedItemKind.File, found.Kind);
        Assert.Null(registry.Find("parent", "other.txt"));
        Assert.Null(registry.Find("other-parent", "report.txt"));
    }

    [Fact]
    public void FindIsOrdinalCaseInsensitive()
    {
        var registry = CreateOpenRegistry();

        registry.Register("parent", "Report.TXT", new PathCollisionEntry("item-1", MappedItemKind.File));

        Assert.NotNull(registry.Find("parent", "REPORT.txt"));
        Assert.NotNull(registry.Find("parent", "report.txt"));
    }

    [Fact]
    public void MappingsSurviveARestart()
    {
        CreateOpenRegistry().Register("parent", "report.txt",
            new PathCollisionEntry("item-1", MappedItemKind.File));

        // A new instance over the same database is a process restart for the registry.
        var restarted = CreateOpenRegistry();

        var found = restarted.Find("parent", "report.txt");
        Assert.NotNull(found);
        Assert.Equal("item-1", found!.SourceItemId);
        Assert.Equal("report.txt", restarted.FindMappedNameByItemId("parent", "item-1"));
    }

    [Fact]
    public void PathMapperV1ReusesPersistedMappingsAfterARestart()
    {
        // First run: two colliding names force a deterministic suffix on the second.
        var mapper1 = new PathMapperV1(CreateOpenRegistry());
        var first = mapper1.Map(new PathMapRequest("", "report.txt", "item-1", MappedItemKind.File));
        var second = mapper1.Map(new PathMapRequest("", "report.txt", "item-2", MappedItemKind.File));

        Assert.False(first.UsedCollisionSuffix);
        Assert.True(second.UsedCollisionSuffix);

        // After a restart the same source items map to the exact same names. A
        // naturally mapped item reports no suffix; the collided item deterministically
        // reuses its suffixed name (M4 semantics: reuse of a suffixed mapping reports
        // UsedCollisionSuffix = true).
        var mapper2 = new PathMapperV1(CreateOpenRegistry());
        var firstAgain = mapper2.Map(new PathMapRequest("", "report.txt", "item-1", MappedItemKind.File));
        var secondAgain = mapper2.Map(new PathMapRequest("", "report.txt", "item-2", MappedItemKind.File));

        Assert.Equal(first.MappedName, firstAgain.MappedName);
        Assert.Equal(second.MappedName, secondAgain.MappedName);
        Assert.False(firstAgain.UsedCollisionSuffix);
        Assert.True(secondAgain.UsedCollisionSuffix);
    }

    [Fact]
    public void FileVersusFolderConflictIsACollision()
    {
        var mapper = new PathMapperV1(CreateOpenRegistry());

        mapper.Map(new PathMapRequest("", "name", "item-file", MappedItemKind.File));
        var folder = mapper.Map(new PathMapRequest("", "name", "item-folder", MappedItemKind.Folder));

        Assert.True(folder.UsedCollisionSuffix);
    }

    [Fact]
    public void UseBeforeOpenThrows()
    {
        var registry = new SqlitePathCollisionRegistry(new SqliteTransferStateSchemaInitializer());

        Assert.Throws<InvalidOperationException>(() => registry.Find("parent", "name"));
        Assert.Throws<InvalidOperationException>(() =>
            registry.Register("parent", "name", new PathCollisionEntry("item", MappedItemKind.File)));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        var directory = Path.GetDirectoryName(_databasePath);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
