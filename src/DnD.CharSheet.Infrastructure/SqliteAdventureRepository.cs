using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;
using Microsoft.Data.Sqlite;

namespace DnD.CharSheet.Infrastructure;

/// <summary>Worker-thread management operations serialized by immediate SQLite transactions.</summary>
public sealed class SqliteAdventureRepository(CharacterDatabase database) : IAdventureRepository
{
    private readonly CharacterDatabase database = database ?? throw new ArgumentNullException(nameof(database));

    private Task<T> Run<T>(Func<SqliteCommand, T> action, CancellationToken token) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var result = action(command);
        token.ThrowIfCancellationRequested();
        transaction.Commit();
        return result;
    }, token);

    private static void Parameter(SqliteCommand command, string name, object value) => command.Parameters.AddWithValue(name, value);
    private static void ExecuteOne(SqliteCommand command, string sql)
    {
        command.CommandText = sql;
        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Adventure or character membership changed. Reload before retrying.");
    }

    public Task<IReadOnlyList<Adventure>> ListAsync(CancellationToken cancellationToken = default) => Run<IReadOnlyList<Adventure>>(command =>
    {
        command.CommandText = "SELECT id, name FROM adventures ORDER BY name COLLATE NOCASE, id";
        using var reader = command.ExecuteReader();
        var result = new List<Adventure>();
        while (reader.Read()) result.Add(new(new AdventureId(Guid.Parse(reader.GetString(0))), reader.GetString(1)));
        return result;
    }, cancellationToken);

    public Task<CharacterMembership> GetMembershipAsync(CharacterId characterId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(characterId);
        return Run(command =>
        {
            Parameter(command, "$character", characterId.ToString());
            command.CommandText = "SELECT m.adventure_id FROM characters c LEFT JOIN adventure_memberships m ON m.character_id=c.id WHERE c.id=$character";
            var value = command.ExecuteScalar();
            if (value is null) throw new InvalidOperationException("Character no longer exists.");
            return new CharacterMembership(characterId, value is string id ? new AdventureId(Guid.Parse(id)) : null);
        }, cancellationToken);
    }

    public Task CreateWithUnassignedAsync(AdventureId id, string name, CharacterId firstMember, CancellationToken cancellationToken = default) =>
        Create(id, name, firstMember, null, cancellationToken);
    public Task CreateWithDraftAsync(AdventureId id, string name, CharacterId firstMember, string characterName, CancellationToken cancellationToken = default) =>
        Create(id, name, firstMember, DraftName.Normalize(characterName), cancellationToken);

    private Task Create(AdventureId id, string name, CharacterId firstMember, string? draftName, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(firstMember);
        var normalized = DraftName.Normalize(name);
        return Run(command =>
        {
            Parameter(command, "$id", id.ToString());
            Parameter(command, "$name", normalized);
            Parameter(command, "$character", firstMember.ToString());
            if (draftName is not null)
            {
                ExecuteOne(command, "INSERT INTO characters(id) VALUES ($character)");
                Parameter(command, "$draft", draftName);
                ExecuteOne(command, "INSERT INTO character_drafts(character_id,name) VALUES ($character,$draft)");
            }
            ExecuteOne(command, "INSERT INTO adventures(id,name,first_character_id) VALUES ($id,$name,$character)");
            ExecuteOne(command, "INSERT INTO adventure_memberships(character_id,adventure_id) SELECT id,$id FROM characters WHERE id=$character AND NOT EXISTS (SELECT 1 FROM adventure_memberships WHERE character_id=$character)");
            return true;
        }, token);
    }

    public Task AssignAsync(CharacterId unassignedCharacter, AdventureId destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unassignedCharacter);
        ArgumentNullException.ThrowIfNull(destination);
        return Run(command =>
        {
            Parameter(command, "$character", unassignedCharacter.ToString());
            Parameter(command, "$id", destination.ToString());
            ExecuteOne(command, "INSERT INTO adventure_memberships(character_id,adventure_id) SELECT id,$id FROM characters WHERE id=$character AND NOT EXISTS (SELECT 1 FROM adventure_memberships WHERE character_id=$character)");
            return true;
        }, cancellationToken);
    }

    public Task RenameAsync(AdventureId id, string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        var normalized = DraftName.Normalize(name);
        return Run(command =>
        {
            Parameter(command, "$id", id.ToString());
            Parameter(command, "$name", normalized);
            ExecuteOne(command, "UPDATE adventures SET name=$name WHERE id=$id");
            return true;
        }, cancellationToken);
    }

    public Task DeleteAsync(AdventureId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Run(command =>
        {
            Parameter(command, "$id", id.ToString());
            ExecuteOne(command, "DELETE FROM adventures WHERE id=$id");
            return true;
        }, cancellationToken);
    }
}
