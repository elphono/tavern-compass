namespace BronzebeardHud.Stats.Tests;

/// <summary>Synthetic comp guides: every guide has its own cards, so that a wrong attribution shows.</summary>
public class CompGuideMatchTests
{
    private static CompGuide Guide(string name, int tier, int tierRank, string[] core, string[]? addons = null, string[]? enablers = null) =>
        new(name, tier, tierRank, core, addons ?? Array.Empty<string>(), enablers ?? Array.Empty<string>(), Array.Empty<string>());

    private static CompGuideSet Set(params CompGuide[] guides) =>
        new(CompGuideSources.HdtFree, guides.GroupBy(g => g.Tier).OrderBy(g => g.Key).Select(g => new CompGuideTier(g.Key, g.ToList())).ToList());

    private static PlayerCards Board(params string[] ids) => new(ids.Select(id => new OwnedCard(id)).ToList(), Array.Empty<OwnedCard>());

    private static PlayerCards Hand(params string[] ids) => new(Array.Empty<OwnedCard>(), ids.Select(id => new OwnedCard(id)).ToList());

    private static IEnumerable<string> Names(IEnumerable<CompGuideProgress> rows) => rows.Select(r => r.Guide.Name);

    // Three guides, three tiers' worth of distinct cards.
    private static readonly CompGuide Keys = Guide("Keys", 2, 0, new[] { "K1", "K2", "K3", "K4" }, addons: new[] { "KA" });
    private static readonly CompGuide Enabled = Guide("Enabled", 1, 0, new[] { "E_CORE1", "E_CORE2" }, enablers: new[] { "EN1" });
    private static readonly CompGuide Untouched = Guide("Untouched", 1, 1, new[] { "U1" }, addons: new[] { "UA" }, enablers: new[] { "UEN" });

    [Fact]
    public void TwoKeyPiecesHeld_OutrankOneEnablerHeld_WhateverTheTier()
    {
        var board = CompGuideMatch.Rank(Set(Keys, Enabled, Untouched), Board("K1", "K2", "EN1"));

        Assert.Equal(new[] { "Keys", "Enabled" }, Names(board.Highlighted)); // 6 points before 2, though "Enabled" is S tier
        Assert.Equal(new int?[] { 1, 2 }, board.Highlighted.Select(p => p.Highlight));
        Assert.Equal(new[] { 6.0, 2.0 }, board.Highlighted.Select(p => p.Score));
        Assert.Equal(new[] { 1, 2 }, board.Tiers.Select(t => t.Tier)); // the tiers keep HDT's order
        Assert.Equal("★2/4", board.Highlighted[0].HeldText);
        Assert.Equal(new[] { "K3", "K4" }, board.Highlighted[0].KeyMissing);
    }

    [Fact]
    public void OneKeyPiece_OutweighsTwoAddOns_WhichOutweighOneAddOn()
    {
        var oneKey = Guide("One key", 3, 0, new[] { "OK1", "OK2" });
        var twoAddons = Guide("Two add-ons", 3, 1, new[] { "TA_CORE" }, addons: new[] { "TA1", "TA2" });
        var oneAddon = Guide("One add-on", 3, 2, new[] { "OA_CORE" }, addons: new[] { "OA1" });

        var board = CompGuideMatch.Rank(Set(oneAddon, twoAddons, oneKey), Board("OA1", "TA1", "TA2", "OK1"));

        Assert.Equal(new[] { "One key", "Two add-ons", "One add-on" }, Names(board.Highlighted));
        Assert.Equal(new[] { 3.0, 2.0, 1.0 }, board.Highlighted.Select(p => p.Score));
        Assert.Equal("★0/1 +2", board.Highlighted[1].HeldText);
    }

    [Fact]
    public void ACardHeldOnlyInHand_CountsAsMuchAsOnTheBoard()
    {
        var set = Set(Keys, Enabled, Untouched);

        var fromHand = CompGuideMatch.Rank(set, Hand("U1"));
        var fromBoard = CompGuideMatch.Rank(set, Board("U1"));
        var none = CompGuideMatch.Rank(set, PlayerCards.None);

        Assert.Equal(new[] { "Untouched" }, Names(fromHand.Highlighted));
        Assert.Equal(fromBoard.Highlighted.Single().Score, fromHand.Highlighted.Single().Score);
        Assert.Equal(3.0, fromHand.Highlighted.Single().Score);
        Assert.Empty(none.Highlighted);
    }

