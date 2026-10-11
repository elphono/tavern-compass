namespace BronzebeardHud.Stats.Tests;

public class HeroNoteTests
{
    private const int Player = 25;

    private static readonly double[] Odds = { 14.0, 13.0, 13.0, 12.0, 12.0, 12.0, 12.0, 12.0 }; // top 4 52%, 1st 14%

    private static readonly OfferedHero Hero = new(entityId: 90, cardId: "H", baseCardId: "H", position: 1);

    private static SourceSnapshot Snap(string source, int? mmr, double placement, int games) =>
        new(new StatProvenance(source, null, null, null, "last-patch", mmr, null), new[] { new StatRecord("hero", "H", "placement", placement, games, "games") });

    private static HeroFigures Figures(string source, string? tier, double placement, double? pick, int? mmr) =>
        new(source, tier, placement, pick, 4000, mmr, Odds);

    private static HeroPickRow Row(ConsolidatedStat? stat, params HeroFigures[] figures) => new(Hero, figures, stat);

    private static ConsolidatedStat? Stat(params SourceSnapshot[] snapshots) =>
        StatsConsolidation.Consolidate(snapshots, Player).Find("hero", "H", "placement", "games");

    [Fact]
    public void Consensus_TheTierInItsShield_TheFigureLargeAndWhite_ItsSampleUnder_ThenSourcesAndOdds()
    {
        var stat = Stat(Snap("firestone", Player, 3.40, 4000), Snap("nomi.gg", null, 3.35, 51));
        var note = HeroNote.For(Row(stat, Figures("firestone", "A", 3.40, 0.18, Player)));

        Assert.Equal(("A", "#FF9F1C"), (note.Tier, note.TierFill));
        Assert.Equal(("3.41", HeroNote.Plain), (note.Figure, note.FigureColour));
        Assert.Equal(("4,051 games", HeroNote.Secondary), (note.Caption, note.CaptionColour));
        Assert.Equal(HeroNote.Rim, note.RimColour);
        // nomi.gg's credit stays on screen: it is a promise to the site's owner.
        Assert.Equal(new[] { "FS 25% + nomi.gg", "top 4 52% · 1st 14%" }, note.Details.Select(d => d.Text));
        Assert.All(note.Details, d => Assert.Equal((HeroNote.Secondary, (string?)null), (d.Colour, d.Tier)));
    }

    [Fact]
    public void Contested_BothFiguresInAmber_TheWordUnder_AnAmberRim_NeverTheMean()
    {
        var stat = Stat(Snap("firestone", Player, 3.80, 4000), Snap("nomi.gg", null, 2.86, 51));
        var note = HeroNote.For(Row(stat, Figures("firestone", "B", 3.80, 0.09, Player)));

        // "2.9 ↔ 3.8" is nine characters, one more than the note's place holds at its size: the arrow loses its spaces.
        Assert.Equal(("2.9↔3.8", HeroNote.Contested), (note.Figure, note.FigureColour));
        Assert.Equal(("contested", HeroNote.Contested, HeroNote.Contested), (note.Caption, note.CaptionColour, note.RimColour));
        Assert.Equal(new[] { "nomi.gg ↔ FS 25%", "top 4 52% · 1st 14%" }, note.Details.Select(d => d.Text));
        Assert.Equal(("B", "#FFE03D"), (note.Tier, note.TierFill));
    }

    [Fact]
    public void NoSourceKnowsTheHero_NoData_GreyShield_NoFigure()
    {
        var note = HeroNote.For(Row(null));

        Assert.Equal(("–", HeroNote.Missing, (string?)null), (note.Tier, note.TierFill, note.Figure));
        Assert.Equal(("no data", HeroNote.Missing, HeroNote.Rim), (note.Caption, note.CaptionColour, note.RimColour));
        Assert.Empty(note.Details);
    }

    [Fact]
    public void WithoutAConsolidatedLine_TheFirstSourceIsTheNote_ASecondSourceASmallLineWithItsTier()
    {
        var note = HeroNote.For(Row(null, Figures("firestone", "S", 3.42, 0.18, Player), Figures("hsreplay-manual", "C", 3.10, 0.12, 10)));

        Assert.Equal(("S", "3.42", HeroNote.Plain), (note.Tier, note.Figure, note.FigureColour));
        Assert.Equal("18% pick · FS 25%", note.Caption);
        Assert.Equal(new[] { "top 4 52% · 1st 14%", "3.10 · 12% · HSR 10%" }, note.Details.Select(d => d.Text)); // the long form is 25 wide
        Assert.Equal(new[] { null, "C" }, note.Details.Select(d => d.Tier));

        // A source without a tier, a pick rate or a bracket: "–", and the figure with its source only.
        var bare = HeroNote.For(Row(null, new HeroFigures("firestone", null, 4.10, null, 3000)));
        Assert.Equal(("–", HeroNote.Missing, "4.10", "FS"), (bare.Tier, bare.TierFill, bare.Figure, bare.Caption));
        Assert.Empty(bare.Details);

        // Every player (100%) names no bracket, as the consolidated line does.
        Assert.Equal("20% pick · FS", HeroNote.For(Row(null, new HeroFigures("firestone", "B", 4.10, 0.2, 3000, 100))).Caption);
    }

