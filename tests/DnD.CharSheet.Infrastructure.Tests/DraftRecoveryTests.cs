using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DnD.CharSheet.Infrastructure.Tests;

[TestClass]
public sealed class DraftRecoveryTests
{
    private string directory = null!;
    private string Journal => Path.Combine(directory, "recovery.json");
    [TestInitialize] public void SetUp() => directory = Path.Combine(Path.GetTempPath(), "charsheet-recovery", Guid.NewGuid().ToString("N"));
    [TestCleanup] public void CleanUp() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

    [TestMethod]
    public async Task UnfinishedInputAndSubmittedIdentitySurviveReopenAndClear()
    {
        var store = new FileDraftRecovery(Journal);
        Assert.IsNull(await store.LoadAsync());
        var input = new DraftRecovery("  Éowyn 星\n");
        await store.SaveAsync(input);
        Assert.AreEqual(input, await new FileDraftRecovery(Journal).LoadAsync());
        var submitted = new DraftRecovery("Éowyn", CharacterId.New());
        await store.SaveAsync(submitted);
        Assert.AreEqual(submitted, await new FileDraftRecovery(Journal).LoadAsync());
        await store.SaveAsync(null);
        Assert.IsNull(await new FileDraftRecovery(Journal).LoadAsync());
    }

    [TestMethod]
    public async Task InvalidWritesPreservePreviousJournal()
    {
        var store = new FileDraftRecovery(Journal);
        var previous = new DraftRecovery("Previous");
        await store.SaveAsync(previous);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.SaveAsync(new DraftRecovery(new string('x', 4097))));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.SaveAsync(new DraftRecovery("", CharacterId.New())));
        Assert.AreEqual(previous, await store.LoadAsync());
        Assert.HasCount(1, Directory.GetFiles(directory));
    }

    [TestMethod]
    public async Task FailedReplaceLeavesPriorJournalIntactAndRetryWorks()
    {
        var store = new FileDraftRecovery(Journal);
        var original = new DraftRecovery("Original");
        await store.SaveAsync(original);
        using (var locked = new FileStream(Journal, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            try
            {
                await store.SaveAsync(new DraftRecovery("Replacement"));
                Assert.Fail("Expected the locked destination to reject replacement.");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
        Assert.AreEqual(original, await store.LoadAsync());
        Assert.HasCount(1, Directory.GetFiles(directory));
        await store.SaveAsync(new DraftRecovery("Replacement"));
        Assert.AreEqual("Replacement", (await store.LoadAsync())!.Name);
    }

    [TestMethod]
    public async Task CorruptFutureAndOversizedJournalsAreNotTreatedAsEmpty()
    {
        Directory.CreateDirectory(directory);
        foreach (var text in new[] { "{", "null", "{\"Version\":2,\"Name\":\"Future\"}", new string('x', 32769) })
        {
            await File.WriteAllTextAsync(Journal, text);
            try { await new FileDraftRecovery(Journal).LoadAsync(); Assert.Fail("Expected invalid recovery failure."); }
            catch (Exception error) when (error is System.Text.Json.JsonException or InvalidDataException) { }
            Assert.AreEqual(text, await File.ReadAllTextAsync(Journal));
        }
    }
}
