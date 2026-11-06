using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DnD.CharSheet.Infrastructure.Tests;

[TestClass]
public sealed class AdventureTests
{
    private string root = null!;
    private string Data => Path.Combine(root, "data");
    private CharacterDatabase Database => new(Path.Combine(Data, "characters.db"));
    private SqliteAdventureRepository Adventures => new(Database);
    [TestInitialize] public void Setup() { root = Path.Combine(Path.GetTempPath(), "charsheet-adventures", Guid.NewGuid().ToString("N")); Database.Initialize(); }
    [TestCleanup] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private void Sql(string sql) { using var c = Database.OpenConnection(); using var cmd = c.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery(); }

    [TestMethod]
    public async Task MigrationPreservesLegacyStateAndSnapshotWithoutInventingMembership()
    {
        var id = CharacterId.New();
        await new SqliteCharacterLibrary(Database).CreateDraftAsync(id, "Legacy");
        await new SqliteCharacterRepository(Database).SaveAsync(new(id, new(0)));
        await new FileDraftRecovery(Path.Combine(Data, "draft-recovery.json")).SaveAsync(new("Unfinished", id));
        Sql("DROP TABLE adventures; DROP TABLE adventure_memberships; PRAGMA user_version=3;");
        var snapshot = Database.Initialize();
        Assert.IsNotNull(snapshot);
        using (var c = new SqliteConnection($"Data Source={snapshot};Pooling=False"))
        {
            c.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "PRAGMA user_version"; Assert.AreEqual(3L, cmd.ExecuteScalar());
        }
        Assert.IsNull(Database.Initialize());
        var character = (await new SqliteCharacterLibrary(Database).ListAsync()).Single();
        Assert.AreEqual(id, character.Id); Assert.AreEqual(1L, character.Revision.Value); Assert.AreEqual("Legacy", character.Name);
        var membership = await Adventures.GetMembershipAsync(id);
        Assert.IsNull(membership.AdventureId);
        Assert.ThrowsExactly<InvalidOperationException>(() => membership.RequireUsableIn(AdventureId.New()));
        Assert.AreEqual(id, (await new FileDraftRecovery(Path.Combine(Data, "draft-recovery.json")).LoadAsync())!.SubmittedId);
        Assert.HasCount(0, await Adventures.ListAsync());
    }

    [TestMethod]
    public async Task CreateAssignDeleteRetainsCharactersAndRejectsStaleOrMissingAssignments()
    {
        var a = AdventureId.New(); var b = AdventureId.New(); var first = CharacterId.New(); var second = CharacterId.New();
        await Adventures.CreateWithDraftAsync(a, " Same ", first, "First");
        await new SqliteCharacterLibrary(Database).CreateDraftAsync(second, "Second");
        await Adventures.AssignAsync(second, a);
        (await Adventures.GetMembershipAsync(first)).RequireUsableIn(a);
        Assert.ThrowsExactly<InvalidOperationException>(() => new CharacterMembership(first, a).RequireUsableIn(b));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Adventures.CreateWithUnassignedAsync(b, "Same", first));
        Assert.HasCount(1, await Adventures.ListAsync());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Adventures.AssignAsync(second, a));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Adventures.AssignAsync(CharacterId.New(), b));
        await Adventures.DeleteAsync(a);
        Assert.IsNull((await Adventures.GetMembershipAsync(first)).AdventureId);
        Assert.HasCount(2, await new SqliteCharacterLibrary(Database).ListAsync());
        await Adventures.CreateWithUnassignedAsync(b, "Same", first);
        await Adventures.RenameAsync(b, "Renamed");
        Assert.AreEqual("Renamed", (await Adventures.ListAsync()).Single().Name);
        Assert.AreEqual(b, (await new SqliteAdventureRepository(new(Path.Combine(Data, "characters.db"))).GetMembershipAsync(first)).AdventureId);
    }

    [TestMethod]
    public async Task DatabaseRejectsEmptyAdventureAndLastMemberRemovalAndRollsBackNewDraft()
    {
        var a = AdventureId.New(); var first = CharacterId.New();
        Assert.ThrowsExactly<SqliteException>(() => Sql($"INSERT INTO adventures VALUES ('{a}','Empty','{first}')"));
        await Adventures.CreateWithDraftAsync(a, "Adventure", first, "First");
        Assert.ThrowsExactly<SqliteException>(() => Sql("DELETE FROM adventure_memberships"));
        Assert.ThrowsExactly<SqliteException>(() => Sql("DELETE FROM characters"));
        var orphan = CharacterId.New();
        await Assert.ThrowsExactlyAsync<SqliteException>(() => Adventures.CreateWithDraftAsync(a, "Duplicate", orphan, "Rollback"));
        Assert.IsNull(await new SqliteCharacterRepository(Database).GetAsync(orphan));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => Adventures.DeleteAsync(a, cancelled.Token));
        Assert.AreEqual(a, (await Adventures.GetMembershipAsync(first)).AdventureId);
    }

    [TestMethod]
    public async Task BackupRestoresMembershipAndRejectsOldSchemaAndBrokenReferences()
    {
        var a = AdventureId.New(); var first = CharacterId.New();
        await Adventures.CreateWithDraftAsync(a, "Adventure", first, "First");
        var backup = new FileBackupService(Data); var archive = Path.Combine(root, "backup.zip");
        await backup.CreateAsync(archive);
        using var preview = await backup.PreviewAsync(archive);
        await Adventures.DeleteAsync(a);
        await backup.RestoreAsync(preview);
        Assert.AreEqual(a, (await Adventures.GetMembershipAsync(first)).AdventureId);
        Sql("PRAGMA foreign_keys=OFF; UPDATE adventure_memberships SET adventure_id='missing';");
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => backup.CreateAsync(Path.Combine(root, "invalid.zip")));
        Sql("PRAGMA foreign_keys=OFF; DROP TABLE adventures; DROP TABLE adventure_memberships; PRAGMA user_version=3;");
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => backup.CreateAsync(Path.Combine(root, "old.zip")));
    }
}

