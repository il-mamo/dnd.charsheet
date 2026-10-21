using Microsoft.Data.Sqlite;

namespace DnD.CharSheet.Infrastructure;

/// <summary>Startup-only schema initialization. Call off the UI thread before opening repositories.</summary>
public sealed class CharacterDatabase
{
    public const int CurrentVersion = 3;
    private readonly string databasePath;

    public CharacterDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        this.databasePath = Path.GetFullPath(databasePath);
    }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DnD.CharSheet", "data", "characters.db");

    /// <summary>Every connection enables foreign keys. Caller owns disposal.</summary>
    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = false
        }.ToString());
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>Returns the pre-migration SQLite snapshot path, or null if no backup was needed.
    /// Startup must serialize this operation; no repositories may write until it completes.</summary>
    public string? Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        var existed = File.Exists(databasePath);
        using var connection = OpenConnection();
        using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "PRAGMA user_version;";
        var version = Convert.ToInt32(versionCommand.ExecuteScalar());
        if (version > CurrentVersion)
            throw new InvalidOperationException($"Database schema {version} is newer than supported schema {CurrentVersion}. Use a compatible application version.");
        if (version == CurrentVersion) return null;

        string? backupPath = null;
        if (existed)
        {
            var backupDirectory = Path.Combine(Path.GetDirectoryName(databasePath)!, "migration-backups");
            Directory.CreateDirectory(backupDirectory);
            backupPath = Path.Combine(backupDirectory, $"characters-v{version}-{Guid.NewGuid():N}.db");
            using var backup = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = backupPath, Pooling = false
            }.ToString());
            backup.Open();
            connection.BackupDatabase(backup);
        }

        using var transaction = connection.BeginTransaction(deferred: false);
        using var migration = connection.CreateCommand();
        migration.Transaction = transaction;
        // Ordered migrations share a transaction; existing identities/revisions stay intact.
        if (version < 1)
        {
        migration.CommandText = """
            CREATE TABLE characters (
                id TEXT NOT NULL PRIMARY KEY CHECK (
                    length(id) = 36 AND id = lower(id) AND
                    substr(id, 9, 1) = '-' AND substr(id, 14, 1) = '-' AND
                    substr(id, 19, 1) = '-' AND substr(id, 24, 1) = '-' AND
                    length(replace(id, '-', '')) = 32 AND
                    replace(id, '-', '') NOT GLOB '*[^0-9a-f]*' AND
                    id <> '00000000-0000-0000-0000-000000000000'),
                revision INTEGER NOT NULL DEFAULT 0 CHECK (revision >= 0)
            ) STRICT;
            PRAGMA user_version = 1;
            """;
        migration.ExecuteNonQuery();
        }
        if (version < 2)
        {
        migration.CommandText = """
            CREATE TABLE character_drafts (
                character_id TEXT NOT NULL PRIMARY KEY REFERENCES characters(id) ON DELETE CASCADE,
                name TEXT NOT NULL CHECK (length(name) BETWEEN 1 AND 80 AND name = trim(name))
            ) STRICT;
            PRAGMA user_version = 2;
            """;
        migration.ExecuteNonQuery();
        }
        migration.CommandText = """
            CREATE TABLE character_portraits (
                character_id TEXT NOT NULL PRIMARY KEY REFERENCES characters(id) ON DELETE CASCADE,
                asset TEXT NOT NULL CHECK(length(asset) = 32 AND asset NOT GLOB '*[^0-9a-f]*')
            ) STRICT;
            PRAGMA user_version = 3;
            """;
        migration.ExecuteNonQuery();
        transaction.Commit();
        return backupPath;
    }
}
