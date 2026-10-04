using static BronzebeardHud.Stats.Tests.GuideTestData;

namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// What the single compositions panel asks of the library: the guide's text as runs (card names apart, in bold), the
/// difficulty badge as HDT draws it, which detail sections fit, how a guide line shows its core cards, the round's log
/// line, and the tribe names read from the game. Synthetic data only: invented names, dbf ids 9001… and card ids TST_….
/// </summary>
public class CompsPanelLogicTests
{
    private static string Describe(IReadOnlyList<CompGuideTextRun> line) =>
        string.Concat(line.Select(r => r.IsCard ? $"<{r.Text}|{r.CardId ?? "?"}>" : r.Text));

    [Fact]
    public void Text_LinesKeepCardNamesApart_ResolvedOrNot_AndAnUnclosedReferenceStaysText()
    {
        var parts = CompGuideText.Parse("Buy [[Alpha||9001]] + [[Beta]]\n\n[[Gamma||9999]] then [[Delta||9002]]. Open [[ end", CompGuideParserTests.CardIdOf);

        Assert.Equal(new[] { "Buy <Alpha|TST_001> + <Beta|?>", string.Empty, "<Gamma|?> then <Delta|TST_002>. Open [[ end" }, parts.Lines.Select(Describe));
        Assert.Equal("Buy Alpha + Beta\n\nGamma then Delta. Open [[ end", parts.PlainText); // unchanged: the runs are another view of it
        Assert.Equal(new[] { "TST_001", "TST_002" }, parts.CardIds);
    }

    [Fact]
    public void Parser_GivesTheGuideItsLines_FirstLineOfHowToPlay_AndOneLinePerCommitCondition_BlankOnesLeftOut()
    {
        var json = "[{\"name\":\"Alpha\",\"tier\":1,\"tier_rank\":0,\"difficulty\":3,\"primary_tribe\":17,\"core_cards\":[9001],\"addon_cards\":[]," +
                   "\"how_to_play\":\"\\nLevel early, find [[Alpha||9001]].\\nSecond line.\"," +
                   "\"when_to_commit\":\"[[Alpha||9001]] + [[Beta||9002]]\\n\\n  \\nAny 2 of [[Gamma||9003]]\",\"common_enablers\":\"\"}]";

        var guide = CompGuideParser.Parse(json, CompGuideParserTests.CardIdOf, CompGuideSources.HdtFree).All.Single();

        Assert.Equal("Level early, find <Alpha|TST_001>.", Describe(guide.HowToPlayFirstLine));
        Assert.Equal(2, guide.HowToPlayLines.Count);
        Assert.Equal(new[] { "<Alpha|TST_001> + <Beta|TST_002>", "Any 2 of <Gamma|TST_003>" }, guide.WhenToCommitLines.Select(Describe));
    }

    [Fact]
    public void Guide_BuiltFromPlainText_HasPlainLines_AndNoTextNoLines()
    {
        var plain = new CompGuide("Alpha", 1, 0, new[] { "A" }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            whenToCommit: "first\n\nsecond", howToPlay: "only");
        var bare = Guide("Beta", 1, 1, new[] { "B" });

        Assert.Equal(new[] { "first", "second" }, plain.WhenToCommitLines.Select(Describe));
        Assert.Equal("only", Describe(plain.HowToPlayFirstLine));
        Assert.Empty(bare.HowToPlayFirstLine);
        Assert.Empty(bare.WhenToCommitLines);
    }

    [Theory]
    [InlineData(1, "Hard", "#7f303e")]
    [InlineData(2, "Medium", "#917b43")]
    [InlineData(3, "Easy", "#49634b")]
    [InlineData(0, "Unknown", "#404040")]
    [InlineData(4, "Unknown", "#404040")]
    public void Difficulty_IsHdtsOwnBadge(int difficulty, string text, string colour)
    {
        Assert.Equal(text, CompGuideDifficulty.Text(difficulty));
        Assert.Equal(colour, CompGuideDifficulty.Colour(difficulty));
    }

    [Fact]
    public void Sections_AllWhenTheyFit_ElseInOrderWhatFits_ASectionNeverCut_TheMoreLineKept()
    {
        var heights = new[] { 40.0, 80, 80, 30, 60 }; // 290 in all

        var all = CompGuideLayout.Sections(heights, 290, moreLineHeight: 16);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, all.Shown);
        Assert.False(all.ShowsMoreLine);

