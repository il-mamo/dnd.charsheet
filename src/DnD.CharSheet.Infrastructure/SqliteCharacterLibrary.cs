using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;

namespace DnD.CharSheet.Infrastructure;

/// <summary>Atomic draft creation and consistent library reads, off the UI thread.</summary>
public sealed class SqliteCharacterLibrary(CharacterDatabase database) : ICharacterLibrary
{
    private readonly CharacterDatabase database = database ?? throw new ArgumentNullException(nameof(database));

    public Task<IReadOnlyList<CharacterDraft>> ListAsync(CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<CharacterDraft>>(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT c.id, c.revision, COALESCE(d.name, 'Unnamed draft') FROM characters c LEFT JOIN character_drafts d ON d.character_id = c.id ORDER BY COALESCE(d.name, 'Unnamed draft') COLLATE NOCASE, c.id;";
        using var reader = command.ExecuteReader();
        var result = new List<CharacterDraft>();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(new CharacterDraft(new CharacterId(Guid.Parse(reader.GetString(0))), new CharacterRevision(reader.GetInt64(1)), reader.GetString(2)));
        }
        return result;
    }, cancellationToken);

    public Task<CharacterDraft> CreateDraftAsync(CharacterId id, string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        var normalized = DraftName.Normalize(name);
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
            command.CommandText = "INSERT INTO character_drafts(character_id, name) VALUES ($id, $name);";
            command.Parameters.AddWithValue("$name", normalized);
            command.ExecuteNonQuery();
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return new CharacterDraft(id, new CharacterRevision(0), normalized);
        }, cancellationToken);
    }
}
