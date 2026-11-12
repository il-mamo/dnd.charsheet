using System.IO.Compression;
using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DnD.CharSheet.Infrastructure.Tests;

[TestClass]
public sealed class BackupTests
{
    private static byte[] ValidPng() => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/iZk9HQAAAABJRU5ErkJggg==");

    [TestMethod]
    public async Task MultipleRecoveryContextsIncludingDeletedAdventureRoundTripWithoutReassignment()
    {
        var active = AdventureId.New();
        var deleted = AdventureId.New();
        var repository = new SqliteAdventureRepository(Database);
        await repository.CreateWithDraftAsync(active, "Active", CharacterId.New(), "First");
        await repository.CreateWithDraftAsync(deleted, "Deleted", CharacterId.New(), "Retained");
        await repository.DeleteAsync(deleted);
        var entries = new[]
        {
            new DraftRecovery("Active input", CharacterId.New(), active),
            new DraftRecovery("Deleted input", AdventureId: deleted),
            new DraftRecovery("Legacy input")
        };
        var recovery = new FileDraftRecovery(Path.Combine(Data, "draft-recovery.json"));
        await recovery.SaveAllAsync(entries);
        var service = new FileBackupService(Data);
        await service.CreateAsync(Archive);
        using var preview = await service.PreviewAsync(Archive);
        await recovery.SaveAllAsync([]);
        await service.RestoreAsync(preview);
        CollectionAssert.AreEqual(entries, (await new FileDraftRecovery(Path.Combine(Data, "draft-recovery.json")).LoadAllAsync()).ToArray());
        Assert.AreEqual(active, (await repository.ListAsync()).Single().Id);
    }

    [TestMethod]
    public async Task LegacyIdentityOnlyRowsRoundTripWithoutInventingMetadata()
    {
        var id = CharacterId.New();
        await new SqliteCharacterRepository(Database).CreateAsync(id);
        var service = new FileBackupService(Data);
        await service.CreateAsync(Archive);
        using var preview = await service.PreviewAsync(Archive);
        CollectionAssert.AreEqual(new[] { "Unnamed draft" }, preview.CharacterNames.ToArray());
        await new SqliteCharacterLibrary(Database).CreateDraftAsync(CharacterId.New(), "Later");
        await service.RestoreAsync(preview);
        var actual = await new SqliteCharacterLibrary(Database).ListAsync();
        Assert.HasCount(1, actual);
        Assert.AreEqual(id, actual[0].Id);
        using var connection = Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM character_drafts";
        Assert.AreEqual(0L, command.ExecuteScalar());
    }

