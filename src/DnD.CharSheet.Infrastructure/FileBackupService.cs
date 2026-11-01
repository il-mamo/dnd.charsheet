using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;
using Microsoft.Data.Sqlite;

namespace DnD.CharSheet.Infrastructure;

/// <summary>Caller drains writes and owns the application's exclusive session lock.</summary>
public sealed class FileBackupService(string dataDirectory) : IBackupService
{
    private readonly string data = Path.GetFullPath(dataDirectory);
    private readonly SemaphoreSlim gate = new(1, 1);
    private const long MaximumTotal = 512L * 1024 * 1024;
    private const int MaximumEntries = 10003;
    private string Rollback => data + ".restore-previous";
    private sealed record Manifest(int Version, int SchemaVersion, DateTimeOffset CreatedUtc);
    private sealed class Prepared(FileBackupService owner, string directory, Manifest manifest,
        string[] names, int portraits, bool recovery) : BackupPreview
    {
        internal FileBackupService Owner { get; } = owner;
        internal string Directory { get; } = directory;
        internal bool Disposed { get; private set; }
        public override DateTimeOffset CreatedUtc => manifest.CreatedUtc;
        public override int CharacterCount => names.Length;
        public override int PortraitCount => portraits;
        public override bool HasDraftRecovery => recovery;
        public override IReadOnlyList<string> CharacterNames { get; } = Array.AsReadOnly(names);
        public override void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            try { if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
    private async Task<T> Run<T>(Func<T> action)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try { return await Task.Run(action).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    public Task CreateAsync(string destinationPath) => Run(() => { Create(destinationPath); return true; });
    public Task<BackupPreview> PreviewAsync(string archivePath) => Run<BackupPreview>(() => Prepare(archivePath));
    public Task<string> RestoreAsync(BackupPreview preview) => Run(() =>
    {
        if (preview is not Prepared p || p.Owner != this || p.Disposed)
            throw new ArgumentException("Restore preview is unavailable.", nameof(preview));
        // Stage is private and never reads the source archive again; revalidate before replacement.
        Validate(p.Directory);
        var safety = Path.Combine(Path.GetDirectoryName(data)!, "backups", $"before-restore-{Guid.NewGuid():N}.zip");
        Create(safety);
        if (System.IO.Directory.Exists(Rollback)) throw new IOException("An earlier restore needs recovery before another restore.");
        System.IO.Directory.Move(data, Rollback);
        try { System.IO.Directory.Move(p.Directory, data); }
        catch { System.IO.Directory.Move(Rollback, data); throw; }
        // Once the new directory exists, startup treats the swap as committed.
        try { System.IO.Directory.Delete(Rollback, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return safety;
    });
    /// <summary>Run before database initialization, with exclusive session ownership.</summary>
    public void RecoverInterruptedRestore()
    {
        if (!System.IO.Directory.Exists(Rollback)) return;
        if (!System.IO.Directory.Exists(data)) System.IO.Directory.Move(Rollback, data);
        else System.IO.Directory.Delete(Rollback, true);
    }
    private string NewStage()
    {
        var path = data + ".backup-stage-" + Guid.NewGuid().ToString("N");
        System.IO.Directory.CreateDirectory(path);
        return path;
    }
    private void Create(string destinationPath)
    {
        var destination = Path.GetFullPath(destinationPath);
        if (destination.StartsWith(data + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a backup destination outside application data.");
        var stage = NewStage();
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var source = Open(Path.Combine(data, "characters.db"), true))
            using (var target = Open(Path.Combine(stage, "characters.db"), false)) source.BackupDatabase(target);
            foreach (var token in ReadAssets(Path.Combine(stage, "characters.db")))
            {
                System.IO.Directory.CreateDirectory(Path.Combine(stage, "portraits"));
                File.Copy(Path.Combine(data, "portraits", token + ".png"), Path.Combine(stage, "portraits", token + ".png"));
            }
            var journal = Path.Combine(data, "draft-recovery.json");
            if (File.Exists(journal)) File.Copy(journal, Path.Combine(stage, "draft-recovery.json"));
            Validate(stage);
            File.WriteAllBytes(Path.Combine(stage, "manifest.json"), JsonSerializer.SerializeToUtf8Bytes(new Manifest(1, CharacterDatabase.CurrentVersion, DateTimeOffset.UtcNow)));
            // Apply the same expanded archive policy used by Preview before publishing a backup.
            var files = System.IO.Directory.GetFiles(stage, "*", SearchOption.AllDirectories);
            if (files.Length > MaximumEntries || files.Sum(path => new FileInfo(path).Length) > MaximumTotal)
                throw new InvalidDataException("Backup exceeds the supported archive limits.");
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                    foreach (var path in files)
                        zip.CreateEntryFromFile(path, Path.GetRelativePath(stage, path).Replace('\\', '/'), CompressionLevel.Optimal);
                stream.Flush(true);
            }
            File.Move(temporary, destination, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); if (System.IO.Directory.Exists(stage)) System.IO.Directory.Delete(stage, true); }
    }
    private Prepared Prepare(string archivePath)
    {
        var stage = NewStage();
        try
        {
            using var zip = ZipFile.OpenRead(archivePath);
            if (zip.Entries.Count > MaximumEntries) throw new InvalidDataException("Backup has too many entries.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var entry in zip.Entries)
            {
                var name = entry.FullName;
                var limit = name switch { "manifest.json" => 4096L, "draft-recovery.json" => 32768L, "characters.db" => MaximumTotal,
                    _ when Regex.IsMatch(name, "^portraits/[0-9a-f]{32}\\.png$", RegexOptions.CultureInvariant) => ManagedCharacterPortraits.MaximumBytes,
                    _ => throw new InvalidDataException("Backup contains an unsupported path.") };
                if (!seen.Add(name) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || entry.Length > limit || entry.Length < 0 || (total += entry.Length) > MaximumTotal)
                    throw new InvalidDataException("Backup exceeds limits or contains duplicate/unsafe entries.");
                var target = Path.Combine(stage, name.Replace('/', Path.DirectorySeparatorChar));
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var input = entry.Open();
                using var output = new FileStream(target, FileMode.CreateNew);
                var buffer = new byte[81920];
                long copied = 0;
                int read;
                while ((read = input.Read(buffer)) > 0)
                {
                    if ((copied += read) > entry.Length) throw new InvalidDataException("Backup entry length is invalid.");
                    output.Write(buffer, 0, read);
                }
                if (copied != entry.Length) throw new InvalidDataException("Backup entry is truncated.");
                output.Flush(true);
            }
            var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllBytes(Path.Combine(stage, "manifest.json"))) ?? throw new InvalidDataException("Missing backup manifest.");
            if (manifest.Version != 1 || manifest.SchemaVersion != CharacterDatabase.CurrentVersion || manifest.CreatedUtc == default)
                throw new InvalidDataException("Unsupported backup format or database version. Use a compatible application version.");
            var names = Validate(stage);
            return new Prepared(this, stage, manifest, names, ReadAssets(Path.Combine(stage, "characters.db")).Length, File.Exists(Path.Combine(stage, "draft-recovery.json")));
        }
        catch { System.IO.Directory.Delete(stage, true); throw; }
    }
    private static SqliteConnection Open(string path, bool readOnly)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate, Pooling = false, ForeignKeys = true }.ToString());
        try { connection.Open(); return connection; } catch { connection.Dispose(); throw; }
    }
    private static string[] ReadAssets(string database)
    {
        using var connection = Open(database, true);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT asset FROM character_portraits ORDER BY asset";
        using var reader = command.ExecuteReader();
        var assets = new List<string>();
        while (reader.Read())
        {
            var token = reader.GetString(0);
            if (!Regex.IsMatch(token, "^[0-9a-f]{32}$", RegexOptions.CultureInvariant)) throw new InvalidDataException("Invalid portrait reference.");
            assets.Add(token);
        }
        return assets.ToArray();
    }
    private static string[] Validate(string stage)
    {
        var path = Path.Combine(stage, "characters.db");
        if (new FileInfo(path).Length > MaximumTotal) throw new InvalidDataException("Database exceeds backup size limit.");
        using (var connection = Open(path, true))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA user_version";
            if (Convert.ToInt32(command.ExecuteScalar()) != CharacterDatabase.CurrentVersion) throw new InvalidDataException("Unsupported database version.");
            command.CommandText = "PRAGMA integrity_check";
            if (!Equals(command.ExecuteScalar(), "ok")) throw new InvalidDataException("Database integrity check failed.");
            command.CommandText = "PRAGMA foreign_key_check";
            if (command.ExecuteScalar() is not null) throw new InvalidDataException("Database relationships are invalid.");
            command.CommandText = "SELECT name FROM sqlite_schema WHERE name NOT LIKE 'sqlite_%' ORDER BY name";
            var objects = new List<string>();
            using (var r = command.ExecuteReader()) while (r.Read()) objects.Add(r.GetString(0));
            if (!objects.SequenceEqual(new[] { "character_drafts", "character_portraits", "characters" })) throw new InvalidDataException("Unsupported database schema.");
            // Compare exact schema against a fresh supported database, rejecting triggers/views/changed constraints.
            var reference = Path.Combine(stage, "schema-check.db");
            try
            {
                new CharacterDatabase(reference).Initialize();
                using var expected = Open(reference, true);
                using var ec = expected.CreateCommand();
                command.CommandText = ec.CommandText = "SELECT sql FROM sqlite_schema WHERE name NOT LIKE 'sqlite_%' ORDER BY name";
                using var actualReader = command.ExecuteReader();
                using var expectedReader = ec.ExecuteReader();
                while (actualReader.Read())
                    if (!expectedReader.Read() || actualReader.GetString(0) != expectedReader.GetString(0)) throw new InvalidDataException("Unsupported database schema.");
            }
            finally { File.Delete(reference); }
            command.CommandText = "SELECT c.id, c.revision, d.name FROM characters c LEFT JOIN character_drafts d ON d.character_id=c.id ORDER BY c.id";
            var names = new List<string>();
            using (var reader = command.ExecuteReader())
                while (reader.Read())
                {
                    var id = reader.GetString(0);
                    if (!Guid.TryParseExact(id, "D", out var guid) || guid == Guid.Empty || id != guid.ToString("D") || reader.GetInt64(1) < 0) throw new InvalidDataException("Invalid character data.");
                    var name = reader.IsDBNull(2) ? "Unnamed draft" : reader.GetString(2);
                    if (DraftName.Normalize(name) != name) throw new InvalidDataException("Invalid character name.");
                    names.Add(name);
                }
            foreach (var token in ReadAssets(path))
            {
                var portrait = Path.Combine(stage, "portraits", token + ".png");
                BackupPngValidator.Validate(portrait);
            }
            var expectedAssets = ReadAssets(path).Select(t => t + ".png").Order().ToArray();
            var portraitDirectory = Path.Combine(stage, "portraits");
            if (System.IO.Directory.Exists(portraitDirectory) && !System.IO.Directory.GetFiles(portraitDirectory).Select(Path.GetFileName).Order().SequenceEqual(expectedAssets)) throw new InvalidDataException("Backup contains unreferenced portraits.");
            new FileDraftRecovery(Path.Combine(stage, "draft-recovery.json")).LoadAsync().GetAwaiter().GetResult();
            return names.ToArray();
        }
    }
}





