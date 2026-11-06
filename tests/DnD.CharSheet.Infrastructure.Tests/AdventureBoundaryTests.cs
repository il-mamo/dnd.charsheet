using System.IO.Compression;
using System.Text.Json;
using DnD.CharSheet.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DnD.CharSheet.Infrastructure.Tests;

[TestClass]
public sealed class AdventureBoundaryTests
{
    private string root = null!;
    private string Data => Path.Combine(root, "data");
    private CharacterDatabase Database => new(Path.Combine(Data, "characters.db"));
    [TestInitialize] public void Setup() { root = Path.Combine(Path.GetTempPath(), "adventure-review", Guid.NewGuid().ToString("N")); Database.Initialize(); }
    [TestCleanup] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private void Sql(string sql) { using var c = Database.OpenConnection(); using var cmd = c.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery(); }

    [TestMethod]
    public async Task FailedVersionThreeMigrationPreservesPriorDataVersionAssetsAndRecovery()
    {
        var character = CharacterId.New();
        await new SqliteCharacterLibrary(Database).CreateDraftAsync(character, "Preserve");
        var portraitDirectory = Path.Combine(Data, "portraits"); Directory.CreateDirectory(portraitDirectory);
        var asset = Path.Combine(portraitDirectory, "sentinel.png"); File.WriteAllBytes(asset, [1,2,3]);
        var recovery = Path.Combine(Data, "draft-recovery.json"); File.WriteAllText(recovery, "sentinel recovery");
        Sql("DROP TABLE adventures; DROP TABLE adventure_memberships; PRAGMA user_version=3; CREATE TABLE adventure_memberships(sentinel TEXT); INSERT INTO adventure_memberships VALUES ('prior');");
        Assert.ThrowsExactly<SqliteException>(() => Database.Initialize());
        using var c = Database.OpenConnection(); using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA user_version"; Assert.AreEqual(3L, cmd.ExecuteScalar());
        cmd.CommandText = "SELECT count(*) FROM sqlite_schema WHERE name='adventures'"; Assert.AreEqual(0L, cmd.ExecuteScalar());
        cmd.CommandText = "SELECT sentinel FROM adventure_memberships"; Assert.AreEqual("prior", cmd.ExecuteScalar());
        Assert.AreEqual(character, (await new SqliteCharacterLibrary(Database).ListAsync()).Single().Id);
        CollectionAssert.AreEqual(new byte[] {1,2,3}, File.ReadAllBytes(asset));
        Assert.AreEqual("sentinel recovery", File.ReadAllText(recovery));
        Assert.HasCount(1, Directory.GetFiles(Path.Combine(Data, "migration-backups")));
    }

    [TestMethod]
    public async Task ActualSchemaThreeArchiveRejectedBeforeReplacingCurrentData()
    {
        var character = CharacterId.New(); await new SqliteCharacterLibrary(Database).CreateDraftAsync(character, "Current");
        var archive = Path.Combine(root, "old.zip"); var oldDatabase = Path.Combine(root, "old.db");
        Sql("DROP TABLE adventures; DROP TABLE adventure_memberships; PRAGMA user_version=3;");
        using (var source = Database.OpenConnection())
        using (var target = new SqliteConnection($"Data Source={oldDatabase};Pooling=False")) { target.Open(); source.BackupDatabase(target); }
        Database.Initialize();
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(oldDatabase, "characters.db");
            using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open());
            writer.Write(JsonSerializer.Serialize(new { Version=1, SchemaVersion=3, CreatedUtc=DateTimeOffset.UtcNow }));
        }
        var before = File.ReadAllBytes(Path.Combine(Data, "characters.db"));
        var error = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new FileBackupService(Data).PreviewAsync(archive));
        StringAssert.Contains(error.Message, "compatible application version");
        CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(Data, "characters.db")));
    }

    [TestMethod]
    public async Task ConcurrentAssignmentHasOneWinnerAndMissingDestinationRollsBack()
    {
        var repository = new SqliteAdventureRepository(Database);
        var a = AdventureId.New(); var b = AdventureId.New();
        await repository.CreateWithDraftAsync(a, "Same name", CharacterId.New(), "A");
        await repository.CreateWithDraftAsync(b, "Same name", CharacterId.New(), "B");
        var character = CharacterId.New(); await new SqliteCharacterLibrary(Database).CreateDraftAsync(character, "Assign");
        await Assert.ThrowsExactlyAsync<SqliteException>(() => repository.AssignAsync(character, AdventureId.New()));
        Assert.IsNull((await repository.GetMembershipAsync(character)).AdventureId);
        async Task<bool> Assign(AdventureId id) { try { await new SqliteAdventureRepository(Database).AssignAsync(character, id); return true; } catch (InvalidOperationException) { return false; } }
        var results = await Task.WhenAll(Assign(a), Assign(b));
        Assert.AreEqual(1, results.Count(x => x));
        Assert.IsNotNull((await repository.GetMembershipAsync(character)).AdventureId);
        Assert.HasCount(2, await repository.ListAsync());
    }

    [TestMethod]
    public void DomainIdentityAndUseGuardRejectInvalidStates()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new AdventureId(Guid.Empty));
        var id = AdventureId.New(); Assert.AreEqual(id, new AdventureId(Guid.Parse(id.ToString())));
        var character = CharacterId.New();
        Assert.ThrowsExactly<InvalidOperationException>(() => new CharacterMembership(character, null).RequireUsableIn(id));
        Assert.ThrowsExactly<InvalidOperationException>(() => new CharacterMembership(character, AdventureId.New()).RequireUsableIn(id));
        new CharacterMembership(character, id).RequireUsableIn(id);
    }
}
