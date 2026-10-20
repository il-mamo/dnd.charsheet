using DnD.CharSheet.Domain;

namespace DnD.CharSheet.Application;

/// <summary>Identity-only persistence foundation. Initialize storage before use.</summary>
public interface ICharacterRepository
{
    Task<CharacterIdentity> CreateAsync(CharacterId id, CancellationToken cancellationToken = default);
    Task<CharacterIdentity?> GetAsync(CharacterId id, CancellationToken cancellationToken = default);
    Task<CharacterIdentity> SaveAsync(CharacterIdentity expected, CancellationToken cancellationToken = default);
}

public sealed record CharacterIdentity(CharacterId Id, CharacterRevision Revision);

/// <summary>The identity already exists, or the expected identity/revision no longer exists.</summary>
public sealed class CharacterConflictException(CharacterId id)
    : InvalidOperationException($"Character {id} already exists or has changed. Reload before retrying.");
