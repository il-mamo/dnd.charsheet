using DnD.CharSheet.Domain;

namespace DnD.CharSheet.Application;

public sealed record CharacterPortrait(string? Token, byte[]? Png);

/// <summary>Managed normalized PNG copies. Token is an opaque optimistic concurrency value.</summary>
public interface ICharacterPortraits
{
    Task<CharacterPortrait> ReadAsync(CharacterId id, CancellationToken cancellationToken = default);
    Task<CharacterPortrait> SaveAsync(CharacterId id, string? expectedToken, byte[]? png, CancellationToken cancellationToken = default);
}
