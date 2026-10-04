using static BronzebeardHud.Stats.Tests.GuideTestData;

namespace BronzebeardHud.Stats.Tests;

/// <summary>Synthetic Firestone compositions for the bridge tests: no real Firestone data, every card id made up.</summary>
internal static class BridgeTestData
{
    public static Composition Comp(string id, string[] core, string[]? addons = null, int? games = null, FinalBoard[]? boards = null,
        string[]? reference = null, CompHeroStat[]? heroes = null, double? placement = null) =>
        new(id, id + " name", Array.Empty<string>(), core, addons ?? Array.Empty<string>(), averagePlacement: placement, dataPoints: games,
            finalBoards: boards, referenceBoard: reference, heroStats: heroes);

    public static FinalBoard Board(int? turn, params string[] cards) => new(9000, turn, cards);
}

/// <summary>
/// Two targets with a bridged Firestone comp each, a guide one of them can pivot to, and a guide that is neither.
/// Boards holding each card, counted by hand:
/// undead_fs (5 boards): U2 4 (one golden, positions 2 1 1 1), TOP 3 (positions 3 2 2), PA 3, TWO 2, RTOP 2, P1 2, UA 1, ONCE 1, R1 0.
/// pirate_fs (5 boards): P1 4, PA 2, TOP 2 (positions 4 3: a tie, the leftmost).
/// Bridges: Undead Butcher → undead_fs (3/3 keys), Pirate Discover → pirate_fs (2/2 keys; undead_fs also qualifies with
/// P1 and PA on its boards, 1/2 keys, and loses on keys), Undead Reborn → undead_fs, Mech Shield → none.
/// </summary>
internal static class BridgeFixture
{
    public static readonly CompGuide Undead = Guide("Undead Butcher", 2, 0, new[] { "U1", "U2", "U3" }, addons: new[] { "UA" }, enablers: new[] { "UE" }, tribe: 11);
    public static readonly CompGuide Pirates = Guide("Pirate Discover", 2, 1, new[] { "P1", "P2" }, addons: new[] { "PA" }, tribe: 23);

    /// <summary>Shares U1 and U2 with Undead Butcher: its pivot. R1 and RTOP are its own core cards.</summary>
    public static readonly CompGuide Reborn = Guide("Undead Reborn", 1, 0, new[] { "U1", "U2", "R1", "RTOP" }, tribe: 11);

    /// <summary>No target, no pivot: the fallback. R1 is a core card of it too.</summary>
    public static readonly CompGuide MechShield = Guide("Mech Shield", 1, 1, new[] { "M1", "R1" }, tribe: 17);

    public static readonly CompGuideSet All = Set(Undead, Pirates, Reborn, MechShield);
    public static readonly string[] Lobby = { "UNDEAD", "PIRATE", "MECHANICAL", "DEMON", "DRAGON" };

    public static readonly Composition UndeadFs = BridgeTestData.Comp("undead_fs", new[] { "U1", "U2" }, addons: new[] { "UA" }, games: 2000, boards: new[]
    {
        BridgeTestData.Board(12, "U1", "U2", "TOP", "TWO", "PA", "RTOP"),
        BridgeTestData.Board(14, "U2", "TOP", "U1", "PA", "RTOP"),
        BridgeTestData.Board(13, "U2_G", "TOP", "TWO", "ONCE", "PA"),
        BridgeTestData.Board(15, "U2", "U1", "UA", "P1"),
        BridgeTestData.Board(11, "U1", "U3", "P1"),
    });

    public static readonly Composition PirateFs = BridgeTestData.Comp("pirate_fs", new[] { "P1", "P2" }, addons: new[] { "PA" }, games: 900, boards: new[]
    {
        BridgeTestData.Board(10, "P1", "P2", "PA", "TOP"),
        BridgeTestData.Board(12, "P1", "PA", "TOP"),
        BridgeTestData.Board(11, "P1", "P2"),
        BridgeTestData.Board(13, "P2", "X"),
        BridgeTestData.Board(12, "P1"),
    });

    public static readonly Composition[] Comps = { UndeadFs, PirateFs };

    /// <summary>Undead Butcher (two core cards held) and a Pirate add-on held.</summary>
    public static readonly OwnedCard[] Held = Owned("U1", "U3", "PA");

    /// <summary>Undead Butcher then Pirate Discover, ticked in that order: the only targets.</summary>
    public static IReadOnlyList<CompTarget> Targets() => GuideTestData.Targets(All, new PlayerCards(Held, Array.Empty<OwnedCard>()), 2, Undead, Pirates);

    public static IReadOnlyDictionary<string, GuideEvidence> Bridge() => GuideBridge.For(All, Comps);
}
