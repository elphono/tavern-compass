namespace BronzebeardHud.Stats.Tests;

public class CompTransitionsTests
{
    private static Composition Comp(string id, double placement, string[] core, string[] addons, string[]? board = null) =>
        new(id, id.ToUpperInvariant(), Array.Empty<string>(), core, addons, averagePlacement: placement, referenceBoard: board);

    // From FROM: STRONG shares four cards, WEAK two, ONE a single card, NONE nothing; WEAK2 two as well, better placed.
    private static readonly Composition From = Comp("from", 3.6, new[] { "K1", "K2", "K3" }, new[] { "A1", "A2" }, new[] { "K1", "K2", "B1", "K3", "A1" });
    private static readonly Composition Strong = Comp("strong", 3.9, new[] { "K1", "K2" }, new[] { "A1" }, new[] { "B1", "X1" });
    private static readonly Composition Weak = Comp("weak", 3.8, new[] { "K3", "Y1" }, new[] { "A2" });
    private static readonly Composition Weak2 = Comp("weak2", 3.2, new[] { "Y2", "B1" }, new[] { "A2" });
    private static readonly Composition One = Comp("one", 2.9, new[] { "A1", "Z1" }, Array.Empty<string>());
    private static readonly Composition None = Comp("none", 3.0, new[] { "Z2" }, new[] { "Z3" });

    [Fact]
    public void MostSharedFirst_ThenPlacement_TwoAtMost_OneCardOrNoneIsNoPivot()
    {
        var all = new[] { From, Strong, Weak, Weak2, One, None };

        var transitions = CompTransitions.For(From, all);

        Assert.Equal(new[] { ("strong", 4), ("weak2", 2) }, transitions.Select(t => (t.To.Id, t.Shared.Count)));
        Assert.Equal(new[] { "K1", "K2", "A1", "B1" }, transitions[0].Shared); // key pieces, add-ons, then the final board
        Assert.Equal("≈ pivot → STRONG (4 shared) · WEAK2 (2 shared)", CompTransitions.Text(transitions));

        // Without the better-placed WEAK2, WEAK takes the second place; ONE and NONE never show.
        Assert.Equal(new[] { "strong", "weak" }, CompTransitions.For(From, new[] { From, Strong, Weak, One, None }).Select(t => t.To.Id));
        Assert.Empty(CompTransitions.For(From, new[] { From, One, None }));
        Assert.Null(CompTransitions.Text(Array.Empty<CompTransition>()));
    }

    [Fact]
    public void GoldenCopiesCount_AndACompNeverPivotsToItself()
    {
        var golden = Comp("golden", 4.0, new[] { "K1_G", "K2_G" }, Array.Empty<string>());
        Assert.Equal(new[] { "K1", "K2" }, CompTransitions.For(From, new[] { From, golden }).Single().Shared);
        Assert.DoesNotContain(CompTransitions.For(From, new[] { From }), t => t.To.Id == "from");
    }
}
