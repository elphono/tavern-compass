using static BronzebeardHud.Stats.Tests.GuideTestData;

namespace BronzebeardHud.Stats.Tests;

public class GuidePivotsTests
{
    // From: core F1 F2 F3, add-ons FA FB. Every other guide shares a known set of these.
    private static readonly CompGuide From = Guide("From", 2, 0, new[] { "F1", "F2", "F3" }, addons: new[] { "FA", "FB" });

    [Fact]
    public void MostSharedFirst_CoreAndAddOnsCount_EnablersDoNot_AtMostTwo()
    {
        var three = Guide("Three", 3, 0, new[] { "X1", "F3" }, addons: new[] { "F1", "FB" });   // F1 F3 FB
        var two = Guide("Two", 1, 0, new[] { "FA", "Y1" }, addons: new[] { "F2" });             // F2 FA
        var twoMore = Guide("Two more", 2, 1, new[] { "F1", "FB" });                             // F1 FB, after Two in HDT's order
        var one = Guide("One", 1, 1, new[] { "F2" }, enablers: new[] { "F1", "F3" });          // F2 only: enablers are no pivot
        var set = Set(From, three, two, twoMore, one);

        var pivots = GuidePivots.For(From, set, new HashSet<string>());

        Assert.Equal(new[] { "Three", "Two" }, pivots.Select(p => p.To.Name));
        Assert.Equal(new[] { "F1", "F3", "FB" }, pivots[0].Shared); // the source's order: core, then add-ons
        Assert.Equal(new[] { "F2", "FA" }, pivots[1].Shared);
        Assert.Equal("≈ pivot → Three (3 shared) · Two (2 shared)", GuidePivots.Text(pivots));
    }

    [Fact]
    public void ATie_GoesToHdtsOrder_TierFirst()
    {
        // Two guides share two cards each; "Late" comes before "Early" in the arguments but after it in HDT's order.
        var late = Guide("Late", 3, 0, new[] { "F1", "F2" });
        var early = Guide("Early", 1, 5, new[] { "FA", "FB" });
        var set = Set(late, From, early);

        var pivots = GuidePivots.For(From, set, new HashSet<string>());

        Assert.Equal(new[] { "Early", "Late" }, pivots.Select(p => p.To.Name));
        Assert.Equal(new[] { 1, 3 }, pivots.Select(p => p.To.Tier));
    }

    [Fact]
    public void TheHeldCardsComeFirst_AndAreCounted()
    {
        var other = Guide("Other", 2, 1, new[] { "F1", "F2", "F3" });
        var set = Set(From, other);

        var pivot = GuidePivots.For(From, set, new HashSet<string> { "F3", "F2" }).Single();

        Assert.Equal(new[] { "F2", "F3", "F1" }, pivot.Shared);
        Assert.Equal(2, pivot.HeldCount);
    }

    [Fact]
    public void OneCardInCommon_IsNoPivot_AndTheGuideIsNeverItsOwnPivot()
    {
        var one = Guide("One", 2, 1, new[] { "F1", "Z1" });
        var copy = Guide("From", 2, 0, new[] { "F1", "F2", "F3" }, addons: new[] { "FA", "FB" }); // the same guide, read again
        var set = Set(From, one);

        Assert.Empty(GuidePivots.For(From, set, new HashSet<string>()));
        Assert.Empty(GuidePivots.For(From, Set(copy), new HashSet<string>()));
        Assert.Null(GuidePivots.Text(Array.Empty<GuidePivot>()));
    }
}
