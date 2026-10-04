using System.Globalization;
using BronzebeardHud.Stats;
using Hearthstone_Deck_Tracker.Hearthstone;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Harness;

/// <summary>
/// Synthetic data for the panels: invented comp guides built on real card ids (so that pictures can be fetched), a few
/// boards the player may hold, Bob's row, and the card names and tiers of HearthstoneJSON when they have been fetched.
/// Nothing here comes from Firestone or HSReplay: the guides' names, card lists and texts are made up.
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

    /// <summary>What the player holds, as the plugin sees it: board then hand; 0, 1 and 3 targets with three targets wanted.</summary>
    public static IReadOnlyList<(string Name, PlayerCards Cards)> Scenarios { get; } = new[]
    {
        ("Nothing yet", PlayerCards.None),
        ("Two cards of one composition", Hold(board: new[] { 40, 41 }, hand: Array.Empty<int>())),               // Elemental Cycle only
        ("A strong board, one in hand", Hold(board: new[] { 6, 7, 8, 13, 14 }, hand: new[] { 9 })),             // Pirate Discover, Mech Magnet, Mech Divine Shield
    };

    /// <summary>Targets each scenario must give with three targets wanted (the self-test checks it).</summary>
    public static readonly int[] ExpectedTargets = { 0, 1, 3 };

    /// <summary>
    /// Bob's row for the scene: with the third scenario's targets, a core card of a target not held yet (16, Mech Divine
    /// Shield, also an add-on of Mech Magnet: core wins), a core card held again (7, Pirate Discover), an add-on (11, Pirate
    /// Discover), an enabler (21, Mech Divine Shield), a pinned card that serves no target (42) and two that serve nothing.
    /// </summary>
    public static IReadOnlyList<string> Shop { get; } = new[] { 16, 11, 7, 42, 50, 21, 57 }.Select(i => Pool[i]).ToList();

    public static TavernPins Pins { get; } = TavernPins.Of(new[] { Pool[42] });

    private static PlayerCards Hold(int[] board, int[] hand) =>
        new(board.Select(i => new OwnedCard(Pool[i])).ToList(), hand.Select(i => new OwnedCard(Pool[i])).ToList());

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
