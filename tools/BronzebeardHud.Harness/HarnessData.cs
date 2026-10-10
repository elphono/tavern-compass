using System.Globalization;
using BronzebeardHud.Stats;
using Hearthstone_Deck_Tracker.Hearthstone;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Harness;

/// <summary>
/// Synthetic data for the panels: invented comp guides built on real card ids (so that pictures can be fetched), a few
/// boards the player may hold, Bob's row, the options of a discover, a Dark Gift and a trinket choice with invented
/// trinket stats, and the card names and tiers of HearthstoneJSON when they have been fetched.
/// Nothing here comes from Firestone or HSReplay: the guides' names, card lists and texts, and the trinket stats, are made up.
/// </summary>
internal static class HarnessData
{
    /// <summary>
    /// Real Battlegrounds card ids; any id works, one that does not exist simply has no picture. The first 31 are those the
    /// repository's tests already used; BG36_352 and BGS_004 (indices 10 and 17) are the portraits with the widest white
    /// side margins measured (36 px of 256): they show whether the oval cut leaves a white crescent (TavernLayout.PortraitCut).
    /// The last 30 are pool minions taken from HearthstoneJSON's card list, so that most guides own their cards and a
    /// scenario can name cards that only one guide lists.
    /// </summary>
    public static readonly string[] Pool =
    {
        "BG21_005", "BG23_008", "BG23_318", "BG24_022", "BG25_009", "BG25_010", "BG25_016", "BG25_040", "BG25_354", "BG26_174", "BG36_352", "BG26_817",
        "BG28_300", "BG28_309", "BG28_504", "BG28_573", "BG29_300", "BGS_004", "BG31_808", "BG31_815", "BG31_835", "BG32_324", "BG32_880", "BG33_823",
        "BG33_825", "BG34_170", "BG36_110", "BG36_114", "BG36_116", "BG36_318", "BG36_515",
        "BG22_202", "BG22_403", "BG24_500", "BG25_001", "BG26_135", "BG26_147", "BG26_149", "BG26_963", "BG27_017", "BG28_741", "BG29_813", "BG30_123",
        "BG31_320", "BG31_323", "BG31_327", "BG31_330", "BG31_810", "BG31_818", "BG32_821", "BG33_821", "BG33_822", "BG34_500", "BG34_950", "BG35_341",
        "BG35_604", "BG36_201", "BG36_331", "BG36_360", "BG36_367", "BG36_506",
    };

    private sealed class GuideSpec
    {
        public GuideSpec(string name, int tier, int tribe, int[] core, int[] addons, int[] enablers)
        {
            Name = name;
            Tier = tier;
            Tribe = tribe;
            Core = core;
            Addons = addons;
            Enablers = enablers;
        }

        public string Name { get; }
        public int Tier { get; }
        public int Tribe { get; }
        public int[] Core { get; }
        public int[] Addons { get; }
        public int[] Enablers { get; }
    }

    /// <summary>
    /// Fifteen guides in four tiers (S, A, B, C), indexes into <see cref="Pool"/>. Most own their cards; a few share some on
    /// purpose, so that the detail has pivots (Undead Butcher ↔ Undead Attack, Mech Magnet ↔ Mech Divine Shield, …).
    /// Mech Divine Shield has seven core cards: its line shows five and "+2". Tribes are HearthDb Race values.
    /// </summary>
    private static readonly GuideSpec[] Specs =
    {
        new("Undead Butcher", 1, 11, new[] { 0, 1, 2 }, new[] { 3, 4 }, new[] { 5 }),
        new("Pirate Discover", 1, 23, new[] { 6, 7, 8, 9 }, new[] { 10, 11 }, new[] { 12 }),
        new("Mech Magnet", 1, 17, new[] { 13, 14 }, new[] { 15, 16, 17 }, new[] { 18 }),
        new("Beast Pack", 2, 20, new[] { 19, 20, 21, 22, 23 }, new[] { 24 }, new[] { 25 }),
        new("Murloc Handbuff", 2, 14, new[] { 26, 27, 28 }, new[] { 29, 30 }, new[] { 31 }),
        new("Demon Gold", 2, 15, new[] { 32, 33, 34, 35, 36, 37 }, new[] { 38 }, new[] { 39 }),
        new("Elemental Cycle", 2, 18, new[] { 40, 41, 42, 43 }, new[] { 44, 45 }, new[] { 46 }),
        new("Quilboar Choose One", 2, 43, new[] { 47, 48, 49 }, new[] { 50, 51 }, new[] { 52 }),
        new("Naga Spells", 3, 92, new[] { 53, 54 }, new[] { 55 }, new[] { 56 }),
        new("Dragon Shields", 3, 24, new[] { 57, 58, 59, 60, 0 }, new[] { 6 }, new[] { 9 }),
        new("Undead Attack", 3, 11, new[] { 0, 1, 4, 10 }, new[] { 2 }, new[] { 13 }),
        new("Aberration Swarm", 3, 126, new[] { 28, 30, 2 }, new[] { 5, 8 }, new[] { 11 }),
        new("Mech Divine Shield", 4, 17, new[] { 13, 14, 15, 16, 17, 18, 19 }, new[] { 20 }, new[] { 21 }),
        new("Murloc Tidecaller", 4, 14, new[] { 26, 29 }, new[] { 27 }, new[] { 30 }),
        new("Beast Deathrattle", 4, 20, new[] { 19, 24, 25 }, new[] { 23 }, new[] { 22 }),
    };

