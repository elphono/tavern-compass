using BronzebeardHud.Stats;
using Hearthstone_Deck_Tracker.Hearthstone;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Harness;

/// <summary>
/// Synthetic data for the panels: invented compositions and guides built on real card ids (so that pictures can be
/// fetched), a few boards the player may hold, and the card names and tiers of HearthstoneJSON when they have been
/// fetched. Nothing here comes from Firestone or HSReplay.
/// </summary>
internal static class HarnessData
{
    /// <summary>
    /// Card ids already used by the repository's tests; any id works, one that does not exist simply has no picture.
    /// BG36_352 and BGS_004 (indices 10 and 17, on the Mech Magnet and Murloc Handbuff lines of the default scene) are
    /// the portraits with the widest white side margins measured (36 px of 256): they show whether the oval cut leaves
    /// a white crescent (TavernLayout.PortraitCut). They took the place of BG26_300 and BG30_002, which have no portrait
    /// on art.hearthstonejson.com (404).
    /// </summary>
    public static readonly string[] Pool =
    {
        "BG21_005", "BG23_008", "BG23_318", "BG24_022", "BG25_009", "BG25_010", "BG25_016", "BG25_040", "BG25_354", "BG26_174", "BG36_352", "BG26_817",
        "BG28_300", "BG28_309", "BG28_504", "BG28_573", "BG29_300", "BGS_004", "BG31_808", "BG31_815", "BG31_835", "BG32_324", "BG32_880", "BG33_823",
        "BG33_825", "BG34_170", "BG36_110", "BG36_114", "BG36_116", "BG36_318", "BG36_515",
    };

    private static readonly (string Id, string Name, string Tribe)[] Archetypes =
    {
        ("undead_butcher", "Undead Butcher", "UNDEAD"),
        ("pirate_discover", "Pirate Discover", "PIRATE"),
        ("mech_magnet", "Mech Magnet", "MECHANICAL"),
        ("beast_pack", "Beast Pack", "BEAST"),
        ("murloc_handbuff", "Murloc Handbuff", "MURLOC"),
        ("demon_gold", "Demon Gold", "DEMON"),
        ("elemental_cycle", "Elemental Cycle", "ELEMENTAL"),
        ("quilboar_choose", "Quilboar Choose One", "QUILBOAR"),
    };

    private static string Card(int index) => Pool[((index % Pool.Length) + Pool.Length) % Pool.Length];

    /// <summary>Eight compositions, each with its reference board, its core and add-on cards and four final boards.</summary>
    public static IReadOnlyList<Composition> Lobby { get; } = Archetypes.Select((a, i) =>
    {
        var board = Enumerable.Range(0, 7).Select(k => Card(i * 3 + k)).ToList();
        var finals = Enumerable.Range(0, 4)
            .Select(j => new FinalBoard(8100 - 40 * j, 12 + j, board.Take(6).Append(Card(i * 3 + 7 + j)).ToList()))
            .ToList();
        return new Composition(a.Id, a.Name, new[] { a.Tribe }, new[] { board[1], board[3] }, new[] { board[5] },
            averagePlacement: 3.4 + 0.15 * i, dataPoints: 400 - 30 * i, tier: null, finalBoards: finals, referenceBoard: board);
    }).ToList();

    /// <summary>What the player holds, as the plugin sees it: board then hand.</summary>
    public static IReadOnlyList<(string Name, PlayerCards Cards)> Scenarios { get; } = new[]
    {
        ("Nothing yet", PlayerCards.None),
        ("Two cards of one composition", Hold(board: new[] { Lobby[0].CoreCards[0], Lobby[0].CoreCards[1] }, hand: Array.Empty<string>())),
        ("A strong board, one in hand", Hold(board: new[] { Lobby[2].ReferenceBoard![0], Lobby[2].ReferenceBoard![1], Lobby[2].ReferenceBoard![2], Lobby[2].ReferenceBoard![3], Lobby[4].CoreCards[0] },
            hand: new[] { Lobby[2].ReferenceBoard![5] })),
    };

    private static PlayerCards Hold(string[] board, string[] hand) =>
        new(board.Select(id => new OwnedCard(id)).ToList(), hand.Select(id => new OwnedCard(id)).ToList());

    /// <summary>The comp guides HDT would show, in four tiers (S, A, B, C): fifteen guides with cards of their own.</summary>
    public static CompGuideSet Guides { get; } = BuildGuides();

    private static CompGuideSet BuildGuides()
    {
        var perTier = new[] { 3, 5, 4, 3 };
        var tiers = new List<CompGuideTier>();
        var n = 0;
        for (var tier = 1; tier <= perTier.Length; tier++)
        {
            var guides = new List<CompGuide>();
            for (var rank = 0; rank < perTier[tier - 1]; rank++, n++)
            {
                var name = n < Archetypes.Length ? Archetypes[n].Name : $"Synthetic guide {n + 1}";
                guides.Add(new CompGuide(name, tier, rank,
                    coreCards: new[] { Card(n * 2), Card(n * 2 + 1), Card(n * 2 + 2) },
                    addonCards: new[] { Card(n * 2 + 3), Card(n * 2 + 4) },
                    enablers: new[] { Card(n + 9) },
                    commitCards: new[] { Card(n * 2 + 1) }));
            }

            tiers.Add(new CompGuideTier(tier, guides));
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