    [Fact]
    public void NothingHeld_NothingHighlighted_HdtOrderKept()
    {
        var board = CompGuideMatch.Rank(Set(Keys, Enabled, Untouched), PlayerCards.None);

        Assert.Empty(board.Highlighted);
        Assert.Equal(new[] { "Enabled", "Untouched", "Keys" }, Names(board.Tiers.SelectMany(t => t.Rows)));
        Assert.All(board.Tiers.SelectMany(t => t.Rows), r => Assert.Equal((0.0, false, ""), (r.Score, r.IsHighlighted, r.HeldText)));
        Assert.Equal(new[] { "S", "A" }, board.Tiers.Select(t => t.Letter));
    }

    [Fact]
    public void ACardInSeveralRoles_CountsOnceInItsStrongest()
    {
        var both = Guide("Both", 2, 0, new[] { "B1", "B2" }, addons: new[] { "BA" }, enablers: new[] { "B1", "BA" });

        var progress = CompGuideMatch.Progress(both, new HashSet<string> { "B1", "BA" });

        Assert.Equal(new[] { "B1" }, progress.KeyHeld);      // key piece and enabler: a key piece only
        Assert.Equal(new[] { "BA" }, progress.EnablersHeld); // add-on and enabler: an enabler only
        Assert.Empty(progress.AddonsHeld);
        Assert.Equal(CompGuideMatch.KeyWeight + CompGuideMatch.EnablerWeight, progress.Score);
        Assert.Null(typeof(CompGuideProgress).GetProperty("Held")); // no list mixing the roles (see CompGuideProgress)
    }

    [Fact]
    public void HighlightedGuides_ComeFirstInTheirTier_AtMostThree_TiesToTheLargerShareOfKeyPieces()
    {
        var s1 = Guide("S first", 1, 0, new[] { "S1A" });
        var s2 = Guide("S second", 1, 1, new[] { "S2A", "S2B", "S2C" });       // one of three key pieces: 3 points, share 1/3
        var s3 = Guide("S third", 1, 2, new[] { "S3A" });                     // one of one: 3 points, share 1
        var a1 = Guide("A first", 2, 0, new[] { "A1A" }, addons: new[] { "A1X" });
        var a2 = Guide("A second", 2, 1, new[] { "A2A", "A2B" });              // one of two: 3 points, share 1/2
        var a3 = Guide("A third", 2, 2, new[] { "A3A" }, addons: new[] { "A3X" }); // an add-on: 1 point, fourth

        var board = CompGuideMatch.Rank(Set(s1, s2, s3, a1, a2, a3), Board("S2A", "S3A", "A2A", "A3X"));

        Assert.Equal(new[] { "S third", "A second", "S second" }, Names(board.Highlighted));
        Assert.Equal(new[] { "S third", "S second", "S first" }, Names(board.Tiers[0].Rows));
        Assert.Equal(new[] { "A second", "A first", "A third" }, Names(board.Tiers[1].Rows)); // "A third" scores 1 but is not among the three
        Assert.Equal(1.0, board.Tiers[1].Rows[2].Score);
        Assert.False(board.Tiers[1].Rows[2].IsHighlighted);
        Assert.Equal("[1. S third 3 ★1/1; 2. A second 3 ★1/2; 3. S second 3 ★1/3]", board.HighlightSummary);
    }

    [Fact]
    public void AGoldenCopy_CountsAsItsBaseCard()
    {
        var board = CompGuideMatch.Rank(Set(Keys, Enabled, Untouched), Board("K1_G"));

        Assert.Equal(new[] { "Keys" }, Names(board.Highlighted));
    }

    [Fact]
    public void Stateless_AGuideDroppedComesBackWhenTheCardsPointToItAgain()
    {
        var set = Set(Keys, Enabled, Untouched);

        var first = CompGuideMatch.Rank(set, Board("K1"));
        var second = CompGuideMatch.Rank(set, Board("E_CORE1"));
        var third = CompGuideMatch.Rank(set, Board("K1"));

        Assert.Equal(new[] { "Keys" }, Names(first.Highlighted));
        Assert.Equal(new[] { "Enabled" }, Names(second.Highlighted));
        Assert.Equal(new[] { "Keys" }, Names(third.Highlighted));
    }
}
