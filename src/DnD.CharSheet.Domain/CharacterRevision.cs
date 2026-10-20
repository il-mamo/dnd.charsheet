namespace DnD.CharSheet.Domain;

/// <summary>Nonnegative optimistic concurrency token; repositories enforce expected revisions.</summary>
public readonly record struct CharacterRevision
{
    public long Value { get; }

    public CharacterRevision(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public CharacterRevision Next() => new(checked(Value + 1));
}
