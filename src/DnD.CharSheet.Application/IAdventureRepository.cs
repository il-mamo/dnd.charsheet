using DnD.CharSheet.Domain;

namespace DnD.CharSheet.Application;

public sealed record Adventure(AdventureId Id, string Name);

/// <summary>Management storage. Confirmation and session selection belong to the caller.
/// Existing unassigned characters remain manageable but cannot pass RequireUsableIn.</summary>
public interface IAdventureRepository
{
    Task<IReadOnlyList<Adventure>> ListAsync(CancellationToken cancellationToken = default);
    Task<CharacterMembership> GetMembershipAsync(CharacterId characterId, CancellationToken cancellationToken = default);
    Task CreateWithUnassignedAsync(AdventureId id, string name, CharacterId firstMember, CancellationToken cancellationToken = default);
    Task CreateWithDraftAsync(AdventureId id, string name, CharacterId firstMember, string characterName, CancellationToken cancellationToken = default);
    Task AssignAsync(CharacterId unassignedCharacter, AdventureId destination, CancellationToken cancellationToken = default);
    Task RenameAsync(AdventureId id, string name, CancellationToken cancellationToken = default);
    Task DeleteAsync(AdventureId id, CancellationToken cancellationToken = default);
}