    [Fact]
    public void Tiers_EachItsColour_EAndNoneGrey()
    {
        var colours = new[] { "S", "A", "B", "C", "D" }.Select(HeroNote.TierColour).ToList();

        Assert.Equal(new[] { "#FF4D4D", "#FF9F1C", "#FFE03D", "#7CD95C", "#4DA6FF" }, colours);
        Assert.Equal(HeroNote.Missing, HeroNote.TierColour("E"));
        Assert.Equal(HeroNote.Missing, HeroNote.TierColour(null));
        Assert.DoesNotContain(HeroNote.Missing, colours);
    }

    /// <summary>
    /// The note dominates: its figure is drawn at least 1.6 times the size of the lines under it, and the badge holds the head
    /// (shield, figure, caption) and two small lines at their own size; every text of these notes fits its place, by the same
    /// generous estimate as the labels (MarkerText).
    /// </summary>
    [Fact]
    public void TheNoteDominates_AndEveryTextFitsItsPlace()
    {
        Assert.True(PanelTypography.HeroNote >= 1.6 * PanelTypography.Small, $"{PanelTypography.HeroNote} against {PanelTypography.Small}");
        Assert.True(PanelTypography.HeroNote >= 1.6 * PanelTypography.Body);
        Assert.Contains(PanelTypography.HeroNote, PanelTypography.All);
        Assert.Equal(new[] { 0, 1, 2 }, HeroPickLayout.ItemsThatFit(new[] { HeroPickLayout.HeadHeight, HeroPickLayout.DetailLine, HeroPickLayout.DetailLine }, HeroPickLayout.ContentHeight));

        var consensus = Stat(Snap("firestone", Player, 3.40, 4000), Snap("nomi.gg", null, 3.35, 51));
        var contested = Stat(Snap("firestone", Player, 3.80, 4000), Snap("nomi.gg", null, 2.86, 51));
        var notes = new[]
        {
            HeroNote.For(Row(consensus, Figures("firestone", "A", 3.40, 0.18, Player))),
            HeroNote.For(Row(contested, Figures("firestone", "B", 3.80, 0.09, Player))),
            HeroNote.For(Row(null, Figures("firestone", "S", 3.42, 0.18, Player), Figures("hsreplay-manual", "C", 3.10, 0.12, 10))),
            HeroNote.For(Row(null)),
        };
        var figureRoom = HeroNote.Chars(HeroPickLayout.NoteWidth, PanelTypography.HeroNote);
        var captionRoom = HeroNote.Chars(HeroPickLayout.NoteWidth, PanelTypography.Small);
        var lineRoom = HeroNote.Chars(HeroPickLayout.ContentWidth, PanelTypography.Small);
        Assert.All(notes, note =>
        {
            Assert.True(note.Figure == null || MarkerText.DisplayLength(note.Figure) <= figureRoom, note.Figure);
            Assert.True(MarkerText.DisplayLength(note.Caption) <= captionRoom, note.Caption);
            Assert.All(note.Details, d => Assert.True(MarkerText.DisplayLength(d.Text) + (d.Tier == null ? 0 : 3) <= lineRoom, d.Text));
        });
    }

    [Fact]
    public void TheConsensusLineOfTheLog_IsUnchanged_ItsPiecesAreTheNote()
    {
        var line = HeroConsensus.For(Stat(Snap("firestone", Player, 3.40, 4000), Snap("nomi.gg", null, 3.35, 51)))!;
        var contest = HeroConsensus.For(Stat(Snap("firestone", Player, 3.80, 4000), Snap("nomi.gg", null, 2.86, 51)))!;

        Assert.Equal(("3.41", "4,051 games", "3.41 · 4,051 games"), (line.Note, line.Sample, line.Figure));
        Assert.Equal(("2.9 ↔ 3.8", (string?)null, "2.9 ↔ 3.8 contested"), (contest.Note, contest.Sample, contest.Figure));
    }
}
