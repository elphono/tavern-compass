using static BronzebeardHud.Stats.Tests.BridgeFixture;

namespace BronzebeardHud.Stats.Tests;

public class ChoiceNoteTests
{
    private const int Wide = 60;

    /// <summary>The labels' widths in marker characters, as ChoiceAdvicePanel computes them (the same at every resolution).</summary>
    private static int Room(ChoiceKind kind)
    {
        var card = ChoiceLayout.Cards(kind, 3, 1920, 1080)[0];
        return MarkerText.MaxChars(card.Width, PanelTypography.Px(PanelTypography.Marker, 1080), TavernLayout.MarkerPadding * TavernLayout.Scale(1080));
    }

    private static OfferedOption Minion(int id, string cardId) => new(id, cardId, "MINION");

    private static OfferedOption Trinket(int id, string cardId, string text) => new(id, cardId, "BATTLEGROUND_TRINKET", text: text);

    private static readonly CardTurnNote Better = new(6, 3.6, 3.92, 400, CardTurnVerdict.Better);
    private static readonly CardTurnNote Worse = new(6, 4.4, 3.92, 600, CardTurnVerdict.Worse);

    /// <summary>
    /// Every kind of line: a target's core card with its boards, an add-on, the top boards of two targets, a pivot, a core card
    /// of another guide, a card's value both ways, a card for three guides ("+k more"), and nothing.
    /// </summary>
    private static ChoiceAdvice Every()
    {
        var crowded = Guide("Crowded", 1, 0, new[] { "C1", "C2" }, tribe: 17);
        var second = Guide("Second Crowd", 2, 0, new[] { "C1", "C3" }, tribe: 17);
        var third = Guide("Third Crowd", 3, 0, new[] { "C1", "C4" }, tribe: 17);
        var guides = GuideTestData.Set(Undead, Pirates, Reborn, MechShield, crowded, second, third);
        var cards = new[] { "U2", "UA", "TOP", "R1", "M1", "GOOD", "BAD", "C1", "NEUTRAL" };
        return ChoiceAdvisor.Advise(cards.Select((c, i) => Minion(100 + i, c)).ToArray(), Held, Targets(), guides, Lobby, bridge: GuideBridge.For(guides, Comps),
            cardValue: id => id == "GOOD" ? Better : id == "BAD" ? Worse : null);
    }

    private static CompGuide Guide(string name, int tier, int rank, string[] core, int tribe) => GuideTestData.Guide(name, tier, rank, core, tribe: tribe);

    [Fact]
    public void WhereThereIsRoom_EveryLineSaysWhatTheLineOfBeforeSaid_ThatTheLogStillWrites()
    {
        var advice = Every();

        foreach (var option in advice.Options)
        {
            var note = ChoiceNote.For(option, Wide, statsLoaded: true);
            Assert.Equal(ChoiceAdvisor.Lines(option, Wide, statsLoaded: true), note.Lines.Select(l => l.Text));
        }

        Assert.Equal(new[]
        {
            "★ core Undead Butcher 2/3→3/3 · 4/5 boards", "+ Undead Butcher · 1/5 boards", "+ Undead Butcher 3/5 boards", "pivot → Undead Reborn (S)",
            "core Mech Shield (S)", "t6 ▲ 3.6 vs 3.9", "t6 ▼ 4.4 vs 3.9", "core Crowded (S)", "—",
        }, advice.Options.Select(o => ChoiceNote.For(o, Wide, true).Lines[0].Text));
        Assert.Equal("+2 more", ChoiceNote.For(advice.Options[7], Wide, true).Lines[1].Text);
    }

