namespace BronzebeardHud.Stats.Tests;

public class CompAdvisorTests
{
    // Synthetic compositions with real-looking ids; the key pieces are distinct across comps,
    // except Leeroy (BG23_318), an add-on of two comps on purpose.
    private static readonly Composition Undead = new(
        "undead_butcher", "Undead Butcher", new[] { "UNDEAD" },
        coreCards: new[] { "BG32_324", "BG25_010", "BG28_309" }, addonCards: new[] { "BG32_880" }, averagePlacement: 3.81);

    private static readonly Composition Pirate = new(
        "pirate_discover", "Pirate Discover", new[] { "PIRATE" },
        coreCards: new[] { "BG26_817", "BG33_823" }, addonCards: new[] { "BG33_825", "BG23_318" }, averagePlacement: 3.93);

    private static readonly Composition Aberration = new(
        "abberation_deathrattle", "Aberration Deathrattle", new[] { "ABERRATION" },
        coreCards: new[] { "BG36_318", "BG25_354" }, addonCards: new[] { "BG23_318" }, averagePlacement: 3.23);

    private static readonly Composition[] All = { Undead, Pirate, Aberration };

    private static OwnedCard Card(string id, string? tribe = null) => new(id, tribe);

    [Fact]
    public void Rank_FollowsTheBoardOverThreeTurns_AndADroppedCompComesBack()
    {
        // Turn 3: one undead key piece, one pirate that is not a piece.
        var turn3 = new[] { Card("BG25_010", "UNDEAD"), Card("BG21_005", "PIRATE") };
        var ranked3 = CompAdvisor.Rank(turn3, All);
        Assert.Equal(new[] { "undead_butcher", "pirate_discover" }, ranked3.Select(p => p.Composition.Id));
        Assert.Equal(3.5, ranked3[0].Score); // 3 × 1 key piece + 0.5 × 1 undead
        Assert.Equal(0.5, ranked3[1].Score); // tribe only

        // Turn 4: the undead was sold; two pirate key pieces (one in hand) and a pirate filler.
        var turn4 = new[] { Card("BG26_817", "PIRATE"), Card("BG21_005", "PIRATE"), Card("BG33_823", "PIRATE") };
        var ranked4 = CompAdvisor.Rank(turn4, All);
        Assert.Equal(new[] { "pirate_discover" }, ranked4.Select(p => p.Composition.Id));
        Assert.Equal(new[] { "BG26_817", "BG33_823" }, ranked4[0].CoreOwned);
        Assert.Equal(7.5, ranked4[0].Score); // 3 × 2 + 0.5 × 3

        // Turn 5: two undead key pieces bought, one pirate piece kept: undead is back on top.
        var turn5 = new[] { Card("BG32_324", "UNDEAD"), Card("BG28_309", "UNDEAD"), Card("BG26_817", "PIRATE") };
        var ranked5 = CompAdvisor.Rank(turn5, All);
        Assert.Equal(new[] { "undead_butcher", "pirate_discover" }, ranked5.Select(p => p.Composition.Id));
        Assert.Equal(7.0, ranked5[0].Score); // 3 × 2 + 0.5 × 2
        Assert.Equal(3.5, ranked5[1].Score); // 3 × 1 + 0.5 × 1
    }

    [Fact]
    public void AdviseShop_ACardCanAdvanceTwoTargets()
    {
        var owned = new[] { Card("BG26_817", "PIRATE"), Card("BG36_318", "ABERRATION") };
        var targets = CompAdvisor.Rank(owned, All);
        var tavern = new[] { "BG23_318", "BG33_823_G", "BG20_101", "BG26_817" };

        var advice = CompAdvisor.AdviseShop(tavern, targets, owned);

        Assert.Equal(new[] { 0, 1, 2, 3 }, advice.Select(a => a.Position));
        var leeroy = advice[0].Advances;
        Assert.Equal(new[] { "abberation_deathrattle", "pirate_discover" }, leeroy.Select(a => a.Composition.Id).OrderBy(id => id));
        Assert.All(leeroy, a => Assert.False(a.IsKeyPiece));

        // A golden copy of a missing key piece counts as that piece.
        var rogers = Assert.Single(advice[1].Advances);
        Assert.Equal(("pirate_discover", true), (rogers.Composition.Id, rogers.IsKeyPiece));

        Assert.Empty(advice[2].Advances); // not in any target
        Assert.Empty(advice[3].Advances); // already owned
    }

    [Fact]
    public void Rank_EmptyBoardAndHand_NoTarget_AndNothingFlagged()
    {
        var targets = CompAdvisor.Rank(Array.Empty<OwnedCard>(), All);
        Assert.Empty(targets);
        Assert.All(CompAdvisor.AdviseShop(new[] { "BG32_324", "BG23_318" }, targets, Array.Empty<OwnedCard>()),
            a => Assert.Empty(a.Advances));
    }

    [Fact]
    public void Rank_CompWithNoPieceAndNoTribeHeld_IsNotATarget()
    {
        var owned = new[] { Card("BG32_324", "UNDEAD"), Card("BG32_880", "UNDEAD") };
        var ranked = CompAdvisor.Rank(owned, All);
        Assert.Equal(new[] { "undead_butcher" }, ranked.Select(p => p.Composition.Id));
        Assert.Equal(new[] { "BG32_880" }, ranked[0].AddonOwned);
    }

    [Fact]
    public void Rank_AmalgamCountsForEveryTribe_AndDuplicatesCountOnce()
    {
        var owned = new[] { Card("BG32_324", "UNDEAD"), Card("BG32_324_G", "UNDEAD"), Card("BG20_202", Tribes.Any) };
        var ranked = CompAdvisor.Rank(owned, All);
        var undead = ranked.Single(p => p.Composition.Id == "undead_butcher");
        Assert.Equal(new[] { "BG32_324" }, undead.CoreOwned);
        Assert.Equal(3, undead.TribeMatches);
        Assert.Equal(1, ranked.Single(p => p.Composition.Id == "pirate_discover").TribeMatches);
    }

    [Fact]
    public void Rank_AnAddonBreaksAnOtherwiseEqualScore()
    {
        // Pirate and aberration both get one key piece and one minion of their tribe (3.5 each);
        // Proud Privateer, a pirate add-on held in hand without tribe info, must put pirate first
        // even though aberration has the better average placement.
        var owned = new[] { Card("BG26_817", "PIRATE"), Card("BG36_318", "ABERRATION"), Card("BG33_825") };
        var ranked = CompAdvisor.Rank(owned, All);
        Assert.Equal(new[] { "pirate_discover", "abberation_deathrattle" }, ranked.Select(p => p.Composition.Id));
        Assert.Equal(4.5, ranked[0].Score);
        Assert.Equal(new[] { "BG33_825" }, ranked[0].AddonOwned);
    }

    [Fact]
    public void Rank_TiesGoToTheBetterAveragePlacement_AndAreCapped()
    {
        var owned = new[] { Card("BG20_202", Tribes.Any) };
        var ranked = CompAdvisor.Rank(owned, All, maxTargets: 2);
        Assert.Equal(new[] { "abberation_deathrattle", "undead_butcher" }, ranked.Select(p => p.Composition.Id));
    }
}
