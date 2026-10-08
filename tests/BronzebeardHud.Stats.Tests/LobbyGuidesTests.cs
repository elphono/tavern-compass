using static BronzebeardHud.Stats.Tests.GuideTestData;

namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// Which of HDT's guides the lobby can play (LobbyGuides). Synthetic guides and cards only; tribes are HearthDb Race values
/// for the guides and Battlegrounds names for the cards. The neutral key card TITUS stands for what was measured in Ali's
/// games on 2026-10-06: one neutral card that is a key card of several guides made guides of absent tribes look probable.
/// </summary>
public class LobbyGuidesTests
{
    private static readonly IReadOnlyDictionary<string, string[]> CardTribes = new Dictionary<string, string[]>
    {
        ["TITUS"] = Array.Empty<string>(),            // neutral
        ["AMALGAM"] = new[] { "ALL" },               // every tribe at once
        ["LOBSTER"] = new[] { "BEAST" },
        ["HYENA"] = new[] { "BEAST" },
        ["BUTCHER"] = new[] { "UNDEAD" },
        ["RISEN"] = new[] { "UNDEAD" },
        ["FELBOAR"] = new[] { "QUILBOAR" },
        ["IMP"] = new[] { "DEMON" },
        ["DRAKE"] = new[] { "DRAGON" },
        ["ROTTING"] = new[] { "BEAST", "UNDEAD" },   // dual type
        ["GOLDEN_ONLY"] = new[] { "PIRATE" },
    };

    private static IReadOnlyCollection<string>? TribesOf(string cardId) => CardTribes.TryGetValue(cardId, out var tribes) ? tribes : null;

    private static readonly CompGuide Lobster = Guide("Beasts - Lobster", 1, 0, new[] { "LOBSTER", "TITUS", "HYENA" }, tribe: 20);
    private static readonly CompGuide BeastsNeutralCore = Guide("Beasts - Neutral core", 1, 1, new[] { "TITUS", "AMALGAM" }, tribe: 20);
    private static readonly CompGuide Butcher = Guide("Undead - Butcher", 2, 0, new[] { "BUTCHER", "TITUS", "RISEN" }, tribe: 11);
    private static readonly CompGuide DemonsWithABoar = Guide("Demons - Shop Buff", 2, 1, new[] { "IMP", "FELBOAR", "TITUS", "AMALGAM" }, tribe: 15);
    private static readonly CompGuide Menagerie = Guide("Menagerie", 2, 2, new[] { "AMALGAM", "TITUS", "FELBOAR", "DRAKE" }, tribe: 0);
    private static readonly CompGuide Rotting = Guide("Menagerie - Rotting", 3, 0, new[] { "ROTTING", "LOBSTER" }, tribe: 0);
    private static readonly CompGuide Unknown = Guide("Something new", 3, 1, new[] { "NOT_IN_THE_DATABASE", "TITUS" }, tribe: 999);
    private static readonly CompGuideSet All = Set(Lobster, BeastsNeutralCore, Butcher, DemonsWithABoar, Menagerie, Rotting, Unknown);

    /// <summary>A lobby of five tribes without beasts, as in Ali's game of 2026-10-06 17:04 (lobby read from its Power.log).</summary>
    private static readonly string[] NoBeasts = { "DEMON", "MECHANICAL", "PIRATE", "QUILBOAR", "UNDEAD" };

    private static LobbyGuides Of(params string[] lobby) => LobbyGuides.Of(All, lobby, TribesOf);

    [Fact]
    public void ATribeAbsentFromTheLobby_LeavesItsGuidesOut_EvenWhenTheirKeyCardsAreNeutral()
    {
        var lobby = Of(NoBeasts);

        Assert.DoesNotContain(lobby.Playable.All, g => g.Id == Lobster.Id);
        Assert.DoesNotContain(lobby.Playable.All, g => g.Id == BeastsNeutralCore.Id); // TITUS and AMALGAM can be found, the beasts cannot
        Assert.Equal("no BEAST", lobby.Reason(Lobster.Id));
        Assert.Equal("no BEAST", lobby.Reason(BeastsNeutralCore.Id));
        Assert.Contains(lobby.Playable.All, g => g.Id == Butcher.Id);
        Assert.Null(lobby.Reason(Butcher.Id));
    }

    [Fact]
    public void AKeyCardThatCannotShowUp_CountsOnlyWhenAtLeastHalfOfTheKeyCardsCannot()
    {
        // Demons - Shop Buff: FELBOAR (quilboar) is one of four key cards. Menagerie (no tribe): FELBOAR and DRAKE, two of four.
        var noBoars = Of("DEMON", "MECHANICAL", "PIRATE", "UNDEAD", "BEAST");

        Assert.Null(noBoars.Reason(DemonsWithABoar.Id));                       // 1 of 4: still played
        Assert.Equal("key cards FELBOAR, DRAKE: no QUILBOAR, DRAGON", noBoars.Reason(Menagerie.Id)); // 2 of 4: left out
        Assert.Null(Of("DEMON", "MECHANICAL", "PIRATE", "UNDEAD", "DRAGON").Reason(Menagerie.Id)); // only FELBOAR missing
    }

