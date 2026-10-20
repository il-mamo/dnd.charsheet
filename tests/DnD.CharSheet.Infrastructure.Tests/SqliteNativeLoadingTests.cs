using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DnD.CharSheet.Infrastructure.Tests;

[TestClass]
public sealed class SqliteNativeLoadingTests
{
    [TestMethod]
    public void BundledProviderCanOpenAndQueryInMemoryDatabase()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 42;";
        Assert.AreEqual(42L, command.ExecuteScalar());
    }
}