    [Fact]
    public void EachRole_ItsPill_ReadWithoutReadingTheText()
    {
        var lines = Every().Options.Select(o => ChoiceNote.For(o, Wide, true).Lines).ToList();

        Assert.Equal(new[] { ChoiceRole.Core, ChoiceRole.Support, ChoiceRole.Boards, ChoiceRole.Pivot, ChoiceRole.Guide, ChoiceRole.CardBetter, ChoiceRole.CardWorse, ChoiceRole.Guide, ChoiceRole.Nothing },
            lines.Select(l => l[0].Role));
        Assert.Equal(new[] { "★ core", "+", "+", "pivot →", "core", "t6 ▲", "t6 ▼", "core", "" }, lines.Select(l => l[0].Pill));
        Assert.Equal(new[] { ChoiceNote.CoreFill, ChoiceNote.SupportFill, ChoiceNote.SupportFill, ChoiceNote.PivotFill, null, ChoiceNote.BetterFill, ChoiceNote.WorseFill, null, null },
            lines.Select(l => l[0].PillFill));
        Assert.Equal((ChoiceRole.More, string.Empty, (string?)null), (lines[7][1].Role, lines[7][1].Pill, lines[7][1].PillFill));

        // The filled pills of different roles never share a colour, nor take a target's.
        var fills = new[] { ChoiceNote.CoreFill, ChoiceNote.SupportFill, ChoiceNote.PivotFill, ChoiceNote.BetterFill, ChoiceNote.WorseFill };
        Assert.Equal(fills.Length, fills.Distinct().Count());
        Assert.Empty(fills.Intersect(CompTargetTracker.Palette));
    }

    [Fact]
    public void TheNameInItsTargetsColour_AGuideThatIsNoTargetInWhite_TheRimFollows()
    {
        var advice = Every();
        var notes = advice.Options.Select(o => ChoiceNote.For(o, Wide, true)).ToList();
        var first = Targets()[0].Colour;

        Assert.Equal(new[] { first, first, first, ChoiceNote.White, ChoiceNote.White, ChoiceNote.White, ChoiceNote.White, ChoiceNote.White, ChoiceNote.White },
            notes.Select(n => n.Lines[0].NameColour));
        Assert.Equal(new[] { first, first, first, ChoiceNote.NeutralRim, ChoiceNote.NeutralRim, ChoiceNote.NeutralRim, ChoiceNote.NeutralRim, ChoiceNote.NeutralRim, ChoiceNote.NeutralRim },
            notes.Select(n => n.Rim));
        Assert.Equal(new[] { "Undead Butcher", "2/3→3/3", "· 4/5 boards" }, new[] { notes[0].Lines[0].Name, notes[0].Lines[0].Count, notes[0].Lines[0].Suffix });
        Assert.Equal(("Undead Reborn", "S"), (notes[3].Lines[0].Name, notes[3].Lines[0].Tier));
        Assert.Equal(("3.6", "vs 3.9"), (notes[5].Lines[0].Count, notes[5].Lines[0].Suffix));
    }

    /// <summary>
    /// At the widths the game gives (discover 36 marker characters, Dark Gift 43, trinket 28: measured by ChoiceLayout and
    /// MarkerText), no line is wider than its label, its pill counted; narrower, the name shortens before the suffix goes,
    /// and the line never loses a whole piece to a cut.
    /// </summary>
    [Fact]
    public void AtTheGamesWidths_EveryLineFits_TheNameShortensFirst()
    {
        Assert.Equal((36, 43, 28), (Room(ChoiceKind.Discover), Room(ChoiceKind.DarkGift), Room(ChoiceKind.Trinket)));
        var advice = Every();
        foreach (var width in new[] { Room(ChoiceKind.Discover), Room(ChoiceKind.DarkGift) })
        {
            Assert.All(advice.Options.SelectMany(o => ChoiceNote.For(o, width, true).Lines), line => Assert.True(line.Width <= width, $"{width}: {line.Text} is {line.Width}"));
        }

        var core = advice.Options[0];
        Assert.Equal("★ core U. Butcher 2/3→3/3 · 4/5 boards", ChoiceNote.For(core, 41, true).Lines[0].Text); // exactly 41 wide with the pill
        Assert.Equal(41, ChoiceNote.For(core, 41, true).Lines[0].Width);
        Assert.Equal("★ core UB 2/3→3/3 · 4/5 boards", ChoiceNote.For(core, 36, true).Lines[0].Text);
        Assert.Equal("★ core U. Butcher 2/3→3/3", ChoiceNote.For(core, 30, true).Lines[0].Text);
        Assert.Equal(("★ core", "UB", "2/3→3/3"), (ChoiceNote.For(core, 22, true).Lines[0].Pill, ChoiceNote.For(core, 22, true).Lines[0].Name, ChoiceNote.For(core, 22, true).Lines[0].Count));
    }

