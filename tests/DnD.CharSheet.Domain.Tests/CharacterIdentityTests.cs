using DnD.CharSheet.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DnD.CharSheet.Domain.Tests;

[TestClass]
public sealed class CharacterIdentityTests
{
    [TestMethod]
    public void IdentityRejectsEmptyAndPreservesCanonicalValueEquality()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new CharacterId(Guid.Empty));
        var value = Guid.Parse("ABCDEF12-3456-7890-ABCD-EF1234567890");
        Assert.AreEqual(new CharacterId(value), new CharacterId(value));
        Assert.AreEqual("abcdef12-3456-7890-abcd-ef1234567890", new CharacterId(value).ToString());
        var first = CharacterId.New();
        Assert.AreNotEqual(Guid.Empty, first.Value);
        Assert.AreNotEqual(first, CharacterId.New());
    }

    [TestMethod]
    public void RevisionBoundariesAndOverflowAreExplicit()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CharacterRevision(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CharacterRevision(long.MinValue));
        Assert.AreEqual(0L, default(CharacterRevision).Value);
        Assert.AreEqual(1L, new CharacterRevision(0).Next().Value);
        Assert.AreEqual(long.MaxValue, new CharacterRevision(long.MaxValue - 1).Next().Value);
        Assert.ThrowsExactly<OverflowException>(() => new CharacterRevision(long.MaxValue).Next());
    }
}
