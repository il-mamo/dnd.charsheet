using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DnD.CharSheet.Infrastructure.Tests;

[TestClass]
public sealed class CharacterLibraryTests
{
    private string directory = null!;
    private string DatabasePath => Path.Combine(directory, "characters.db");
    private CharacterDatabase database = null!;
    private SqliteCharacterLibrary library = null!;

    [TestInitialize]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "charsheet-library-tests", Guid.NewGuid().ToString("N"));
        database = new CharacterDatabase(DatabasePath);
        database.Initialize();
        library = new SqliteCharacterLibrary(database);
    }

    [TestCleanup]
    public void CleanUp() => Directory.Delete(directory, recursive: true);

    [TestMethod]
    public async Task InvalidNamesCannotCreateAnyIdentityOrMetadata()
    {
        foreach (var name in new string?[] { null, "", "   ", new('x', 81), "\tValid", "Valid\n", "A\0B", "A\u007fB", "A\u0085B" })
            await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await library.CreateDraftAsync(CharacterId.New(), name!));
        Assert.HasCount(0, await library.ListAsync());
        Assert.AreEqual(0L, Scalar(database, "SELECT count(*) FROM characters;"));
        Assert.AreEqual(0L, Scalar(database, "SELECT count(*) FROM character_drafts;"));
    }

    [TestMethod]
    public async Task TrimmedBoundariesUnicodeAndDuplicateNamesSurviveReopen()
    {
        var names = new[] { "  X  ", " " + new string('x', 80) + " ", "\u00c9owyn  \u661f", string.Concat(Enumerable.Repeat("\U0001f600", 40)), "\u00c9owyn  \u661f" };
        var expected = new List<CharacterDraft>();
        foreach (var name in names)
            expected.Add(await library.CreateDraftAsync(CharacterId.New(), name));
        Assert.AreEqual(expected.Count, expected.Select(draft => draft.Id).Distinct().Count());
        Assert.IsTrue(expected.All(draft => draft.Revision.Value == 0));
        CollectionAssert.AreEqual(names.Select(name => name.Trim()).ToArray(), expected.Select(draft => draft.Name).ToArray());
        var reopened = new CharacterDatabase(DatabasePath);
        Assert.IsNull(reopened.Initialize());
        var actual = await new SqliteCharacterLibrary(reopened).ListAsync();
        CollectionAssert.AreEquivalent(expected.ToArray(), actual.ToArray());
    }

    [TestMethod]
    public async Task DuplicateIdAndMetadataFailureRollBackWithoutChangingExistingDrafts()
    {
        var original = await library.CreateDraftAsync(CharacterId.New(), "Original");
        await Assert.ThrowsExactlyAsync<CharacterConflictException>(() => library.CreateDraftAsync(original.Id, "Replacement"));
        Execute(database, "CREATE TRIGGER reject_draft BEFORE INSERT ON character_drafts BEGIN SELECT RAISE(ABORT, 'injected failure'); END;");
        var failedId = CharacterId.New();
        await Assert.ThrowsExactlyAsync<SqliteException>(() => library.CreateDraftAsync(failedId, "Failed"));
        Assert.IsNull(await new SqliteCharacterRepository(database).GetAsync(failedId));
        CollectionAssert.AreEqual(new[] { original }, (await library.ListAsync()).ToArray());
        Execute(database, "DROP TRIGGER reject_draft;");
        Assert.AreEqual(failedId, (await library.CreateDraftAsync(failedId, "Retry")).Id);
    }

    [TestMethod]
    public async Task PreCanceledOperationsDoNotMutateLibrary()
    {
        var original = await library.CreateDraftAsync(CharacterId.New(), "Original");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => library.CreateDraftAsync(CharacterId.New(), "Canceled", canceled.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => library.ListAsync(canceled.Token));
        CollectionAssert.AreEqual(new[] { original }, (await library.ListAsync()).ToArray());
    }

    [TestMethod]
    public async Task CompetingCreatesForSameIdHaveExactlyOneDurableWinner()
    {
        var id = CharacterId.New();
        async Task<bool> Attempt(SqliteCharacterLibrary writer, string name)
        {
            try { await writer.CreateDraftAsync(id, name); return true; }
            catch (CharacterConflictException) { return false; }
        }
        var results = await Task.WhenAll(Attempt(library, "First"), Attempt(new SqliteCharacterLibrary(database), "Second"));
        Assert.AreEqual(1, results.Count(result => result));
        var saved = await library.ListAsync();
        Assert.HasCount(1, saved);
        Assert.AreEqual(id, saved[0].Id);
        Assert.AreEqual(results[0] ? "First" : "Second", saved[0].Name);
    }

    [TestMethod]
    public async Task VersionOneUpgradeBacksUpAndPreservesLegacyIdentityAndRevision()
    {
        var path = Path.Combine(directory, "legacy.db");
        var id = CharacterId.New();
        using (var original = OpenRaw(path))
            Execute(original, $"CREATE TABLE characters(id TEXT PRIMARY KEY, revision INTEGER NOT NULL); INSERT INTO characters VALUES ('{id}', 17); PRAGMA user_version=1;");
        var upgraded = new CharacterDatabase(path);
        var backup = upgraded.Initialize();
        Assert.IsNotNull(backup);
        using (var snapshot = OpenRaw(backup))
        {
            Assert.AreEqual(1L, Scalar(snapshot, "PRAGMA user_version;"));
            Assert.AreEqual(17L, Scalar(snapshot, "SELECT revision FROM characters;"));
            Assert.AreEqual(0L, Scalar(snapshot, "SELECT count(*) FROM sqlite_master WHERE name='character_drafts';"));
        }
        var reader = new SqliteCharacterLibrary(upgraded);
        CollectionAssert.AreEqual(new[] { new CharacterDraft(id, new CharacterRevision(17), "Unnamed draft") }, (await reader.ListAsync()).ToArray());
        await reader.CreateDraftAsync(CharacterId.New(), "New draft");
        Assert.AreEqual(17L, (await new SqliteCharacterRepository(upgraded).GetAsync(id))!.Revision.Value);
        Assert.AreEqual((long)CharacterDatabase.CurrentVersion, Scalar(upgraded, "PRAGMA user_version;"));
        Assert.IsNull(upgraded.Initialize());
    }

    [TestMethod]
    public void FailedVersionOneUpgradeKeepsOriginalVersionDataAndReadableBackup()
    {
        var path = Path.Combine(directory, "blocked.db");
        using (var original = OpenRaw(path))
            Execute(original, "CREATE TABLE characters(id TEXT, revision INTEGER); INSERT INTO characters VALUES ('legacy', 23); CREATE TABLE character_drafts(existing TEXT); INSERT INTO character_drafts VALUES ('keep'); PRAGMA user_version=1;");
        Assert.ThrowsExactly<SqliteException>(() => new CharacterDatabase(path).Initialize());
        using var current = OpenRaw(path);
        Assert.AreEqual(1L, Scalar(current, "PRAGMA user_version;"));
        Assert.AreEqual(23L, Scalar(current, "SELECT revision FROM characters;"));
        Assert.AreEqual("keep", Scalar(current, "SELECT existing FROM character_drafts;"));
        var backups = Directory.GetFiles(Path.Combine(directory, "migration-backups"));
        Assert.HasCount(1, backups);
        using var snapshot = OpenRaw(backups[0]);
        Assert.AreEqual(1L, Scalar(snapshot, "PRAGMA user_version;"));
        Assert.AreEqual(23L, Scalar(snapshot, "SELECT revision FROM characters;"));
    }

    private static SqliteConnection OpenRaw(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    private static object? Scalar(CharacterDatabase source, string sql)
    {
        using var connection = source.OpenConnection();
        return Scalar(connection, sql);
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void Execute(CharacterDatabase source, string sql)
    {
        using var connection = source.OpenConnection();
        Execute(connection, sql);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
