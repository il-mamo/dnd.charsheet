using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DnD.CharSheet.Infrastructure.Tests;

[TestClass]
public sealed class TransferTests
{
    private string root = null!;
    private string Data => Path.Combine(root, "data");
    private CharacterDatabase Database => new(Path.Combine(Data, "characters.db"));
    private SqliteAdventureRepository Repository => new(Database);
    private readonly AdventureId a = AdventureId.New(), b = AdventureId.New();
    private readonly CharacterId first = CharacterId.New(), other = CharacterId.New();

    [TestInitialize]
    public async Task Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "charsheet-transfers", Guid.NewGuid().ToString("N"));
        Database.Initialize();
        await Repository.CreateWithDraftAsync(a, "Source", first, "Moving");
        await Repository.CreateWithDraftAsync(b, "Destination", CharacterId.New(), "Destination member");
        await new SqliteCharacterLibrary(Database).CreateDraftAsync(other, "Retained");
        await Repository.AssignAsync(other, a);
    }

    [TestCleanup] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    private string FirstMember(AdventureId id)
    {
        using var connection = Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT first_character_id FROM adventures WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString());
        return (string)command.ExecuteScalar()!;
    }

    [TestMethod]
    public async Task FirstMemberTransferPreservesCharacterPortraitRecoveryAndRestartBackupState()
    {
        await new SqliteCharacterRepository(Database).SaveAsync(new(first, new(0)));
        var portraits = new ManagedCharacterPortraits(Database, Path.Combine(Data, "portraits"));
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/iZk9HQAAAABJRU5ErkJggg==");
        var portrait = await portraits.SaveAsync(first, null, png);
        var recovery = new FileDraftRecovery(Path.Combine(Data, "draft-recovery.json"));
        await recovery.SaveAsync(new("Source unfinished input", AdventureId: a));
        var before = (await new SqliteCharacterLibrary(Database).ListAsync()).Single(c => c.Id == first);
        var recoveryBytes = File.ReadAllBytes(Path.Combine(Data, "draft-recovery.json"));

        await Repository.TransferAsync(first, a, b);

        Assert.AreEqual(other.ToString(), FirstMember(a));
        Assert.AreEqual(b, (await Repository.GetMembershipAsync(first)).AdventureId);
        Assert.AreEqual(a, (await Repository.GetMembershipAsync(other)).AdventureId);
        Assert.AreEqual(before, (await new SqliteCharacterLibrary(Database).ListAsync()).Single(c => c.Id == first));
        Assert.AreEqual(portrait.Token, (await portraits.ReadAsync(first)).Token);
        CollectionAssert.AreEqual(png, (await portraits.ReadAsync(first)).Png!);
        CollectionAssert.AreEqual(recoveryBytes, File.ReadAllBytes(Path.Combine(Data, "draft-recovery.json")));
        Database.Initialize();
        Assert.AreEqual(b, (await Repository.GetMembershipAsync(first)).AdventureId);

        var backups = new FileBackupService(Data);
        var archive = Path.Combine(root, "transferred.zip");
        await backups.CreateAsync(archive);
        using var preview = await backups.PreviewAsync(archive);
        await Repository.TransferAsync(first, b, a);
        await backups.RestoreAsync(preview);
        Assert.AreEqual(b, (await Repository.GetMembershipAsync(first)).AdventureId);
        Assert.AreEqual(other.ToString(), FirstMember(a));
        Assert.AreEqual(before, (await new SqliteCharacterLibrary(Database).ListAsync()).Single(c => c.Id == first));
        CollectionAssert.AreEqual(png, (await portraits.ReadAsync(first)).Png!);
        CollectionAssert.AreEqual(recoveryBytes, File.ReadAllBytes(Path.Combine(Data, "draft-recovery.json")));
    }

    [TestMethod]
    public async Task NonFirstTransferPreservesDesignatedMemberAndBlocksLastMember()
    {
        await Repository.TransferAsync(other, a, b);
        Assert.AreEqual(first.ToString(), FirstMember(a));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Repository.TransferAsync(first, a, b));
        Assert.AreEqual(a, (await Repository.GetMembershipAsync(first)).AdventureId);
        Assert.HasCount(2, await Repository.ListAsync());
    }

    [TestMethod]
    public async Task MissingDestinationRollsBackDesignatedMemberAndMembership()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Repository.TransferAsync(first, a, AdventureId.New()));
        Assert.AreEqual(first.ToString(), FirstMember(a));
        Assert.AreEqual(a, (await Repository.GetMembershipAsync(first)).AdventureId);
        await Repository.DeleteAsync(b);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Repository.TransferAsync(first, a, b));
        Assert.AreEqual(first.ToString(), FirstMember(a));
        Assert.AreEqual(a, (await Repository.GetMembershipAsync(first)).AdventureId);
    }

    [TestMethod]
    public async Task SameMissingUnassignedAndStaleSourcesCannotMoveCharacter()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Repository.TransferAsync(first, a, a));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Repository.TransferAsync(first, AdventureId.New(), b));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Repository.TransferAsync(CharacterId.New(), a, b));
        var unassigned = CharacterId.New();
        await new SqliteCharacterLibrary(Database).CreateDraftAsync(unassigned, "Unassigned");
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Repository.TransferAsync(unassigned, a, b));
        Assert.IsNull((await Repository.GetMembershipAsync(unassigned)).AdventureId);
        await Repository.TransferAsync(first, a, b);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Repository.TransferAsync(first, a, b));
        Assert.AreEqual(b, (await Repository.GetMembershipAsync(first)).AdventureId);
        Assert.AreEqual(other.ToString(), FirstMember(a));
    }

    [TestMethod]
    public async Task ConcurrentConfirmationsHaveOneWinnerAndDoNotEmptySource()
    {
        async Task<bool> Move(CharacterId character)
        {
            try { await Repository.TransferAsync(character, a, b); return true; }
            catch (InvalidOperationException) { return false; }
        }
        var duplicate = await Task.WhenAll(Move(first), Move(first));
        Assert.AreEqual(1, duplicate.Count(success => success));
        await Repository.TransferAsync(first, b, a);
        var competing = await Task.WhenAll(Move(first), Move(other));
        Assert.AreEqual(1, competing.Count(success => success));
        var memberships = await Task.WhenAll(Repository.GetMembershipAsync(first), Repository.GetMembershipAsync(other));
        Assert.AreEqual(1, memberships.Count(member => member.AdventureId == a));
    }

    [TestMethod]
    public async Task CancellationAndDatabaseWriteFailureLeaveOriginalMembershipAndAnchor()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => Repository.TransferAsync(first, a, b, cancellation.Token));
        using (var connection = Database.OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TRIGGER reject_transfer BEFORE UPDATE OF adventure_id ON adventure_memberships BEGIN SELECT RAISE(ABORT, 'injected write failure'); END";
            command.ExecuteNonQuery();
        }
        await Assert.ThrowsExactlyAsync<SqliteException>(() => Repository.TransferAsync(first, a, b));
        Assert.AreEqual(a, (await Repository.GetMembershipAsync(first)).AdventureId);
        Assert.AreEqual(first.ToString(), FirstMember(a));
        Assert.AreEqual(a, (await Repository.GetMembershipAsync(other)).AdventureId);
    }
}