    [Fact]
    public void AHeldCoreCardAgain_IsACopy_NotAStep()
    {
        var advice = ChoiceAdvisor.Advise(new[] { Minion(1, "U1"), Minion(2, "NEUTRAL") }, Held, Targets(), All, Lobby, bridge: Bridge());
        var line = ChoiceNote.For(advice.Options[0], Wide, true).Lines[0];

        Assert.Equal((ChoiceRole.Core, "★ core", "2/3 copy", "· 4/5 boards"), (line.Role, line.Pill, line.Count, line.Suffix));
        Assert.Equal(ChoiceAdvisor.Lines(advice.Options[0], Wide, true), new[] { line.Text });
    }

    [Fact]
    public void ACardsValue_TheComparisonFirst_ThenTheFigureAlone_ThenNothing()
    {
        var good = Every().Options[5];
        string[] At(int width) => ChoiceNote.For(good, width, true).Lines.Select(l => l.Text).ToArray();

        Assert.Equal(new[] { "t6 ▲ 3.6 vs 3.9" }, At(17)); // 15 characters and the pill's 2
        Assert.Equal(new[] { "t6 ▲ 3.6" }, At(16));
        Assert.Equal(new[] { "t6 ▲ 3.6" }, At(10));
        Assert.Equal(new[] { "—" }, At(9));
    }

    [Fact]
    public void LinesWithoutAPill_CountNoPadding_AndMoreShortensWhereItMust()
    {
        var crowded = Every().Options[7];

        Assert.Equal(1, ChoiceNote.For(Every().Options[8], Wide, true).Lines[0].Width); // "—"
        Assert.Equal(("+2 more", 7), (ChoiceNote.For(crowded, 7, true).Lines[1].Text, ChoiceNote.For(crowded, 7, true).Lines[1].Width));
        Assert.Equal("+2", ChoiceNote.For(crowded, 6, true).Lines[1].Text);
    }

    private static (ChoiceAdvice Advice, TrinketReference Reference) Trinkets()
    {
        // The bracket's trinkets place 4.00 on average (a filler of many games); the offered ones sit in each band.
        var stats = new[]
        {
            new TrinketStat("GOOD", 3.70, 5000, 0.44, new Dictionary<int, double> { [25] = 3.75 }),
            new TrinketStat("EDGE", 3.70, 5000, 0.31, new Dictionary<int, double> { [25] = 3.80 }),
            new TrinketStat("MID", 4.00, 5000, null, new Dictionary<int, double> { [25] = 4.05 }),
            new TrinketStat("BAD", 4.30, 5000, 0.12, new Dictionary<int, double> { [25] = 4.20 }),
            new TrinketStat("UNDEAD", 4.00, 5000, 0.20, new Dictionary<int, double> { [25] = 4.10 }),
            new TrinketStat("CONTEST", 4.00, 4000, 0.22, new Dictionary<int, double> { [25] = 4.10 }),
            new TrinketStat("FILLER", 4.00, 1_000_000, 0.1, new Dictionary<int, double> { [25] = 4.00 }),
        }.ToDictionary(t => t.TrinketCardId);
        var firestone = new SourceSnapshot(new StatProvenance(StatsSources.Firestone, null, null, null, "last-patch", null, null),
            new[] { new StatRecord("trinket", "CONTEST", "placement", 4.00, 4000, "games"), new StatRecord("trinket", "GOOD", "placement", 3.70, 5000, "games") },
            new Dictionary<string, double> { ["trinket"] = 3.9 });
        var nomi = new SourceSnapshot(new StatProvenance(StatsSources.NomiGg, null, null, null, "since 2026-10-02", null, null), new[]
        {
            new StatRecord("trinket", "CONTEST", "placement", 2.90, 300, "games"),
            new StatRecord("trinket", "GOOD", "placement", 3.40, 55, "games"),
        }, new Dictionary<string, double> { ["trinket"] = 3.6 });
        var view = StatsConsolidation.Consolidate(new[] { firestone, nomi }, 25);
        var options = new[] { "GOOD", "EDGE", "MID", "BAD", "UNDEAD", "CONTEST", "UNKNOWN" }
            .Select((id, i) => Trinket(i + 1, id, id == "UNDEAD" ? "Your Undead have +3 Attack." : "Gain 2 Gold.")).ToArray();
        var advice = ChoiceAdvisor.Advise(options, Held, Targets(), All, Lobby, id => stats.TryGetValue(id, out var s) ? s : null, bracket: 25,
            trinketView: id => view.Find("trinket", id, "placement", "games"));
        return (advice, TrinketReference.Of(stats.Values, 25)!);
    }

