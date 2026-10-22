using System.Text.Json;
using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;

namespace DnD.CharSheet.Infrastructure;

/// <summary>Single-session recovery journal. Replace only after a complete, flushed write.</summary>
public sealed class FileDraftRecovery(string path) : IDraftRecovery
{
    private readonly string path = Path.GetFullPath(path);
    private readonly SemaphoreSlim gate = new(1, 1);
    private sealed record Document(int Version, string Name, Guid? SubmittedId);

    public async Task<DraftRecovery?> LoadAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                // Opening directly distinguishes a missing file from inaccessible data.
                byte[] bytes;
                try
                {
                    using var stream = File.OpenRead(path);
                    if (stream.Length > 32768) throw new InvalidDataException("Recovery journal exceeds its limit.");
                    bytes = new byte[checked((int)stream.Length)];
                    stream.ReadExactly(bytes);
                }
                catch (FileNotFoundException) { return null; }
                catch (DirectoryNotFoundException) { return null; }
                var document = JsonSerializer.Deserialize<Document>(bytes)
                    ?? throw new InvalidDataException("Recovery journal is empty.");
                if (document.Version != 1 || document.Name is null || document.Name.Length > 4096)
                    throw new InvalidDataException("Unsupported recovery journal.");
                var id = document.SubmittedId is { } value ? new CharacterId(value) : null;
                if (id is not null) DraftName.Normalize(document.Name);
                return new DraftRecovery(document.Name, id);
            }).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public async Task SaveAsync(DraftRecovery? draft)
    {
        if (draft is not null)
        {
            if (draft.Name.Length > 4096) throw new ArgumentException("Recovery input exceeds its limit.");
            if (draft.SubmittedId is not null) DraftName.Normalize(draft.Name);
        }
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                if (draft is null) { File.Delete(path); return; }
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    var bytes = JsonSerializer.SerializeToUtf8Bytes(new Document(1, draft.Name, draft.SubmittedId?.Value));
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
