namespace BronzebeardHud.Stats.Tests;

public class CompDetailTests
{
    // Synthetic tiers, every card distinct. Tier 3 is the boundary (kept), tier 4 the first one left out;
    // UNKNOWN has no tier at all.
    private static readonly Dictionary<string, int> Tiers = new()
    {
        ["KEY_T5"] = 5,
        ["KEY_T3"] = 3,
        ["ADD_T2"] = 2,
        ["ADD_T4"] = 4,
        ["ADD_T1"] = 1,
        ["EARLY_T1"] = 1,
        ["EARLY_T3"] = 3,
        ["EARLY_T2"] = 2,
        ["LATE_T4"] = 4,
        ["LATE_T6"] = 6,
    };

    private static int? TierOf(string cardId) => Tiers.TryGetValue(cardId, out var tier) ? tier : null;

    // Five final boards. Boards holding each card: EARLY_T1 4, KEY_T5 4, KEY_T3 3 (one golden copy), EARLY_T3 3,
    // EARLY_T2 2, ADD_T2 1, ADD_T1 1, LATE_T4 1, LATE_T6 1, UNKNOWN 1, ADD_T4 0. Turns 12, 10, 14, none, 11.
    private static readonly Composition Comp = new("undead_test", "Undead Test", new[] { "UNDEAD", "BEAST" },
        new[] { "KEY_T5", "KEY_T3" }, new[] { "ADD_T2", "ADD_T4", "ADD_T1" }, averagePlacement: 3.84, dataPoints: 1234, tier: "A",
        finalBoards: new[]
        {
            new FinalBoard(9100, 12, new[] { "EARLY_T1", "KEY_T5", "KEY_T3", "EARLY_T3", "ADD_T2", "LATE_T4" }),
            new FinalBoard(9000, 10, new[] { "EARLY_T1", "KEY_T5", "KEY_T3_G", "EARLY_T3", "LATE_T6" }),
            new FinalBoard(8900, 14, new[] { "EARLY_T1", "KEY_T5", "ADD_T1", "EARLY_T2" }),
            new FinalBoard(8800, null, new[] { "EARLY_T1", "KEY_T3", "EARLY_T2" }),
            new FinalBoard(8700, 11, new[] { "EARLY_T3", "KEY_T5", "UNKNOWN" }),
        });

    [Fact]
    public void EarlyEnablers_TierThreeAtMost_MostSeenOnFinalBoardsFirst()
    {
        var detail = CompDetail.For(Comp, TierOf);

        // Tier 4 and above (ADD_T4, LATE_T4, LATE_T6, KEY_T5) and the tierless UNKNOWN are left out; tier 3 is kept.
        // 4 boards, then 3 (KEY_T3 before EARLY_T3: first seen earlier), 2, then 1 (add-ons in their order).
        Assert.Equal(new[] { "EARLY_T1", "KEY_T3", "EARLY_T3", "EARLY_T2", "ADD_T2", "ADD_T1" }, detail.EarlyEnablers.Select(c => c.CardId));
        Assert.Equal(new[] { 4, 3, 3, 2, 1, 1 }, detail.EarlyEnablers.Select(c => c.FinalBoards));
        Assert.Equal(new int?[] { 1, 3, 3, 2, 2, 1 }, detail.EarlyEnablers.Select(c => c.TechLevel));
    }

    // Ali's decision of 2026-09-27: a key piece of tier ≤ 3 stays among the early enablers, even though it is also listed under "When to commit". Not an oversight.
    [Fact]
    public void EarlyEnablers_KeepKeyPiecesOfTierThreeOrLess_ByDesign()
    {
        var detail = CompDetail.For(Comp, TierOf);

        Assert.Contains("KEY_T3", detail.CommitCards.Select(c => c.CardId));
        Assert.Contains("KEY_T3", detail.EarlyEnablers.Select(c => c.CardId));
        Assert.DoesNotContain("KEY_T5", detail.EarlyEnablers.Select(c => c.CardId)); // tier 5: a key piece, not an early one
    }