    /// <summary>
    /// The lobby of the first scenarios: five tribes, as a game has, so that seven of the fifteen guides (murlocs, demons,
    /// quilboar, naga, dragons, aberrations) are left out of the list.
    /// </summary>
    public static readonly string[] Lobby = { "BEAST", "ELEMENTAL", "MECHANICAL", "PIRATE", "UNDEAD" };

    /// <summary>A lobby without undead nor dragons: the three guides that list the neutral card <c>Pool[0]</c> as a key card are left out.</summary>
    public static readonly string[] LobbyWithoutUndead = { "BEAST", "ELEMENTAL", "MECHANICAL", "MURLOC", "PIRATE" };

    /// <summary>
    /// What the player holds, as the plugin sees it (board then hand), and the lobby's tribes (empty: not known yet).
    /// 0, 1 and 3 targets with three targets wanted; then the two bugs of 2026-10-06:
    /// - "A neutral key card of absent tribes": <c>Pool[0]</c>, made neutral here (<see cref="CardTribes"/>), is a key card of
    ///   Undead Butcher, Undead Attack and Dragon Shields, none of which the lobby can play: with the filter, only Mech Magnet
    ///   and Mech Divine Shield (13 and 14) are targets; without it, Undead Attack (key card 0 and enabler 13) was the third;
    /// - "The lobby not known yet": the neutral card and a mech key card, the tribes unknown: nothing is left out (Undead
    ///   Attack, Mech Magnet and Undead Butcher are the targets, in two tiers), and the panel says so — unless its note would
    ///   push a target out of the list at the default size, which it does with three targets and not with one;
    /// - "Ticks: in progress and guesses": Elemental Cycle has two key cards held (40, 41: in progress); 19 and 6 are one key
    ///   card each of Beast Deathrattle, Beast Pack, Mech Divine Shield and Pirate Discover (guesses). Ticking Pirate Discover
    ///   keeps Elemental Cycle, silences Beast Deathrattle.
    /// </summary>
    public static IReadOnlyList<(string Name, PlayerCards Cards, IReadOnlyList<string> Lobby)> Scenarios { get; } = new (string, PlayerCards, IReadOnlyList<string>)[]
    {
        ("Nothing yet", PlayerCards.None, Lobby),
        ("Two cards of one composition", Hold(board: new[] { 40, 41 }, hand: Array.Empty<int>()), Lobby),         // Elemental Cycle only
        ("A strong board, one in hand", Hold(board: new[] { 6, 7, 8, 13, 14 }, hand: new[] { 9 }), Lobby),       // Pirate Discover, Mech Magnet, Mech Divine Shield
        ("A neutral key card of absent tribes", Hold(board: new[] { 0, 13, 14 }, hand: Array.Empty<int>()), LobbyWithoutUndead),
        ("The lobby not known yet", Hold(board: new[] { 0, 13 }, hand: Array.Empty<int>()), Array.Empty<string>()),
        ("Ticks: in progress and guesses", Hold(board: new[] { 40, 41, 19 }, hand: new[] { 6 }), Lobby),
    };

    /// <summary>Targets each scenario must give with three targets wanted (the self-test checks it).</summary>
    public static readonly int[] ExpectedTargets = { 0, 1, 3, 2, 3, 3 };

    /// <summary>The scenario of the scene by default (<c>--scenario</c>): three targets.</summary>
    public const int DefaultScenario = 2;

