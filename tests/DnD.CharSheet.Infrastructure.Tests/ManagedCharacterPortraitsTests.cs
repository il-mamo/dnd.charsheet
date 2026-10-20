using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DnD.CharSheet.Infrastructure.Tests;

[TestClass]
public sealed class ManagedCharacterPortraitsTests
{
    private string directory = null!;
    private CharacterDatabase database = null!;
    private ManagedCharacterPortraits portraits = null!;
    private string Assets => Path.Combine(directory, "portraits");
    // Real 1x1 PNG; infrastructure accepts the desktop codec's normalized output.
    private static byte[] Image => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aXioAAAAASUVORK5CYII=");

    [TestInitialize]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "charsheet-portrait-tests", Guid.NewGuid().ToString("N"));
        database = new CharacterDatabase(Path.Combine(directory, "characters.db"));
        database.Initialize();
        portraits = new ManagedCharacterPortraits(database, Assets);
    }

    [TestCleanup]
    public void CleanUp() => Directory.Delete(directory, recursive: true);

    private async Task<CharacterId> Create(string name = "A") => (await new SqliteCharacterLibrary(database).CreateDraftAsync(CharacterId.New(), name)).Id;

    [TestMethod]
    public async Task ManagedCopySurvivesDeletedSourceReopenAndOtherCharacterUpdates()
    {
        var a = await Create();
        var b = await Create("B");
        var source = Path.Combine(directory, "source.png");
        File.WriteAllBytes(source, Image);
        var bytes = File.ReadAllBytes(source);
        var saved = await portraits.SaveAsync(a, null, bytes);
        Array.Fill(bytes, (byte)0);
        File.Delete(source);
        var other = await portraits.SaveAsync(b, null, Image);
        Assert.AreNotEqual(saved.Token, other.Token);
        var reopened = new CharacterDatabase(Path.Combine(directory, "characters.db"));
        Assert.IsNull(reopened.Initialize());
        var reader = new ManagedCharacterPortraits(reopened, Assets);
        CollectionAssert.AreEqual(Image, (await reader.ReadAsync(a)).Png!);
        await reader.SaveAsync(b, other.Token, null);
        Assert.AreEqual(saved.Token, (await reader.ReadAsync(a)).Token);
        Assert.IsNull((await reader.ReadAsync(b)).Png);
        Assert.AreEqual(0L, (await new SqliteCharacterRepository(database).GetAsync(a))!.Revision.Value);
    }

    [TestMethod]
    public async Task ReplaceAndRemovePersistAndRejectStaleTokens()
    {
        var id = await Create();
        var first = await portraits.SaveAsync(id, null, Image);
        var second = await portraits.SaveAsync(id, first.Token, Image);
        Assert.AreNotEqual(first.Token, second.Token);
        await Assert.ThrowsExactlyAsync<CharacterConflictException>(() => portraits.SaveAsync(id, first.Token, Image));
        await Assert.ThrowsExactlyAsync<CharacterConflictException>(() => portraits.SaveAsync(id, null, null));
        Assert.AreEqual(second.Token, (await portraits.ReadAsync(id)).Token);
        await portraits.SaveAsync(id, second.Token, null);
        var freshReader = new ManagedCharacterPortraits(database, Assets);
        Assert.IsNull((await freshReader.ReadAsync(id)).Token);
        await Assert.ThrowsExactlyAsync<CharacterConflictException>(() => portraits.SaveAsync(id, second.Token, Image));
    }

    [TestMethod]
    public async Task InvalidInputMissingCharacterAndCancellationCannotChangePriorPortrait()
    {
        var id = await Create();
        var prior = await portraits.SaveAsync(id, null, Image);
        foreach (var invalid in new[] { Array.Empty<byte>(), new byte[8], new byte[ManagedCharacterPortraits.MaximumBytes + 1] })
            await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await portraits.SaveAsync(id, prior.Token, invalid));
        await Assert.ThrowsExactlyAsync<CharacterConflictException>(() => portraits.SaveAsync(CharacterId.New(), null, Image));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => portraits.SaveAsync(id, prior.Token, null, canceled.Token));
        Assert.AreEqual(prior.Token, (await portraits.ReadAsync(id)).Token);
        CollectionAssert.AreEqual(Image, (await portraits.ReadAsync(id)).Png!);
    }

    [TestMethod]
    public async Task MissingAndOversizedManagedFilesReturnFallbackWithoutLosingRemovalToken()
    {
        var id = await Create();
        var saved = await portraits.SaveAsync(id, null, Image);
        var path = Path.Combine(Assets, saved.Token + ".png");
        File.Delete(path);
        var missing = await portraits.ReadAsync(id);
        Assert.AreEqual(saved.Token, missing.Token);
        Assert.IsNull(missing.Png);
        using (var stream = File.Create(path)) stream.SetLength(ManagedCharacterPortraits.MaximumBytes + 1L);
        Assert.IsNull((await portraits.ReadAsync(id)).Png);
        await portraits.SaveAsync(id, missing.Token, null);
        Assert.IsNull((await portraits.ReadAsync(id)).Token);
    }

    [TestMethod]
    public async Task FailedReferenceUpdatePreservesPriorFileAndAllowsRetry()
    {
        var id = await Create();
        var prior = await portraits.SaveAsync(id, null, Image);
        Execute("CREATE TRIGGER reject_portrait BEFORE UPDATE ON character_portraits BEGIN SELECT RAISE(ABORT, 'injected failure'); END;");
        await Assert.ThrowsExactlyAsync<SqliteException>(() => portraits.SaveAsync(id, prior.Token, Image));
        Assert.AreEqual(prior.Token, (await portraits.ReadAsync(id)).Token);
        CollectionAssert.AreEqual(Image, File.ReadAllBytes(Path.Combine(Assets, prior.Token + ".png")));
        Execute("DROP TRIGGER reject_portrait;");
        Assert.AreNotEqual(prior.Token, (await portraits.SaveAsync(id, prior.Token, Image)).Token);
    }

    [TestMethod]
    public async Task VersionTwoMigrationPreservesIdentityNameRevisionAndPreupgradeBackup()
    {
        var id = await Create();
        Execute("DROP TABLE character_portraits; PRAGMA user_version=2; UPDATE characters SET revision=17;");
        var backup = database.Initialize();
        Assert.IsNotNull(backup);
        using (var snapshot = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backup, Pooling = false }.ToString()))
        {
            snapshot.Open();
            using var command = snapshot.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            Assert.AreEqual(2L, command.ExecuteScalar());
            command.CommandText = "SELECT count(*) FROM sqlite_master WHERE name='character_portraits';";
            Assert.AreEqual(0L, command.ExecuteScalar());
        }
        var drafts = await new SqliteCharacterLibrary(database).ListAsync();
        Assert.AreEqual(new CharacterDraft(id, new CharacterRevision(17), "A"), drafts.Single());
        Assert.IsNull((await portraits.ReadAsync(id)).Png);
        await portraits.SaveAsync(id, null, Image);
        Assert.IsNull(database.Initialize());
    }

    private void Execute(string sql)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
