using static BronzebeardHud.Stats.Tests.BridgeTestData;
using static BronzebeardHud.Stats.Tests.GuideTestData;

namespace BronzebeardHud.Stats.Tests;

public class CardEvidenceTests
{
    private static readonly CompGuide G = Guide("Guide", 1, 0, new[] { "K1", "K2" });

    // C: positions 2, 2 (golden), 1, then 3 and 4 on one board (counted once): on 4 of 5 boards, usually 2nd.
    // K2 on 2 boards (3rd, 2nd), W on 1, ABSENT on none.
    private static readonly Composition Comp = BridgeTestData.Comp("c", new[] { "K1", "K2" }, boards: new[]
    {
        Board(12, "X", "C", "K1"),
        Board(13, "Y", "C_G", "K2"),
        Board(14, "C", "K1"),
        Board(11, "Z", "W", "C", "C_G"),
        Board(10, "K1", "K2"),
    });

    private static readonly GuideEvidence Evidence = new(G, Comp, new[] { "K1", "K2" }, 2);

    private static (int, int, int?, string?, bool) Summary(CardEvidence e) => (e.BoardsWith, e.BoardCount, e.UsualPosition, e.Text, e.IsTop);

    [Fact]
    public void OnFourOfFiveBoards_ATwoCopyBoardCountsOnce_UsualPositionIsTheMostFrequent()
    {
        Assert.Equal((4, 5, (int?)2, "4/5 boards", true), Summary(CardEvidence.For("C", Evidence)));
        Assert.Equal("4/5", CardEvidence.For("C", Evidence).Count);
    }

    [Fact]
    public void AGoldenCardId_IsItsBaseCard()
    {
        var golden = CardEvidence.For("C_G", Evidence);

        Assert.Equal("C", golden.CardId);
        Assert.Equal(Summary(CardEvidence.For("C", Evidence)), Summary(golden));
    }

    [Fact]
    public void OnNoBoard_SaysZeroOfFive_WithoutPosition()
    {
        Assert.Equal((0, 5, (int?)null, "0/5 boards", false), Summary(CardEvidence.For("ABSENT", Evidence)));
    }

    [Fact]
    public void TwoBoardsIsTop_OneIsNot()
    {
        Assert.Equal((2, 5, (int?)2, "2/5 boards", true), Summary(CardEvidence.For("K2", Evidence))); // 3rd and 2nd: the leftmost
        Assert.Equal((1, 5, (int?)2, "1/5 boards", false), Summary(CardEvidence.For("W", Evidence)));
    }

    [Fact]
    public void ACompWithoutBoards_HasNoText()
    {
        var noBoards = new GuideEvidence(G, BridgeTestData.Comp("lists", new[] { "K1", "K2" }), new[] { "K1", "K2" }, 2);

        var evidence = CardEvidence.For("K1", noBoards);

        Assert.Equal((0, 0, (int?)null, (string?)null, false), Summary(evidence));
        Assert.False(evidence.HasBoards);
        Assert.Null(evidence.Count);
    }

    [Fact]
    public void BoardEvidence_OnlyForTargetsBridgedToACompWithBoards_InTargetOrder()
    {
        var targets = BridgeFixture.Targets();
        var bridge = BridgeFixture.Bridge();

        Assert.Equal(new (string, string, string?)[] { ("Undead Butcher", "undead_fs", "3/5"), ("Pirate Discover", "pirate_fs", "2/5") },
            BoardEvidence.For("TOP", targets, bridge).Select(b => (b.Target.Guide.Name, b.Card.Evidence.Composition.Id, b.Card.Count)));
        Assert.Empty(BoardEvidence.For("TOP", targets, null));
        Assert.Empty(BoardEvidence.For("TOP", targets, new Dictionary<string, GuideEvidence>()));

        var listsOnly = GuideBridge.For(BridgeFixture.All, new[] { BridgeTestData.Comp("lists", new[] { "U1", "U2", "U3" }) });
        Assert.True(listsOnly.ContainsKey(BridgeFixture.Undead.Id));
        Assert.Empty(BoardEvidence.For("U1", targets, listsOnly)); // bridged, but no board to count on
        Assert.Equal("Undead Butcher → undead_fs 3/5 pos 2", BoardEvidence.For("TOP", targets, bridge)[0].Diagnostic);
        Assert.Equal("Undead Butcher → undead_fs 0/5", BoardEvidence.For("R1", targets, bridge)[0].Diagnostic);
    }
}
