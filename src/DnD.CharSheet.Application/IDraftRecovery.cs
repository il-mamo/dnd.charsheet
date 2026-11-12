using DnD.CharSheet.Domain;

namespace DnD.CharSheet.Application;

/// <summary>Unfinished input, or an explicitly submitted creation with a stable retry identity.</summary>
public sealed record DraftRecovery(string Name, CharacterId? SubmittedId = null, AdventureId? AdventureId = null);

public interface IDraftRecovery
{
    Task<DraftRecovery?> LoadAsync();
    Task SaveAsync(DraftRecovery? draft);
    async Task<IReadOnlyList<DraftRecovery>> LoadAllAsync()
    {
        var draft = await LoadAsync();
        return draft is null ? [] : [draft];
    }
    Task SaveAllAsync(IReadOnlyList<DraftRecovery> drafts) => drafts.Count <= 1
        ? SaveAsync(drafts.SingleOrDefault())
        : throw new NotSupportedException("This recovery store does not support multiple Adventures.");
}
