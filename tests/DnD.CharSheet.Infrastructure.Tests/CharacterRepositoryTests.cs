using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DnD.CharSheet.Infrastructure.Tests;

[TestClass]
public sealed class CharacterRepositoryTests
{
    private string directory = null!;
    private CharacterDatabase database = null!;
    private SqliteCharacterRepository repository = null!;

    [TestInitialize]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "charsheet-tests", Guid.NewGuid().ToString("N"));
        database = new CharacterDatabase(Path.Combine(directory, "characters.db"));
        database.Initialize();
        repository = new SqliteCharacterRepository(database);
    }

    [TestCleanup]
    public void CleanUp() => Directory.Delete(directory, recursive: true);

    [TestMethod]
    public async Task RoundTripAcrossReopenPreservesSeparateCharacters()
    {
        var first = await repository.CreateAsync(CharacterId.New());
        var second = await repository.CreateAsync(CharacterId.New());
        var saved = await repository.SaveAsync(first);
        Assert.AreEqual(1L, saved.Revision.Value);
        var reopened = new CharacterDatabase(Path.Combine(directory, "characters.db"));
        reopened.Initialize();
        var reader = new SqliteCharacterRepository(reopened);
        Assert.AreEqual(saved, await reader.GetAsync(first.Id));
        Assert.AreEqual(second, await reader.GetAsync(second.Id));
        Assert.IsNull(await reader.GetAsync(CharacterId.New()));
    }

    [TestMethod]
    public async Task DuplicateMissingAndStaleWritesLeaveStoredStateIntact()
    {
        var original = await repository.CreateAsync(CharacterId.New());
        await Assert.ThrowsExactlyAsync<CharacterConflictException>(() => repository.CreateAsync(original.Id));
        var saved = await repository.SaveAsync(original);
        await Assert.ThrowsExactlyAsync<CharacterConflictException>(() => repository.SaveAsync(original));
        var missing = new CharacterIdentity(CharacterId.New(), new CharacterRevision(0));
        await Assert.ThrowsExactlyAsync<CharacterConflictException>(() => repository.SaveAsync(missing));
        Assert.AreEqual(saved, await repository.GetAsync(original.Id));
        Assert.IsNull(await repository.GetAsync(missing.Id));
    }

    [TestMethod]
    public async Task CompetingRepositoryInstancesHaveExactlyOneWinner()
    {
        var original = await repository.CreateAsync(CharacterId.New());
        async Task<bool> Attempt(SqliteCharacterRepository writer)
        {
            try { await writer.SaveAsync(original); return true; }
            catch (CharacterConflictException) { return false; }
        }
        var results = await Task.WhenAll(Attempt(repository), Attempt(new SqliteCharacterRepository(database)));
        Assert.AreEqual(1, results.Count(success => success));
        Assert.AreEqual(1L, (await repository.GetAsync(original.Id))!.Revision.Value);
    }

    [TestMethod]
    public async Task CancellationAndOverflowPreserveState()
    {
        var original = await repository.CreateAsync(CharacterId.New());
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var missing = CharacterId.New();
        await Assert.ThrowsAsync<OperationCanceledException>(() => repository.CreateAsync(missing, canceled.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => repository.SaveAsync(original, canceled.Token));
        Assert.IsNull(await repository.GetAsync(missing));
        Assert.AreEqual(original, await repository.GetAsync(original.Id));
        using (var connection = database.OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE characters SET revision = 9223372036854775807;";
            command.ExecuteNonQuery();
        }
        var maximum = (await repository.GetAsync(original.Id))!;
        await Assert.ThrowsExactlyAsync<OverflowException>(() => repository.SaveAsync(maximum));
        Assert.AreEqual(maximum, await repository.GetAsync(original.Id));
    }

    [TestMethod]
    public async Task ConcurrentCreatesHaveOneWinnerAndPersistInitialRevision()
    {
        var id = CharacterId.New();
        async Task<bool> Attempt(SqliteCharacterRepository writer)
        {
            try { await writer.CreateAsync(id); return true; }
            catch (CharacterConflictException) { return false; }
        }
        var results = await Task.WhenAll(Attempt(repository), Attempt(new SqliteCharacterRepository(database)));
        Assert.AreEqual(1, results.Count(success => success));
        Assert.AreEqual(new CharacterIdentity(id, new CharacterRevision(0)), await repository.GetAsync(id));
    }

    [TestMethod]
    public async Task FailedSaveRollsBackAndSameExpectedRevisionCanRetry()
    {
        var original = await repository.CreateAsync(CharacterId.New());
        using (var connection = database.OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TRIGGER reject_save BEFORE UPDATE ON characters BEGIN SELECT RAISE(ABORT, 'injected save failure'); END;";
            command.ExecuteNonQuery();
        }
        await Assert.ThrowsExactlyAsync<Microsoft.Data.Sqlite.SqliteException>(() => repository.SaveAsync(original));
        Assert.AreEqual(original, await repository.GetAsync(original.Id));
        using (var connection = database.OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "DROP TRIGGER reject_save;";
            command.ExecuteNonQuery();
        }
        Assert.AreEqual(new CharacterRevision(1), (await repository.SaveAsync(original)).Revision);
    }

    [TestMethod]
    public async Task StorageFailurePropagatesAndCanBeRetried()
    {
        var uninitialized = new CharacterDatabase(Path.Combine(directory, "retry.db"));
        var writer = new SqliteCharacterRepository(uninitialized);
        var id = CharacterId.New();
        await Assert.ThrowsExactlyAsync<Microsoft.Data.Sqlite.SqliteException>(() => writer.CreateAsync(id));
        uninitialized.Initialize();
        Assert.AreEqual(new CharacterRevision(0), (await writer.CreateAsync(id)).Revision);
    }
}
