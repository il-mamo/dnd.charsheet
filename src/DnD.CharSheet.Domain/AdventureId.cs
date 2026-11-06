namespace DnD.CharSheet.Domain;

/// <summary>Stable Adventure identity; names are never identity keys.</summary>
public sealed record AdventureId
{
    public Guid Value { get; }
    public AdventureId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Adventure identity must not be empty.", nameof(value));
        Value = value;
    }
    public static AdventureId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public sealed record CharacterMembership(CharacterId CharacterId, AdventureId? AdventureId)
{
    public void RequireUsableIn(AdventureId selectedAdventure)
    {
        ArgumentNullException.ThrowIfNull(selectedAdventure);
        if (AdventureId is null || AdventureId != selectedAdventure)
            throw new InvalidOperationException("Assign the character to the selected Adventure before use.");
    }
}
