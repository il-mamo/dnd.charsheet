namespace DnD.CharSheet.Domain;

/// <summary>Stable mutable-character identity, independent of catalog and UI selection.</summary>
public sealed record CharacterId
{
    public Guid Value { get; }

    public CharacterId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Character identity must not be empty.", nameof(value));
        Value = value;
    }

    public static CharacterId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}
