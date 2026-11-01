using System.IO.Compression;
using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DnD.CharSheet.Infrastructure.Tests;

[TestClass]
public sealed class BackupBoundaryTests
{
    private string root = null!;
    private string Data => Path.Combine(root, "data");
    private string Archive => Path.Combine(root, "backup.zip");
    private CharacterDatabase Database => new(Path.Combine(Data, "characters.db"));
    [TestInitialize] public void Setup() { root = Path.Combine(Path.GetTempPath(), "charsheet-backup-independent", Guid.NewGuid().ToString("N")); Database.Initialize(); }
    [TestCleanup] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [TestMethod]
    public async Task InvalidRecoveryAndChangedSchemaRejectWithoutChangingLiveBytes()
    {
        var service = new FileBackupService(Data);
        await new SqliteCharacterLibrary(Database).CreateDraftAsync(CharacterId.New(), "Keep");
        await service.CreateAsync(Archive);
        var before = File.ReadAllBytes(Path.Combine(Data, "characters.db"));
        using (var zip = ZipFile.Open(Archive, ZipArchiveMode.Update))
        using (var writer = new StreamWriter(zip.CreateEntry("draft-recovery.json").Open()))
            writer.Write("{\"Version\":99,\"Name\":\"Bad\"}");
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => service.PreviewAsync(Archive));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(Data, "characters.db")));
        File.Delete(Archive);
        await service.CreateAsync(Archive);
        var extracted = Path.Combine(root, "changed");
        ZipFile.ExtractToDirectory(Archive, extracted);
        using (var db = new SqliteConnection($"Data Source={Path.Combine(extracted, "characters.db")};Pooling=False"))
        {
            db.Open(); using var command = db.CreateCommand();
            command.CommandText = "CREATE TRIGGER unexpected AFTER INSERT ON characters BEGIN SELECT 1; END";
            command.ExecuteNonQuery();
        }
        File.Delete(Archive); ZipFile.CreateFromDirectory(extracted, Archive);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => service.PreviewAsync(Archive));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(Data, "characters.db")));
        Assert.HasCount(0, Directory.GetDirectories(root, "data.backup-stage-*"));
    }

    [TestMethod]
    public async Task DisposedOrForeignPreviewCannotReplaceData()
    {
        var service = new FileBackupService(Data);
        await service.CreateAsync(Archive);
        var preview = await service.PreviewAsync(Archive);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => new FileBackupService(Data).RestoreAsync(preview));
        preview.Dispose();
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.RestoreAsync(preview));
        Assert.IsFalse(Directory.Exists(Path.Combine(root, "backups")));
        Assert.IsTrue(File.Exists(Path.Combine(Data, "characters.db")));
    }

    [TestMethod]
    public async Task RecoveryAfterCommittedRenameKeepsReplacementAndRemovesPredecessor()
    {
        var library = new SqliteCharacterLibrary(Database);
        await library.CreateDraftAsync(CharacterId.New(), "Old");
        Directory.Move(Data, Data + ".restore-previous");
        Database.Initialize();
        await library.CreateDraftAsync(CharacterId.New(), "New");
        new FileBackupService(Data).RecoverInterruptedRestore();
        Assert.AreEqual("New", (await library.ListAsync()).Single().Name);
        Assert.IsFalse(Directory.Exists(Data + ".restore-previous"));
    }

    [TestMethod]
    public async Task FailedExportPreservesExistingDestinationAndCleansTemporaryFiles()
    {
        var service = new FileBackupService(Data);
        File.WriteAllText(Archive, "Existing file");
        File.WriteAllText(Path.Combine(Data, "draft-recovery.json"), "malformed");
        try { await service.CreateAsync(Archive); Assert.Fail("Expected validation failure."); }
        catch (System.Text.Json.JsonException) { }
        Assert.AreEqual("Existing file", File.ReadAllText(Archive));
        Assert.HasCount(0, Directory.GetFiles(root, "*.tmp"));
        Assert.HasCount(0, Directory.GetDirectories(root, "data.backup-stage-*"));
    }
}