    /// <summary>
    /// The synthetic tribes of a card of <see cref="Pool"/>, as LobbyGuides asks them (HearthDb's in the plugin): the tribe of
    /// the first guide listing it as a key card, else of the first guide listing it at all; <c>Pool[0]</c> is neutral (a key
    /// card of guides of two tribes, as Titus Rivendare is in HSReplay's guides); null for a card no guide lists.
    /// </summary>
    public static IReadOnlyCollection<string>? CardTribes(string cardId)
    {
        var index = Array.IndexOf(Pool, cardId);
        if (index < 0)
        {
            return null;
        }

        if (index == 0)
        {
            return Array.Empty<string>();
        }

        var spec = Specs.FirstOrDefault(s => s.Core.Contains(index)) ?? Specs.FirstOrDefault(s => s.Addons.Contains(index) || s.Enablers.Contains(index));
        return spec == null ? null : GuideTribes.NameOf(spec.Tribe) is { } name ? new[] { name } : Array.Empty<string>();
    }

    /// <summary>The board's power scenes (<c>--power</c>, the self-test): a level, or none for lack of data.</summary>
    public static IReadOnlyList<string> PowerScenes { get; } = new[] { "behind", "even", "ahead", "shiny", "none", "early" };

    /// <summary>
    /// The board's power for a scene, as Plugin.UpdateWarband computes it (WarbandCurve.Compare) on an invented curve for
    /// <see cref="Hero"/> (×1.8 a turn, 120 at turn 8; nothing from Firestone): boards of 80, 142, 190 and 260 at turn 8 give
    /// the four levels; "none" is a hero without curve, "early" turn 2 (an average of 6, under the minimum).
    /// </summary>
    public static WarbandComparison Power(string scene)
    {
        var sources = new[] { HeroStats };
        return scene switch
        {
            "behind" => WarbandCurve.Compare(8, 80, Hero, sources),
            "ahead" => WarbandCurve.Compare(8, 190, Hero, sources),
            "shiny" => WarbandCurve.Compare(8, 260, Hero, sources),
            "none" => WarbandCurve.Compare(8, 142, "TB_BaconShop_HERO_28", sources),
            "early" => WarbandCurve.Compare(2, 3, Hero, sources),
            "even" => WarbandCurve.Compare(8, 142, Hero, sources),
            _ => throw new ArgumentException($"--power {scene}: expected {string.Join(", ", PowerScenes)}"),
        };
    }

    private static readonly HeroStatsFile HeroStats = new(StatsSources.Firestone, new[]
    {
        new HeroStat(Hero, 4.1, 1000, warbandCurve: new[] { (1, 4.0), (2, 6.0), (3, 11.0), (4, 19.0), (5, 35.0), (6, 37.0), (7, 67.0), (8, 120.0), (9, 216.0), (10, 389.0) }
            .Select(p => new WarbandPoint(p.Item1, p.Item2)).ToList()),
        new HeroStat(OpponentHero, 4.3, 800, warbandCurve: new[] { (1, 4.0), (2, 7.0), (3, 12.0), (4, 22.0), (5, 50.0), (6, 60.0), (7, 90.0), (8, 143.0), (9, 250.0), (10, 420.0) }
            .Select(p => new WarbandPoint(p.Item1, p.Item2)).ToList()),
    });

    /// <summary>The opponent's hero in the simulation: a Battlegrounds hero id, its curve invented (143 at turn 8, 50 at turn 5).</summary>
    public const string OpponentHero = "TB_BaconShop_HERO_17";

    /// <summary>A hero no curve covers (the "none" scenes): a Battlegrounds hero id.</summary>
    public const string UnchartedHero = "TB_BaconShop_HERO_28";

    /// <summary>The opponent's gauge scenes (<c>--opp-power</c>, the self-test): a level, or none for lack of data.</summary>
    public static IReadOnlyList<string> OpponentPowerScenes { get; } = new[] { "behind", "even", "ahead", "shiny", "none", "next", "unseen" };