    [Fact]
    public void Trinket_ThePlacementLarge_InTheColourOfItsBand_AmongTheBracketsTrinkets()
    {
        var (advice, reference) = Trinkets();
        Assert.Equal(4.00, reference.Mean, precision: 2);
        var notes = advice.Options.Select(o => ChoiceNote.For(o, Room(ChoiceKind.Trinket), true, 2, reference).Trinket!).ToList();

        Assert.Equal(new[] { "3.75", "3.80", "4.05", "4.20", "≈3.80", "3.2 ↔ 4.0", null }, notes.Select(n => n.Figure));
        Assert.Equal(new[] { PlacementBand.Good, PlacementBand.Good, PlacementBand.Average, PlacementBand.Bad, PlacementBand.Good, PlacementBand.None, PlacementBand.None },
            notes.Select(n => n.Band));
        Assert.Equal(new[] { PlacementGrade.GoodColour, PlacementGrade.GoodColour, PlacementGrade.AverageColour, PlacementGrade.BadColour, PlacementGrade.GoodColour, HeroNote.Contested, HeroNote.Missing },
            notes.Select(n => n.Colour));
        Assert.Equal(new[] { false, false, false, false, false, true, false }, notes.Select(n => n.Contested));
    }

    [Fact]
    public void Trinket_TheSmallLines_SayWhatTheLinesOfBeforeSaid()
    {
        var (advice, reference) = Trinkets();
        var details = advice.Options.Select(o => ChoiceNote.For(o, Room(ChoiceKind.Trinket), true, 2, reference)).ToList();
        string[] Texts(int i) => details[i].Trinket!.Details.Select(d => d.Text).ToArray();

        Assert.Equal(new[] { "avg place · 44% picked", "nomi.gg 3.70 (55)" }, Texts(0)); // agreeing: nomi.gg's credit, recentred
        Assert.Equal(new[] { "avg place · 31% picked" }, Texts(1));
        Assert.Equal(new[] { "avg place" }, Texts(2));                                // no pick rate
        Assert.Equal(new[] { "avg 4.10", "≈ Undead Butcher" }, Texts(4));              // adjusted to the first target, in its colour
        Assert.Equal(Targets()[0].Colour, details[4].Trinket!.Details[1].Colour);
        Assert.Equal(new[] { "contested", "nomi.gg ↔ FS" }, Texts(5));
        Assert.Equal(HeroNote.Contested, details[5].Trinket!.Details[0].Colour);
        Assert.Equal(new[] { "no data" }, Texts(6));
        Assert.Equal(new[] { "loading…" }, ChoiceNote.For(advice.Options[6], Room(ChoiceKind.Trinket), statsLoaded: false, 2, reference).Trinket!.Details.Select(d => d.Text));

        // As many lines as the log's, so that they fit the label ChoiceLayout sizes on those.
        Assert.All(advice.Options, o => Assert.Equal(ChoiceAdvisor.Lines(o, Room(ChoiceKind.Trinket), true).Count,
            ChoiceNote.For(o, Room(ChoiceKind.Trinket), true, 2, reference).Trinket!.Details.Count));
        // Every small line fits beside its figure.
        Assert.All(details, n => Assert.All(n.Trinket!.Details, d =>
            Assert.True(MarkerText.DisplayLength(d.Text) <= ChoiceNote.DetailChars(Room(ChoiceKind.Trinket), n.Trinket.Figure ?? string.Empty), d.Text)));
        // The rim takes the band's colour; a trinket without stats is neutral.
        Assert.Equal(new[] { PlacementGrade.GoodColour, PlacementGrade.BadColour, HeroNote.Contested, ChoiceNote.NeutralRim }, new[] { 0, 3, 5, 6 }.Select(i => details[i].Rim));
    }

