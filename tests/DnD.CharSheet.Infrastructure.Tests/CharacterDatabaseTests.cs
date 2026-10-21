using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DnD.CharSheet.Infrastructure.Tests;

[TestClass]
public sealed class CharacterDatabaseTests
{
    private string directory = null!;
    private string PathToDatabase => Path.Combine(directory, "nested", "characters.db");

    [TestInitialize]
    public void SetUp() => directory = Path.Combine(Path.GetTempPath(), "charsheet-tests", Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void CleanUp()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    [TestMethod]
    public void FreshDatabaseReopensIdempotentlyAndEnablesForeignKeysOnEveryConnection()
    {
        var database = new CharacterDatabase(PathToDatabase);
        Assert.IsNull(database.Initialize());
        using (var connection = database.OpenConnection())
        {
            Assert.AreEqual((long)CharacterDatabase.CurrentVersion, Scalar(connection, "PRAGMA user_version;"));
            Execute(connection, "INSERT INTO characters(id) VALUES ('12345678-1234-1234-1234-123456789abc');");
            Assert.AreEqual(0L, Scalar(connection, "SELECT revision FROM characters;"));
        }
        Assert.IsNull(new CharacterDatabase(PathToDatabase).Initialize());
        for (var index = 0; index < 2; index++)
        {
            using var connection = database.OpenConnection();
            Assert.AreEqual(1L, Scalar(connection, "PRAGMA foreign_keys;"));
            Assert.AreEqual(1L, Scalar(connection, "SELECT count(*) FROM characters;"));
            Execute(connection, "CREATE TEMP TABLE child(id TEXT REFERENCES characters(id));");
            Assert.ThrowsExactly<SqliteException>(() => Execute(connection, "INSERT INTO child VALUES ('missing');"));
        }
        Assert.IsFalse(Directory.Exists(Path.Combine(Path.GetDirectoryName(PathToDatabase)!, "migration-backups")));
    }

    [TestMethod]
    public void SchemaRejectsMalformedIdentitiesAndInvalidRevisions()
    {
        var database = new CharacterDatabase(PathToDatabase);
        database.Initialize();
        using var connection = database.OpenConnection();
        foreach (var id in new[] { "", "00000000-0000-0000-0000-000000000000", "ABCDEF12-3456-7890-ABCD-EF1234567890", "12345678123412341234123456789abc", "g2345678-1234-1234-1234-123456789abc", "12345678--234-1234-1234-123456789abc" })
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO characters(id) VALUES ($id);";
            command.Parameters.AddWithValue("$id", id);
            Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery(), id);
        }
        foreach (var revision in new[] { "-1", "NULL", "1.5", "'invalid'", "9223372036854775808" })
            Assert.ThrowsExactly<SqliteException>(() => Execute(connection, $"INSERT INTO characters VALUES ('12345678-1234-1234-1234-123456789abc', {revision});"), revision);
        Execute(connection, "INSERT INTO characters VALUES ('12345678-1234-1234-1234-123456789abc', 9223372036854775807);");
        Assert.AreEqual(long.MaxValue, Scalar(connection, "SELECT revision FROM characters;"));
        Assert.ThrowsExactly<SqliteException>(() => Execute(connection, "INSERT INTO characters VALUES ('12345678-1234-1234-1234-123456789abc', 0);"));
    }

    [TestMethod]
    public void ExistingWalDatabaseGetsReadablePreMigrationSnapshotIncludingCommittedWalData()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathToDatabase)!);
        using var original = OpenRaw(PathToDatabase);
        Execute(original, "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; CREATE TABLE legacy(value TEXT); INSERT INTO legacy VALUES ('preserved');");
        Assert.IsTrue(File.Exists(PathToDatabase + "-wal"));
        var database = new CharacterDatabase(PathToDatabase);
        var backup = database.Initialize();
        Assert.IsNotNull(backup);
        using var snapshot = OpenRaw(backup);
        Assert.AreEqual("preserved", Scalar(snapshot, "SELECT value FROM legacy;"));
        Assert.AreEqual(0L, Scalar(snapshot, "PRAGMA user_version;"));
        Assert.AreEqual("ok", Scalar(snapshot, "PRAGMA integrity_check;"));
        Assert.AreEqual(0L, Scalar(snapshot, "SELECT count(*) FROM sqlite_master WHERE name='characters';"));
        using var current = database.OpenConnection();
        Assert.AreEqual("preserved", Scalar(current, "SELECT value FROM legacy;"));
        Assert.AreEqual((long)CharacterDatabase.CurrentVersion, Scalar(current, "PRAGMA user_version;"));
    }

    [TestMethod]
    public void FailedMigrationPreservesOriginalSchemaDataAndVersionAndKeepsBackup()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathToDatabase)!);
        using (var original = OpenRaw(PathToDatabase))
            Execute(original, "CREATE TABLE characters(legacy TEXT); INSERT INTO characters VALUES ('original');");
        Assert.ThrowsExactly<SqliteException>(() => new CharacterDatabase(PathToDatabase).Initialize());
        using var current = OpenRaw(PathToDatabase);
        Assert.AreEqual(0L, Scalar(current, "PRAGMA user_version;"));
        Assert.AreEqual("original", Scalar(current, "SELECT legacy FROM characters;"));
        var backups = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(PathToDatabase)!, "migration-backups"));
        Assert.HasCount(1, backups);
        using var snapshot = OpenRaw(backups[0]);
        Assert.AreEqual("original", Scalar(snapshot, "SELECT legacy FROM characters;"));
    }

    [TestMethod]
    public void BackupFailureAbortsBeforeChangingExistingDatabase()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathToDatabase)!);
        using (var original = OpenRaw(PathToDatabase))
            Execute(original, "CREATE TABLE legacy(value TEXT); INSERT INTO legacy VALUES ('safe');");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(PathToDatabase)!, "migration-backups"), "directory blocked");
        var before = File.ReadAllBytes(PathToDatabase);
        Assert.ThrowsExactly<IOException>(() => new CharacterDatabase(PathToDatabase).Initialize());
        CollectionAssert.AreEqual(before, File.ReadAllBytes(PathToDatabase));
        using var current = OpenRaw(PathToDatabase);
        Assert.AreEqual(0L, Scalar(current, "PRAGMA user_version;"));
        Assert.AreEqual("safe", Scalar(current, "SELECT value FROM legacy;"));
    }

    [TestMethod]
    public void UnsupportedFutureDatabaseRemainsByteForByteIntact()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathToDatabase)!);
        using (var original = OpenRaw(PathToDatabase))
            Execute(original, $"CREATE TABLE future(value TEXT); INSERT INTO future VALUES ('untouched'); PRAGMA user_version={CharacterDatabase.CurrentVersion + 1};");
        var before = File.ReadAllBytes(PathToDatabase);
        Assert.ThrowsExactly<InvalidOperationException>(() => new CharacterDatabase(PathToDatabase).Initialize());
        CollectionAssert.AreEqual(before, File.ReadAllBytes(PathToDatabase));
        Assert.IsFalse(Directory.Exists(Path.Combine(Path.GetDirectoryName(PathToDatabase)!, "migration-backups")));
    }

    private static SqliteConnection OpenRaw(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