    /// <summary>
    /// What the opponent's gauge reads for a scene, as Plugin.UpdateOpponentPower reads HDT (HdtEntityAdapter.OpponentFacts): in
    /// combat at turn 8 the opponent (player 3) on boards of 90, 160, 210 and 300 against THEIR hero's invented curve (143 at
    /// turn 8: the four levels; against the player's own curve, 120, 160 would read +33 %); "none" a hero no curve covers;
    /// in the shop at turn 9, "next" the next opponent's board last seen at turn 5 (70 against their 50 then: ahead, where
    /// their 250 of turn 9 would read behind), "unseen" a next opponent never fought.
    /// </summary>
    public static OpponentFacts OpponentFacts(string scene)
    {
        IReadOnlyList<(int, int)> Minions(int stats) => new[] { (3, 4), (5, 3), (stats - 15, 0) };
        OpponentFacts Fight(string hero, int stats) => new(OverlayPhase.Combat, 8, Hero, 3, 0,
            new Dictionary<int, BoardSeen> { [3] = new(hero, 8, Minions(stats)) }, new Dictionary<int, string> { [3] = hero });
        return scene switch
        {
            "behind" => Fight(OpponentHero, 90),
            "even" => Fight(OpponentHero, 160),
            "ahead" => Fight(OpponentHero, 210),
            "shiny" => Fight(OpponentHero, 300),
            "none" => Fight(UnchartedHero, 160),
            "next" => new(OverlayPhase.Shop, 9, Hero, 0, 3, new Dictionary<int, BoardSeen> { [3] = new(OpponentHero, 5, Minions(70)) },
                new Dictionary<int, string> { [3] = OpponentHero }),
            "unseen" => new(OverlayPhase.Shop, 4, Hero, 0, 6, new Dictionary<int, BoardSeen>(), new Dictionary<int, string> { [6] = OpponentHero }),
            _ => throw new ArgumentException($"--opp-power {scene}: expected {string.Join(", ", OpponentPowerScenes)}"),
        };
    }

    /// <summary>The opponent's gauge for a scene (OpponentPower.Compare on the harness's curves), and its log line.</summary>
    public static (WarbandComparison Power, string Line) OpponentPower(string scene, Func<string, string> heroName)
    {
        var facts = OpponentFacts(scene);
        var power = BronzebeardHud.Stats.OpponentPower.Compare(facts, new[] { HeroStats }, heroName);
        return (power, BronzebeardHud.Stats.OpponentPower.LogLine(facts, power));
    }

    /// <summary>
    /// Bob's row for the scene: with the third scenario's targets, a core card of a target not held yet (16, Mech Divine
    /// Shield, also an add-on of Mech Magnet: core wins), a core card held again (7, Pirate Discover), an add-on (11, Pirate
    /// Discover), an enabler (21, Mech Divine Shield), a pinned card that serves no target (42), one that serves nothing
    /// (50), and one no target's guide lists but that stands on 3 of the 5 boards of Mech Magnet's Firestone comp (57: a
    /// dotted "+ Mech Magnet 3/5" frame, through the bridge).
    /// </summary>
    public static IReadOnlyList<string> Shop { get; } = new[] { 16, 11, 7, 42, 50, 21, 57 }.Select(i => Pool[i]).ToList();

    public static TavernPins Pins { get; } = TavernPins.Of(new[] { Pool[42] });

    private static PlayerCards Hold(int[] board, int[] hand) =>
        new(board.Select(i => new OwnedCard(Pool[i])).ToList(), hand.Select(i => new OwnedCard(Pool[i])).ToList());

    /// <summary>The choices the harness can open above the scene (the bar's list, <c>--choice</c>), "none" first.</summary>
    public static IReadOnlyList<(string Label, ChoiceKind Kind)> Choices { get; } = new[]
    {
        ("No choice", ChoiceKind.None),
        ("Discover", ChoiceKind.Discover),
        ("Dark Gift", ChoiceKind.DarkGift),
        ("Trinket", ChoiceKind.Trinket),
    };

