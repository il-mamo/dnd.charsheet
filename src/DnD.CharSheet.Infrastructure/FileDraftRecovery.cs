using System.Text.Json;
using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;

namespace DnD.CharSheet.Infrastructure;

/// <summary>Single-session recovery journal. Replace only after a complete, flushed write.</summary>
public sealed class FileDraftRecovery(string path) : IDraftRecovery
{
    private readonly string path = Path.GetFullPath(path);
    private readonly SemaphoreSlim gate = new(1, 1);
    private sealed record Entry(string Name, Guid? SubmittedId, Guid? AdventureId);
    private sealed record Document(int Version, string? Name = null, Guid? SubmittedId = null, Entry[]? Entries = null);
    private const int MaximumBytes = 32768;

    public async Task<DraftRecovery?> LoadAsync() => (await LoadAllAsync()).FirstOrDefault();

    public async Task<IReadOnlyList<DraftRecovery>> LoadAllAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run<IReadOnlyList<DraftRecovery>>(() =>
            {
                byte[] bytes;
                try
                {
                    using var stream = File.OpenRead(path);
                    if (stream.Length > MaximumBytes) throw new InvalidDataException("Recovery journal exceeds its limit.");
                    bytes = new byte[checked((int)stream.Length)];
                    stream.ReadExactly(bytes);
                }
                catch (FileNotFoundException) { return []; }
                catch (DirectoryNotFoundException) { return []; }
                var document = JsonSerializer.Deserialize<Document>(bytes)
                    ?? throw new InvalidDataException("Recovery journal is empty.");
                Entry[] entries = document.Version switch
                {
                    1 when document.Name is not null => [new(document.Name, document.SubmittedId, null)],
                    2 when document.Entries is not null => document.Entries,
                    _ => throw new InvalidDataException("Unsupported recovery journal.")
                };
                if (entries.Any(entry => entry is null)) throw new InvalidDataException("Invalid recovery entry.");
                try
                {
                    var drafts = entries.Select(entry => new DraftRecovery(entry.Name,
                        entry.SubmittedId is { } id ? new CharacterId(id) : null,
                        entry.AdventureId is { } adventure ? new AdventureId(adventure) : null)).ToArray();
                    Validate(drafts);
                    return drafts;
                }
                catch (ArgumentException error) { throw new InvalidDataException("Invalid recovery journal.", error); }
            }).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public Task SaveAsync(DraftRecovery? draft) => SaveAllAsync(draft is null ? [] : [draft]);

    private static void Validate(IReadOnlyList<DraftRecovery> drafts)
    {
        if (drafts.Count > 64) throw new ArgumentException("Recovery journal has too many Adventures.");
        var contexts = new HashSet<AdventureId?>();
        var submitted = new HashSet<CharacterId>();
        foreach (var draft in drafts)
        {
            if (draft is null || draft.Name is null || draft.Name.Length > 4096)
                throw new ArgumentException("Recovery input exceeds its limit.");
            if (!contexts.Add(draft.AdventureId)) throw new ArgumentException("Duplicate recovery Adventure.");
            if (draft.SubmittedId is not null)
            {
                DraftName.Normalize(draft.Name);
                if (!submitted.Add(draft.SubmittedId)) throw new ArgumentException("Duplicate submitted identity.");
            }
        }
    }

    public async Task SaveAllAsync(IReadOnlyList<DraftRecovery> drafts)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        // Capture before yielding so subsequent caller edits cannot redirect a queued write.
        var snapshot = drafts.ToArray();
        Validate(snapshot);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new Document(2, Entries: snapshot.Select(draft =>
            new Entry(draft.Name, draft.SubmittedId?.Value, draft.AdventureId?.Value)).ToArray()));
        if (bytes.Length > MaximumBytes) throw new ArgumentException("Recovery journal exceeds its limit.");
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                if (snapshot.Length == 0) { File.Delete(path); return; }
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(bytes);
                        stream.Flush(flushToDisk: true);
                    }
                    File.Move(temporary, path, overwrite: true);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
}
