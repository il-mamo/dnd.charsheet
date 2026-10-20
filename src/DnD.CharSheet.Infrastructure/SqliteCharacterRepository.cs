using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;

namespace DnD.CharSheet.Infrastructure;

/// <summary>Runs synchronous SQLite work on workers. SQLite immediate transactions serialize
/// writers across repository instances. Cancellation is honored before commit; after commit
/// the successful result is returned even if cancellation subsequently arrives.</summary>
public sealed class SqliteCharacterRepository(CharacterDatabase database) : ICharacterRepository
{
    private readonly CharacterDatabase database = database ?? throw new ArgumentNullException(nameof(database));

    public Task<CharacterIdentity> CreateAsync(CharacterId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = database.OpenConnection();
            using var transaction = connection.BeginTransaction(deferred: false);
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO characters(id, revision) VALUES ($id, 0) ON CONFLICT(id) DO NOTHING;";
            command.Parameters.AddWithValue("$id", id.ToString());
            if (command.ExecuteNonQuery() != 1) throw new CharacterConflictException(id);
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return new CharacterIdentity(id, new CharacterRevision(0));
        }, cancellationToken);
    }

    public Task<CharacterIdentity?> GetAsync(CharacterId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT revision FROM characters WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id.ToString());
            var value = command.ExecuteScalar();
            cancellationToken.ThrowIfCancellationRequested();
            return value is long revision ? new CharacterIdentity(id, new CharacterRevision(revision)) : null;
        }, cancellationToken);
    }

    public Task<CharacterIdentity> SaveAsync(CharacterIdentity expected, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(expected.Id);
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var next = expected.Revision.Next();
            using var connection = database.OpenConnection();
            using var transaction = connection.BeginTransaction(deferred: false);
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE characters SET revision = $next WHERE id = $id AND revision = $expected;";
            command.Parameters.AddWithValue("$id", expected.Id.ToString());
            command.Parameters.AddWithValue("$expected", expected.Revision.Value);
            command.Parameters.AddWithValue("$next", next.Value);
            if (command.ExecuteNonQuery() != 1) throw new CharacterConflictException(expected.Id);
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return expected with { Revision = next };
        }, cancellationToken);
    }
}