    [Fact]
    public void Trinket_NarrowerLabels_ShortenTheSmallLines_NeverCutThem()
    {
        var (advice, reference) = Trinkets();
        string[] At(int width) => ChoiceNote.For(advice.Options[0], width, true, 2, reference).Trinket!.Details.Select(d => d.Text).ToArray();

        Assert.Equal(new[] { "avg · 44% picked", "nomi.gg 3.70 (55)" }, At(23)); // 17 small characters beside "3.75": just room for the games
        Assert.Equal(new[] { "avg · 44% picked", "nomi.gg 3.70" }, At(22)); // 16
        Assert.Equal(new[] { "avg place · 44% picked" }, ChoiceNote.For(advice.Options[0], 28, true, 1, reference).Trinket!.Details.Select(d => d.Text)); // one line: no source beside
        Assert.Equal(new[] { "44% picked", "nomi.gg 3.70" }, At(20));
        Assert.Equal(new[] { "avg" }, ChoiceNote.For(advice.Options[2], 15, true, 2, reference).Trinket!.Details.Select(d => d.Text));

        // A contest keeps both figures and its sources: the arrow loses its spaces first.
        Assert.Equal("3.2 ↔ 4.0", ChoiceNote.For(advice.Options[5], 26, true, 2, reference).Trinket!.Figure);
        Assert.Equal("3.2↔4.0", ChoiceNote.For(advice.Options[5], 25, true, 2, reference).Trinket!.Figure);
    }

    [Fact]
    public void Trinket_AConsensusOfFirestoneAlone_NamesNoOtherSource()
    {
        // Two of Firestone's brackets agreeing: a consensus with no other source to credit, so no empty second line.
        var stat = new TrinketStat("T", 3.80, 2000, 0.3, new Dictionary<int, double> { [25] = 3.80 });
        var view = StatsConsolidation.Consolidate(new[]
        {
            new SourceSnapshot(new StatProvenance(StatsSources.Firestone, null, null, null, "last-patch", 25, null), new[] { new StatRecord("trinket", "T", "placement", 3.80, 2000, "games") }),
            new SourceSnapshot(new StatProvenance(StatsSources.Firestone, null, null, null, "last-patch", 50, null), new[] { new StatRecord("trinket", "T", "placement", 3.82, 2000, "games") }),
        }, 25);
        var advice = ChoiceAdvisor.Advise(new[] { Trinket(1, "T", "Gain 2 Gold."), Trinket(2, "U", "Gain 3 Gold.") }, Held, Targets(), All, Lobby,
            id => id == "T" ? stat : null, bracket: 25, trinketView: id => view.Find("trinket", id, "placement", "games"));

        Assert.Equal(StatVerdict.Consensus, advice.Options[0].Trinket!.Consolidated!.Verdict);
        Assert.Equal(new[] { "avg place · 30% picked" }, ChoiceNote.For(advice.Options[0], Room(ChoiceKind.Trinket), true).Trinket!.Details.Select(d => d.Text));
    }

    [Fact]
    public void Trinket_WithoutAReference_ShownUngraded()
    {
        var (advice, _) = Trinkets();
        var note = ChoiceNote.For(advice.Options[0], Room(ChoiceKind.Trinket), true).Trinket!;

        Assert.Equal(("3.75", PlacementBand.None, PlacementGrade.UngradedColour), (note.Figure, note.Band, note.Colour));
    }

    [Fact]
    public void TheTrinketsNote_IsLargeYetHoldsInAOneLineLabel()
    {
        Assert.True(PanelTypography.ChoiceNote >= 1.6 * PanelTypography.Small);
        Assert.Contains(PanelTypography.ChoiceNote, PanelTypography.All);
        var label = ChoiceLayout.Labels(ChoiceKind.Trinket, 4, 1920, 1080, 1)[0];
        Assert.True(PanelTypography.ChoiceNote <= label.Height - 2 * 1.5, $"{label.Height:0.0} px label"); // inside its 1.5 px rim
    }
}