        // 289: not all; 289 − 16 = 273 left: 40 + 80 + 80 = 200, then 30 (230), and 60 would make 290: left out.
        var some = CompGuideLayout.Sections(heights, 289, moreLineHeight: 16);
        Assert.Equal(new[] { 0, 1, 2, 3 }, some.Shown);
        Assert.True(some.ShowsMoreLine);
        Assert.Equal(5, some.Total);

        // 150 − 16 = 134: 40, 80 (120); the second 80 does not fit, but the 30 would make 150: no; nothing else fits.
        Assert.Equal(new[] { 0, 1 }, CompGuideLayout.Sections(heights, 150, 16).Shown);

        // A long first section that does not fit leaves its room to the shorter ones after it.
        Assert.Equal(new[] { 1, 2 }, CompGuideLayout.Sections(new[] { 500.0, 50, 50 }, 116, 16).Shown);

        // Exactly the room: shown (a box dragged to the content's height holds it).
        Assert.Equal(new[] { 0, 1 }, CompGuideLayout.Sections(new[] { 0.1 + 0.2, 0.3 }, 0.6, 16).Shown);
    }

    [Fact]
    public void Fit_AtLeastOne_ShowsTheFirstTargetWithItsHeader_WhenNothingFits_AndOnlyThen()
    {
        CompGuideFitItem Header(int group) => new(CompGuideItemKind.TierHeader, group, 20);
        CompGuideFitItem Row(int group, bool target = false) => new(CompGuideItemKind.Row, group, 60, target);
        var items = new[] { Header(0), Row(0), Row(0), Header(1), Row(1), Row(1, target: true) };

        Assert.Empty(CompGuideLayout.Fit(items, 50, 16).Shown); // without it: nothing
        var forced = CompGuideLayout.Fit(items, 50, 16, atLeastOne: true);
        Assert.Equal(new[] { 3, 5 }, forced.Shown);              // the target, under its own tier's bar
        Assert.Equal((1, 4), (forced.RowsShown, forced.RowsTotal));
        Assert.True(forced.ShowsMoreLine);

        var noTarget = new[] { Header(0), Row(0), Row(0), Header(1), Row(1) };
        Assert.Equal(new[] { 0, 1 }, CompGuideLayout.Fit(noTarget, 50, 16, atLeastOne: true).Shown);

        // Room for one row: what fits is shown, nothing more is forced (the target first, as Fit always does).
        Assert.Equal(new[] { 3, 5 }, CompGuideLayout.Fit(items, 20 + 60 + 16, 16, atLeastOne: true).Shown);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(2, 2, 0)]
    [InlineData(6, 6, 0)]
    [InlineData(7, 5, 2)]
    [InlineData(9, 5, 4)]
    public void ListOvals_AllUpToSix_ElseFiveAndTheRestCounted(int core, int ovals, int more) =>
        Assert.Equal((ovals, more), PanelFit.ListOvals(core));

    [Fact]
    public void RoundLine_HasTheExactFormat()
    {
        var a = Guide("Alpha", 1, 0, new[] { "A1", "A2", "A3" });
        var b = Guide("Beta", 2, 0, new[] { "B1", "B2" });
        var cards = new PlayerCards(Owned("A1", "A2", "B1"), Owned("X9"));
        var tracker = new CompTargetTracker();
        Assert.True(tracker.Toggle(b.Id));
        var targets = tracker.Next(CompGuideMatch.Rank(Set(a, b), cards), 3);

        Assert.Equal($"Bronzebeard HUD: comps round=6 source=hdt-free comps=2 board=3 hand=1 targets=[1. Beta {CompTargetTracker.Palette[0]} ★1/2 ticked; 2. Alpha {CompTargetTracker.Palette[1]} ★2/3]",
            CompTargets.RoundLine(6, CompGuideSources.HdtFree, 2, cards, targets));
        Assert.Equal("Bronzebeard HUD: comps round=1 source=none comps=0 board=0 hand=0 targets=none",
            CompTargets.RoundLine(1, null, 0, PlayerCards.None, Array.Empty<CompTarget>()));
    }

    [Theory]
    [InlineData(20, "PET", "BEAST")]    // .NET 8 names 20 PET; HDT's .NET Framework 4.8 names it BEAST: always BEAST
    [InlineData(20, "BEAST", "BEAST")]
    [InlineData(17, "MECHANICAL", "MECHANICAL")]
    [InlineData(26, "ALL", "ALL")]      // no Battlegrounds tribe of its own: the enum's name stays
    [InlineData(16, "SCOURGE", "SCOURGE")]
    public void TribeNames_ReadFromTheGame_AreTheBattlegroundsNames(int race, string enumName, string expected) =>
        Assert.Equal(expected, GuideTribes.NameOrEnum(race, enumName));
}