    [TestMethod]
    public async Task BrokenPortraitAndMissingTimestampAreRejected()
    {
        var id = CharacterId.New();
        await new SqliteCharacterLibrary(Database).CreateDraftAsync(id, "Keep");
        var portraits = new ManagedCharacterPortraits(Database, Path.Combine(Data, "portraits"));
        var portrait = await portraits.SaveAsync(id, null, ValidPng());
        var service = new FileBackupService(Data);
        await service.CreateAsync(Archive);
        using (var zip = ZipFile.Open(Archive, ZipArchiveMode.Update))
        {
            zip.GetEntry("manifest.json")!.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open());
            writer.Write("{\"Version\":1,\"SchemaVersion\":3}");
        }
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => service.PreviewAsync(Archive));
        File.Delete(Archive);
        var broken = ValidPng();
        broken[45] ^= 1;
        File.WriteAllBytes(Path.Combine(Data, "portraits", portrait.Token + ".png"), broken);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => service.CreateAsync(Archive));
        Assert.IsFalse(File.Exists(Archive));
    }
    private string root = null!;
    private string Data => Path.Combine(root, "data");
    private string Archive => Path.Combine(root, "backup.zip");
    private CharacterDatabase Database => new(Path.Combine(Data, "characters.db"));
    [TestInitialize] public void Setup() { root = Path.Combine(Path.GetTempPath(), "charsheet-backup", Guid.NewGuid().ToString("N")); Database.Initialize(); }
    [TestCleanup] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [TestMethod]
    public async Task RoundTripUsesValidatedPreviewAndPreservesReplacementInSafetyBackup()
    {
        var id = CharacterId.New();
        var library = new SqliteCharacterLibrary(Database);
        await library.CreateDraftAsync(id, "Original");
        var png = ValidPng();
        await new ManagedCharacterPortraits(Database, Path.Combine(Data, "portraits")).SaveAsync(id, null, png);
        await new FileDraftRecovery(Path.Combine(Data, "draft-recovery.json")).SaveAsync(new DraftRecovery("Unfinished"));
        var service = new FileBackupService(Data);
        await service.CreateAsync(Archive);
        using var preview = await service.PreviewAsync(Archive);
        Assert.AreEqual(1, preview.CharacterCount);
        Assert.AreEqual(1, preview.PortraitCount);
        Assert.IsTrue(preview.HasDraftRecovery);
        await library.CreateDraftAsync(CharacterId.New(), "Replacement");
        File.WriteAllText(Archive, "changed after preview");
        var safety = await service.RestoreAsync(preview);
        Assert.HasCount(1, await library.ListAsync());
        Assert.AreEqual("Original", (await library.ListAsync())[0].Name);
        Assert.AreEqual("Unfinished", (await new FileDraftRecovery(Path.Combine(Data, "draft-recovery.json")).LoadAsync())!.Name);
        CollectionAssert.AreEqual(png, (await new ManagedCharacterPortraits(Database, Path.Combine(Data, "portraits")).ReadAsync(id)).Png!);
        using var safetyPreview = await service.PreviewAsync(safety);
        Assert.AreEqual(2, safetyPreview.CharacterCount);
    }

    [TestMethod]
    public async Task UnsafeDuplicateUnsupportedAndTruncatedArchivesNeverMutateLiveData()
    {
        await new SqliteCharacterLibrary(Database).CreateDraftAsync(CharacterId.New(), "Keep");
        var service = new FileBackupService(Data);
        foreach (var name in new[] { "../characters.db", "portraits/../characters.db", "characters.db-wal", "PORTRAITS/abc.png" })
        {
            using (var zip = ZipFile.Open(Archive, ZipArchiveMode.Create)) using (var writer = new StreamWriter(zip.CreateEntry(name).Open())) writer.Write("unsafe");
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => service.PreviewAsync(Archive));
            File.Delete(Archive);
        }
        await service.CreateAsync(Archive);
        using (var zip = ZipFile.Open(Archive, ZipArchiveMode.Update)) zip.CreateEntry("characters.db");
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => service.PreviewAsync(Archive));
        Assert.AreEqual("Keep", (await new SqliteCharacterLibrary(Database).ListAsync())[0].Name);
        Assert.HasCount(0, Directory.GetDirectories(root, "data.backup-stage-*"));
    }

    [TestMethod]
    public async Task SafetyDestinationFailureLeavesLiveDirectoryAndPreviewUsable()
    {
        var library = new SqliteCharacterLibrary(Database);
        await library.CreateDraftAsync(CharacterId.New(), "Original");
        var service = new FileBackupService(Data);
        await service.CreateAsync(Archive);
        using var preview = await service.PreviewAsync(Archive);
        await library.CreateDraftAsync(CharacterId.New(), "Keep");
        File.WriteAllText(Path.Combine(root, "backups"), "blocks directory");
        try { await service.RestoreAsync(preview); Assert.Fail("Expected safety backup failure."); }
        catch (IOException) { }
        Assert.HasCount(2, await library.ListAsync());
        File.Delete(Path.Combine(root, "backups"));
        await service.RestoreAsync(preview);
        Assert.HasCount(1, await library.ListAsync());
    }

    [TestMethod]
    public async Task UnsupportedFormatAndMissingPortraitPreserveCurrentData()
    {
        var service = new FileBackupService(Data);
        var id = CharacterId.New();
        await new SqliteCharacterLibrary(Database).CreateDraftAsync(id, "Keep");
        await service.CreateAsync(Archive);
        using (var zip = ZipFile.Open(Archive, ZipArchiveMode.Update))
        {
            zip.GetEntry("manifest.json")!.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open());
            writer.Write("{\"Version\":2,\"SchemaVersion\":3,\"CreatedUtc\":\"2026-11-01T00:00:00Z\"}");
        }
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => service.PreviewAsync(Archive));
        File.Delete(Archive);
        await new ManagedCharacterPortraits(Database, Path.Combine(Data, "portraits")).SaveAsync(id, null, ValidPng());
        await service.CreateAsync(Archive);
        using (var zip = ZipFile.Open(Archive, ZipArchiveMode.Update)) zip.Entries.Single(e => e.FullName.StartsWith("portraits/", StringComparison.Ordinal)).Delete();
        await Assert.ThrowsExactlyAsync<DirectoryNotFoundException>(() => service.PreviewAsync(Archive));
        Assert.AreEqual("Keep", (await new SqliteCharacterLibrary(Database).ListAsync())[0].Name);
    }

    [TestMethod]
    public void StartupRestoresOldDirectoryAfterInterruptedFirstRename()
    {
        Directory.Move(Data, Data + ".restore-previous");
        new FileBackupService(Data).RecoverInterruptedRestore();
        Assert.IsTrue(File.Exists(Path.Combine(Data, "characters.db")));
        Assert.IsFalse(Directory.Exists(Data + ".restore-previous"));
        Database.Initialize();
    }
}






