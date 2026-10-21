using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;

namespace DnD.CharSheet.Infrastructure;

public sealed class ManagedCharacterPortraits(CharacterDatabase database, string assetDirectory) : ICharacterPortraits
{
    public const int MaximumBytes = 10 * 1024 * 1024;
    private readonly string assetDirectory = Path.GetFullPath(assetDirectory);

    private string AssetPath(string token)
    {
        if (!Guid.TryParseExact(token, "N", out _)) throw new InvalidDataException("Invalid portrait reference.");
        return Path.Combine(assetDirectory, token + ".png");
    }

    public Task<CharacterPortrait> ReadAsync(CharacterId id, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT asset FROM character_portraits WHERE character_id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        var token = command.ExecuteScalar() as string;
        byte[]? bytes = null;
        if (token is not null)
        {
            try
            {
                var path = AssetPath(token);
                using var stream = File.OpenRead(path);
                if (stream.Length <= MaximumBytes)
                {
                    bytes = new byte[(int)stream.Length];
                    stream.ReadExactly(bytes);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new CharacterPortrait(token, bytes);
    }, cancellationToken);

    public Task<CharacterPortrait> SaveAsync(CharacterId id, string? expectedToken, byte[]? png, CancellationToken cancellationToken = default)
    {
        // Take ownership of a snapshot before scheduling work.
        var content = png?.ToArray();
        if (content is not null && (content.Length is < 8 or > MaximumBytes ||
            !content.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })))
            throw new ArgumentException("Portrait must be a normalized PNG up to 10 MiB.", nameof(png));
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var token = content is null ? null : Guid.NewGuid().ToString("N");
            using var connection = database.OpenConnection();
            using var transaction = connection.BeginTransaction(deferred: false);
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT count(*) FROM characters WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id.ToString());
            if (Convert.ToInt64(command.ExecuteScalar()) != 1) throw new CharacterConflictException(id);
            command.CommandText = "SELECT asset FROM character_portraits WHERE character_id = $id;";
            if ((command.ExecuteScalar() as string) != expectedToken) throw new CharacterConflictException(id);
            if (token is not null)
            {
                Directory.CreateDirectory(assetDirectory);
                // CreateNew and immutable names ensure a failed update cannot alter the current image.
                using var file = new FileStream(AssetPath(token), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                file.Write(content!);
                file.Flush(flushToDisk: true);
            }
            command.CommandText = token is null
                ? "DELETE FROM character_portraits WHERE character_id = $id;"
                : "INSERT INTO character_portraits(character_id, asset) VALUES ($id, $asset) ON CONFLICT(character_id) DO UPDATE SET asset = excluded.asset;";
            if (token is not null) command.Parameters.AddWithValue("$asset", token);
            command.ExecuteNonQuery();
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return new CharacterPortrait(token, content);
        }, cancellationToken);
    }
}