    /// <summary>
    /// Ali's game of 2026-10-08: Menagerie, playable without quilboars, showed a quilboar among its key cards. The guide stays
    /// (one key card of four); the panel asks, card by card, which cannot show up, and leaves those out of the line.
    /// </summary>
    [Fact]
    public void CannotShowUp_TheKeyCardsOfAnAbsentTribe_NeverANeutralAnAmalgamADualTypeOrAnUnknownCard()
    {
        var noBoars = Of("DEMON", "MECHANICAL", "PIRATE", "UNDEAD", "DRAGON");

        Assert.Contains(noBoars.Playable.All, g => g.Id == Menagerie.Id);
        Assert.Equal(new[] { "FELBOAR" }, Menagerie.CoreCards.Where(noBoars.CannotShowUp));
        Assert.False(noBoars.CannotShowUp("ROTTING")); // beast or undead: the undead are there
        Assert.False(noBoars.CannotShowUp("NOT_IN_THE_DATABASE"));
        Assert.True(Of("UNDEAD", "DEMON", "MECHANICAL", "PIRATE", "QUILBOAR").CannotShowUp("DRAKE"));

        Assert.False(LobbyGuides.Unknown(All).CannotShowUp("FELBOAR")); // lobby not known yet: nothing is ruled out
        Assert.False(Of().CannotShowUp("FELBOAR"));
    }

    [Fact]
    public void ADualTypeCard_ShowsUpWhenEitherTribeIsThere_AnAmalgamAlways_AnUnknownCardIsNeverHeldAgainstAGuide()
    {
        var undeadOnly = Of("UNDEAD", "DEMON", "MECHANICAL", "PIRATE", "QUILBOAR");

        // Rotting (no tribe): ROTTING is beast or undead, so it shows up; LOBSTER does not: 1 of 2, half: left out.
        Assert.Equal("key cards LOBSTER: no BEAST", undeadOnly.Reason(Rotting.Id));
        Assert.Null(Of("BEAST", "DEMON", "MECHANICAL", "PIRATE", "QUILBOAR").Reason(Rotting.Id)); // both show up through BEAST

        // A race value no table knows and a card the database does not know: never left out on a guess.
        Assert.Null(undeadOnly.Reason(Unknown.Id));
        Assert.Null(Of("PIRATE").Reason(Unknown.Id));
    }

    [Fact]
    public void ALobbyNotKnownYet_LeavesNothingOut_AndSaysSo()
    {
        var unknown = LobbyGuides.Of(All, Array.Empty<string>(), TribesOf);

        Assert.False(unknown.Known);
        Assert.Equal(All.All.Select(g => g.Id), unknown.Playable.All.Select(g => g.Id));
        Assert.Empty(unknown.LeftOut);
        Assert.Equal("Bronzebeard HUD: lobby tribes unknown (shop turn 2): 7 guides, none left out", unknown.Line("shop turn 2"));
        Assert.Same(All, LobbyGuides.Unknown(All).All);
        Assert.False(LobbyGuides.Unknown(All).Known);
    }

    [Fact]
    public void ThePlayableSet_KeepsHdtsTiersAndOrder_WithoutTheGuidesLeftOut_AndAnEmptyTierGoes()
    {
        var lobby = Of(NoBeasts);

        Assert.Equal(new[] { "Undead - Butcher", "Demons - Shop Buff", "Menagerie", "Something new" }, lobby.Playable.All.Select(g => g.Name));
        Assert.Equal(new[] { 2, 3 }, lobby.Playable.Tiers.Select(t => t.Tier)); // the S tier held only beasts
        Assert.Equal(All.Source, lobby.Playable.Source);
        Assert.Equal(new[] { "Beasts - Lobster", "Beasts - Neutral core", "Menagerie - Rotting" }, lobby.LeftOut.Select(l => l.Guide.Name));
        Assert.Equal(NoBeasts, lobby.Tribes);
        Assert.True(lobby.Known);
    }

    [Fact]
    public void TheLogLine_NamesTheLobby_HowManyArePlayable_AndWhyEachOtherIsLeftOut()
    {
        Assert.Equal(
            "Bronzebeard HUD: lobby tribes=[DEMON,MECHANICAL,PIRATE,QUILBOAR,UNDEAD] (shop turn 1): 4/7 guides playable; left out: " +
            "Beasts - Lobster (no BEAST), Beasts - Neutral core (no BEAST), Menagerie - Rotting (key cards LOBSTER: no BEAST)",
            Of(NoBeasts).Line("shop turn 1"));
        Assert.Equal("Bronzebeard HUD: lobby tribes=[BEAST,DEMON,DRAGON,MECHANICAL,PIRATE,QUILBOAR,UNDEAD] (hero selection): 7/7 guides playable; left out: none",
            Of("UNDEAD", "BEAST", "DEMON", "DRAGON", "MECHANICAL", "PIRATE", "QUILBOAR").Line("hero selection"));
    }

    [Fact]
    public void GoldenKeyCards_AreLookedUpAsTheirBaseCard()
    {
        var golden = Guide("Pirates - Golden", 1, 0, new[] { "GOLDEN_ONLY_G", "TITUS" }, tribe: 0);
        var lobby = LobbyGuides.Of(Set(golden), new[] { "UNDEAD", "BEAST" }, TribesOf);

        Assert.Equal("key cards GOLDEN_ONLY_G: no PIRATE", lobby.Reason(golden.Id));
    }
}