    /// <summary>A choice named as <c>--choice</c> names it (<see cref="ChoiceClassifier.Name"/>: discover, dark-gift, trinket, none).</summary>
    public static ChoiceKind ChoiceOf(string name)
    {
        foreach (var (_, kind) in Choices)
        {
            if (string.Equals(ChoiceClassifier.Name(kind), name, StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        throw new ArgumentException($"--choice {name}: expected {string.Join(", ", Choices.Select(c => ChoiceClassifier.Name(c.Kind)))}");
    }

    /// <summary>
    /// The options of each choice, as HDT hands them to the plugin, with the counts of Ali's games (his HDT logs: discover
    /// and Dark Gift 3 options, trinkets 4 in all 48 trinket choices). Picked so that, with the third scenario's targets
    /// (Pirate Discover, Mech Magnet, Mech Divine Shield) and the bridge to <see cref="FirestoneComps"/>, every kind of
    /// label shows:
    /// - discover, the three labels the bridge opens: 11, an add-on of the target Pirate Discover, on 3 of the 5 boards of
    ///   its comp ("+ Pirate Discover · 3/5 boards"); 46, in no list of a target but on 3 of the 5 boards of Mech Magnet's
    ///   comp ("+ Mech Magnet 3/5 boards", in its colour; "+ Elemental Cycle" in the second scenario, where Elemental Cycle
    ///   is the only target); 22, a core card of Beast Pack, a guide the target Mech Divine Shield can pivot to ("pivot →
    ///   Beast Pack (A)", neutral; "core Beast Pack (A)" without the bridge or that target);
    /// - Dark Gift: 21, an enabler of the target Mech Divine Shield, bridged to nothing (no "boards"); 0, a core card of
    ///   Undead Butcher, an S guide that is no target, and of two other guides ("+2 more"); 50, nothing (an add-on of a
    ///   guide that is no target, on no board);
    /// - trinkets (real ones, their English text as HDT reads it): one names Pirates (Pirate Discover's tribe), one Mechs
    ///   (Mech Magnet and Mech Divine Shield), one Elementals (no target's), one no tribe.
    /// </summary>
    public static IReadOnlyList<OfferedOption> Choice(ChoiceKind kind) => kind switch
    {
        ChoiceKind.Discover => Minions(false, 11, 46, 22),
        ChoiceKind.DarkGift => Minions(true, 21, 0, 50),
        ChoiceKind.Trinket => Trinkets.Select((t, i) => new OfferedOption(9101 + i, t.Id, "BATTLEGROUND_TRINKET", text: t.Text)).ToList(),
        _ => Array.Empty<OfferedOption>(),
    };

    private static IReadOnlyList<OfferedOption> Minions(bool darkGift, params int[] indices) =>
        indices.Select((index, i) => new OfferedOption((darkGift ? 9201 : 9001) + i, Pool[index], "MINION", hasDarkGift: darkGift)).ToList();

    /// <summary>Real trinkets and their English text (HearthstoneJSON), which TrinketAffinity reads for a tribe.</summary>
    private static readonly (string Id, string Text)[] Trinkets =
    {
        ("BG30_MagicItem_439", "You only need 2 copies of a Pirate to make it Golden."),
        ("BG30_MagicItem_910", "[x]After a friendly Mech loses <b>Divine Shield</b>, give it <b>Divine Shield</b>. <i>(3 times per combat.)</i>"),
        ("BG30_MagicItem_544", "[x]After you play an Elemental, give Elementals in the Tavern +3/+2 this game."),
        ("BG30_MagicItem_303", "<b>Start of Combat:</b> When you have space, summon an Ancestral Automaton."),
    };

    /// <summary>The English text of a trinket of <see cref="Choice"/>; null for anything else.</summary>
    public static string? TrinketText(string cardId) => Trinkets.FirstOrDefault(t => t.Id == cardId).Text;

    /// <summary>The MMR bracket the harness plays in (MmrBracket's percentile): the trinkets' placement for it is shown.</summary>
    public const int Bracket = 25;

    /// <summary>The shop turn of the scene, for the card values (<c>--card-values</c>).</summary>
    public const int Turn = 6;

    /// <summary>
    /// Invented card stats at <see cref="Turn"/> (never Firestone's), against a filler card that sets the turn's average
    /// near 4.0: 50 (nothing for any guide) and 42 (pinned) clearly better, "▲"; 16 (a target's core card) clearly worse,
    /// "▼", in its second line; every other card unknown, so nothing is said of it.
    /// </summary>
    public static CardStatsFile CardStats { get; } = new(
        new StatProvenance("harness", null, null, null, "last-patch", Bracket, null),
        new[] { (50, 400, 3.5), (42, 400, 3.55), (16, 600, 4.4) }
            .Select(c => new CardStat(Pool[c.Item1], new[] { new CardTurnStat(Turn, c.Item2, c.Item3) }))
            .Append(new CardStat("HARNESS_FILLER", new[] { new CardTurnStat(Turn, 5000, 4.0) }))
            .ToList());

    /// <summary>
    /// The early cards' pool and tier, invented and fixed (the tiers HearthstoneJSON gives arrive after the first drawing):
    /// the two good cards of <see cref="CardStats"/> at tier 3 and 4, the bad one at tier 3, so that both rows show.
    /// </summary>
    /// <summary>
    /// Four offered heroes, invented, for the hero badges (component 3): a consensus of two sources (one recentred), a
    /// contest, one source alone, and a hero no source knows.
    /// </summary>
    public static IReadOnlyList<HeroPickRow> HeroRows()
    {
        var odds = new[] { 14.0, 13.0, 13.0, 12.0, 12.0, 12.0, 12.0, 12.0 };
        var firestone = new HeroStatsFile(StatsSources.Firestone, new[]
        {
            new HeroStat("HARNESS_HERO_A", 3.42, 4051, pickRate: 0.18, placementDistribution: odds),
            new HeroStat("HARNESS_HERO_B", 3.80, 12480, pickRate: 0.09, placementDistribution: odds),
            new HeroStat("HARNESS_HERO_C", 4.61, 880, pickRate: 0.04, placementDistribution: odds),
            new HeroStat("HARNESS_HERO_E", 4.10, 3000),
        }, mmrPercentile: 25, timePeriod: "last-patch");
        var nomi = new SourceSnapshot(new StatProvenance(StatsSources.NomiGg, null, null, null, "since 2026-10-02", null, null), new[]
        {
            new StatRecord("hero", "HARNESS_HERO_A", "placement", 3.10, 51, "games"),
            new StatRecord("hero", "HARNESS_HERO_B", "placement", 2.60, 90, "games"),
            new StatRecord("hero", "HARNESS_HERO_E", "placement", 3.80, 800, "games"),
        }, new Dictionary<string, double> { ["hero"] = 3.75 });
        var view = StatsConsolidation.Consolidate(new[] { SourceSnapshot.Of(firestone), nomi }, 25);
        var offered = new[] { "HARNESS_HERO_A", "HARNESS_HERO_B", "HARNESS_HERO_C", "HARNESS_HERO_D" }
            .Select((id, i) => new OfferedHero(entityId: 90 + i, cardId: id, baseCardId: id, position: i + 1)).ToList();
        return HeroPickAdvisor.BuildRows(offered, new[] { firestone }, view);
    }

    /// <summary>An invented nomi.gg analysis (components 4 and 7): buffs and nerfs, and every tribe before and after the patch.</summary>
    public static NomiAnalysisFile Nomi { get; } = new(
        new StatProvenance(StatsSources.NomiGg, null, null, null, "since 2026-10-02", null, "36.6.3"), 253216,
        new[] { "quilboar", "pirate", "undead" }, new[] { "aberration" }, Array.Empty<NomiHero>(),
        new[]
        {
            new NomiTribe("beast", 400, 3.80, 900, 3.55), new NomiTribe("demon", 300, 3.70, 700, 3.75),
            new NomiTribe("mech", 350, 3.96, 800, 4.30), new NomiTribe("murloc", 200, 3.40, 500, 3.52),
            new NomiTribe("pirate", 300, 4.07, 900, 3.70), new NomiTribe("undead", 300, 4.48, 900, 3.93),
            new NomiTribe("naga", 250, 3.60, 600, 3.66), new NomiTribe("elemental", 200, 4.00, 500, 3.90),
            new NomiTribe("dragon", 220, 3.72, 600, 3.95), new NomiTribe("quilboar", 260, 3.40, 800, 3.30),
        },
        Array.Empty<NomiTrinket>(), Array.Empty<NomiTierMedian>());

    public static IReadOnlyDictionary<string, int> EarlyPool { get; } = new Dictionary<string, int>
    {
        [Pool[50]] = 3,
        [Pool[42]] = 4,
        [Pool[16]] = 3,
        ["HARNESS_FILLER"] = 1,
    };

    public const int EarlyTier = 3;

    /// <summary>
    /// Invented trinket stats (never Firestone's), one per trinket: a placement for every player and per bracket, the
    /// last one without the harness's bracket (its placement for every player is shown) and the third without a pick rate.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, TrinketStat> TrinketStats = new[]
    {
        new TrinketStat(Trinkets[0].Id, 3.92, 5400, 0.31, new Dictionary<int, double> { [100] = 3.92, [50] = 3.85, [25] = 3.71 }),
        new TrinketStat(Trinkets[1].Id, 4.10, 4100, 0.18, new Dictionary<int, double> { [100] = 4.10, [50] = 4.02, [25] = 3.95 }),
        new TrinketStat(Trinkets[2].Id, 4.35, 2600, null, new Dictionary<int, double> { [100] = 4.35, [25] = 4.28 }),
        new TrinketStat(Trinkets[3].Id, 3.66, 7300, 0.44, new Dictionary<int, double> { [100] = 3.66 }),
    }.ToDictionary(t => t.TrinketCardId, StringComparer.Ordinal);

    /// <summary>
    /// The trinkets in a consolidated view (component 9): Firestone's, with invented nomi.gg figures, the first agreeing
    /// (its figure named under the placement), the second contested (both figures).
    /// </summary>
    private static readonly ConsolidatedView TrinketsView = StatsConsolidation.Consolidate(new[]
    {
        new SourceSnapshot(new StatProvenance(StatsSources.Firestone, null, null, null, "last-patch", null, null),
            TrinketStats.Values.Select(t => new StatRecord("trinket", t.TrinketCardId, "placement", t.AveragePlacement, t.DataPoints, "games")).ToList(),
            new Dictionary<string, double> { ["trinket"] = 3.9 }),
        new SourceSnapshot(new StatProvenance(StatsSources.NomiGg, null, null, null, "since 2026-10-02", null, null), new[]
        {
            new StatRecord("trinket", Trinkets[0].Id, "placement", 3.70, 60, "games"),
            new StatRecord("trinket", Trinkets[1].Id, "placement", 3.10, 400, "games"),
        }, new Dictionary<string, double> { ["trinket"] = 3.7 }),
    }, Bracket);

    public static ConsolidatedStat? TrinketView(string cardId) => TrinketsView.Find("trinket", cardId, "placement", "games");

    /// <summary>The trinket stats ChoiceAdvisor asks for, as the plugin hands it the panel's cache (ChoiceAdvicePanel.TrinketStat).</summary>
    public static TrinketStat? TrinketStat(string cardId) => TrinketStats.TryGetValue(cardId, out var stat) ? stat : null;

    /// <summary>
    /// The hero the harness plays, for the hero piece of a guide's context line (HeroCompAffinity on <see cref="FirestoneComps"/>).
    /// A Battlegrounds hero id; nothing is fetched for it, and its figures below are invented.
    /// </summary>
    public const string Hero = "TB_BaconShop_HERO_16";

    /// <summary>
    /// Invented Firestone compositions (nothing comes from Firestone), on the guides' card ids, for the bridge (GuideBridge),
    /// with the third scenario's targets (Pirate Discover, Mech Magnet, Mech Divine Shield):
    /// - pirate_fs is Pirate Discover's (its core cards 6, 7, 8 and its add-on 11 in common, all four of its core cards on the
    ///   boards); 11 stands on 3 of its 5 final boards: "+ Pirate Discover · 3/5 boards" over a choice offering it;
    /// - mech_fs is Mech Magnet's (13, 14, 15: both its core cards and an add-on). It shares the same three with Mech Divine
    ///   Shield, but they are 3 of its 7 core cards, under half: Mech Divine Shield is bridged to nothing, the counter-example
    ///   the log line names "no match". No other card of Mech Divine Shield's lists is on its boards, or it would bridge.
    ///   46 (an enabler of Elemental Cycle, in no list of a target) stands on 3 of its 5 boards: "+ Mech Magnet 3/5 boards"
    ///   over a choice offering it; 57 (a core card of Dragon Shields, no target) too: a dotted "+ Mech Magnet 3/5" frame on
    ///   Bob's card.
    /// Each has a figure for <see cref="Hero"/>, so that the context line has its three pieces.
    /// </summary>
    public static IReadOnlyList<Composition> FirestoneComps { get; } = new[]
    {
        new Composition("pirate_fs", "Pirates (synthetic)", new[] { "PIRATE" }, Cards(6, 7, 8), Cards(11), averagePlacement: 3.9, dataPoints: 1800,
            finalBoards: new[]
            {
                Board(12, 6, 7, 8, 11, 9, 10),
                Board(14, 6, 7, 11, 9),
                Board(13, 7, 8, 9, 6),
                Board(13, 6, 8, 11, 10),
                Board(11, 7, 9, 10),
            },
            heroStats: new[] { new CompHeroStat(Hero, 23, 2.9) }),
        new Composition("mech_fs", "Mechs (synthetic)", new[] { "MECHANICAL" }, Cards(13, 14), Cards(15), averagePlacement: 4.1, dataPoints: 1200,
            finalBoards: new[]
            {
                Board(10, 13, 14, 15, 46, 57),
                Board(12, 13, 14, 57, 46),
                Board(11, 14, 15, 57, 3),
                Board(12, 13, 15, 46),
                Board(13, 13, 14, 15),
            },
            heroStats: new[] { new CompHeroStat(Hero, 14, 4.4) }),
    };

    private static string[] Cards(params int[] indices) => indices.Select(i => Pool[i]).ToArray();

    private static FinalBoard Board(int turn, params int[] indices) => new(8000, turn, Cards(indices));

    /// <summary>
    /// The comp guides HDT would show, with their texts written in HSReplay's markup (<c>[[Name||dbf]]</c>, the dbf id
    /// standing for an index into <see cref="Pool"/> here) so that they go through the same parsing as HDT's, card names
    /// in bold. <paramref name="nameOf"/> gives a card's name (HearthstoneJSON's once loaded, else its id).
    /// </summary>
    public static CompGuideSet Guides(Func<string, string> nameOf)
    {
        string? Resolve(int dbf) => dbf >= 0 && dbf < Pool.Length ? Pool[dbf] : null;
        string Ref(int index) => $"[[{nameOf(Pool[index])}||{index.ToString(CultureInfo.InvariantCulture)}]]";
        var tiers = new List<CompGuideTier>();
        foreach (var tier in Specs.GroupBy(s => s.Tier).OrderBy(g => g.Key))
        {
            var guides = tier.Select((s, rank) =>
            {
                var howToPlay = CompGuideText.Parse(
                    $"Stay on tier 3 until {Ref(s.Core[0])} shows up, then level and keep every {Ref(s.Core[s.Core.Length - 1])} you find.\n" +
                    "A second paragraph that the panel leaves to HDT.", Resolve);
                var commit = CompGuideText.Parse(
                    $"{Ref(s.Core[0])} + {Ref(s.Core[1])}\nAny two of {Ref(s.Addons[0])} or {Ref(s.Enablers[0])}", Resolve);
                var enablers = CompGuideText.Parse(string.Join("\n", s.Enablers.Select(Ref)), Resolve);
                return new CompGuide(s.Name, s.Tier, rank, s.Core.Select(i => Pool[i]).ToList(), s.Addons.Select(i => Pool[i]).ToList(),
                    enablers.CardIds, commit.CardIds, commit.PlainText, enablers.PlainText, howToPlay.PlainText,
                    difficulty: 1 + rank % 3, primaryTribe: s.Tribe, howToPlayLines: howToPlay.Lines, whenToCommitLines: commit.Lines);
            }).ToList();
            tiers.Add(new CompGuideTier(tier.Key, guides));
        }

        return new CompGuideSet(CompGuideSources.HdtFree, tiers);
    }
}

/// <summary>
/// Card names and tavern tiers from HearthstoneJSON (api.hearthstonejson.com), fetched once into the harness's cache
/// folder. Until they are there, a card is known by its id.
/// </summary>
internal static class HarnessCards
{
    private static Dictionary<string, Card> _cards = new(StringComparer.Ordinal);

    public static void Install() =>
        Database.Lookup = id => _cards.TryGetValue(id, out var card) ? card : new Card(id, id, 0);

    /// <summary>Reads the cached card list, or fetches it on a background thread first; <paramref name="ready"/> runs on the calling thread's dispatcher.</summary>
    public static void Load(string directory, Action ready)
    {
        var path = Path.Combine(directory, "cards.json");
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                if (!File.Exists(path))
                {
                    Directory.CreateDirectory(directory);
                    using var client = new System.Net.WebClient();
                    client.DownloadFile("https://api.hearthstonejson.com/v1/latest/enUS/cards.json", path + ".part");
                    File.Move(path + ".part", path);
                }

                var cards = new Dictionary<string, Card>(StringComparer.Ordinal);
                foreach (var item in JArray.Parse(File.ReadAllText(path)))
                {
                    var id = item.Value<string>("id");
                    if (id != null)
                    {
                        cards[id] = new Card(id, item.Value<string>("name") ?? id, item.Value<int?>("techLevel") ?? 0);
                    }
                }

                _cards = cards;
                dispatcher.BeginInvoke(ready);
            }
            catch (Exception e)
            {
                Hearthstone_Deck_Tracker.Utility.Logging.Log.Warn("card names unavailable (" + e.Message + "): cards are shown by id");
            }
        });
    }
}
