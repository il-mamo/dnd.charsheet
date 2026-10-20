using DnD.CharSheet.Domain;

namespace DnD.CharSheet.Application;

/// <summary>A saved, incomplete character. Names are identity aids, not handbook rules.</summary>
public sealed record CharacterDraft(CharacterId Id, CharacterRevision Revision, string Name);

public static class DraftName
{
    public const int MaximumLength = 80;
    public static string Normalize(string? value)
    {
        var name = value?.Trim() ?? "";
        if (name.Length is < 1 or > MaximumLength || (value?.Any(char.IsControl) ?? false))
            throw new ArgumentException("Enter a name of 1–80 characters without control characters.", nameof(value));
        return name;
    }
}

public interface ICharacterLibrary
{
    Task<IReadOnlyList<CharacterDraft>> ListAsync(CancellationToken cancellationToken = default);
    Task<CharacterDraft> CreateDraftAsync(CharacterId id, string name, CancellationToken cancellationToken = default);
}


