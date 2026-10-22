using DnD.CharSheet.Domain;

namespace DnD.CharSheet.Application;

/// <summary>Unfinished input, or an explicitly submitted creation with a stable retry identity.</summary>
public sealed record DraftRecovery(string Name, CharacterId? SubmittedId = null);

public interface IDraftRecovery
{
    Task<DraftRecovery?> LoadAsync();
    Task SaveAsync(DraftRecovery? draft);
}