    [Fact]
    public void EarlyEnablers_AreCappedAfterSorting()
    {
        var detail = CompDetail.For(Comp, TierOf, maxEnablers: 3);

        Assert.Equal(new[] { "EARLY_T1", "KEY_T3", "EARLY_T3" }, detail.EarlyEnablers.Select(c => c.CardId));
    }

    [Fact]
    public void WhenToCommit_IsTheKeyPieces_WithTheirTier()
    {
        var detail = CompDetail.For(Comp, TierOf);

        Assert.Equal(new[] { ("KEY_T5", (int?)5, 4), ("KEY_T3", (int?)3, 3) }, detail.CommitCards.Select(c => (c.CardId, c.TechLevel, c.FinalBoards)));
    }

    [Fact]
    public void TypicalFinalTurn_IsTheMedianOfTheTurnsGiven()
    {
        // 10, 11, 12, 14 (the board without a turn is left out): (11 + 12) / 2.
        var even = CompDetail.For(Comp, TierOf);
        Assert.Equal(11.5, even.TypicalFinalTurn);
        Assert.Equal("Typical final turn: 11,5", even.TypicalFinalTurnText);

        // An odd count gives the middle value, not the mean (7 + 9 + 16) / 3 = 10,7.
        var odd = new Composition("odd", "Odd", Array.Empty<string>(), new[] { "KEY_T5" }, Array.Empty<string>(), finalBoards: new[]
        {
            new FinalBoard(1, 16, new[] { "KEY_T5" }), new FinalBoard(2, 7, new[] { "KEY_T5" }), new FinalBoard(3, 9, new[] { "KEY_T5" }),
        });
        Assert.Equal(9.0, CompDetail.For(odd, TierOf).TypicalFinalTurn);
        Assert.Equal("Typical final turn: 9", CompDetail.For(odd, TierOf).TypicalFinalTurnText);
    }

    [Fact]
    public void HeaderAndNote_SayWhatTheDetailIsDerivedFrom()
    {
        var detail = CompDetail.For(Comp, TierOf);

        Assert.Equal("Undead Test · Undead, Beast · avg place 3,8 · 1 234 games · tier A", detail.Header);
        Assert.Equal("Undead, Beast · 1 234 games · tier A · final turn ≈ 11,5", detail.Meta);
        Assert.Equal("Derived from 5 top final boards (Firestone) and the comp's card lists; the source has no early-game guide.", detail.SourceNote);
    }

    [Fact]
    public void HandWrittenComp_WithoutFinalBoards_EnablersFromAddOnsOnly_NoTurn()
    {
        var handWritten = new Composition("hsr-beasts", "Beasts", new[] { "BEAST" }, new[] { "KEY_T3" }, new[] { "ADD_T4", "ADD_T1", "ADD_T2" });

        var detail = CompDetail.For(handWritten, TierOf);

        Assert.Equal(new[] { "ADD_T1", "ADD_T2" }, detail.EarlyEnablers.Select(c => c.CardId));
        Assert.All(detail.EarlyEnablers, c => Assert.Equal(0, c.FinalBoards));
        Assert.Null(detail.TypicalFinalTurn);
        Assert.Null(detail.TypicalFinalTurnText);
        Assert.Equal("Beasts · Beast", detail.Header);
        Assert.Equal("Beast", detail.Meta);
        Assert.Equal("Derived from the comp's card lists; the source has no final boards and no early-game guide.", detail.SourceNote);
    }

    [Fact]
    public void TierZero_CountsAsUnknown()
    {
        // HearthDb gives 0 for a card without TECH_LEVEL: it must not pass for an early card.
        var comp = new Composition("zero", "Zero", Array.Empty<string>(), new[] { "KEY_T5" }, new[] { "NO_TIER", "ADD_T1" });

        var detail = CompDetail.For(comp, id => id == "NO_TIER" ? 0 : TierOf(id));

        Assert.Equal(new[] { "ADD_T1" }, detail.EarlyEnablers.Select(c => c.CardId));
    }
}
