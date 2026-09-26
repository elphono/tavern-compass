namespace BronzebeardHud.Stats.Tests;

public class MinionLineupsTests
{
    private static Composition Comp(string id, string name, double placement, params FinalBoard[] boards) =>
        new(id, name, Array.Empty<string>(), new[] { $"{id}_KEY" }, Array.Empty<string>(), averagePlacement: placement, finalBoards: boards);

    private static FinalBoard B(int mmr, params string[] cards) => new(mmr, 14, cards);

    // BG28_309 sits 3rd in two undead boards, 1st in one mech board; BG31_808 in no board at all.
    private static readonly Composition Undead = Comp("undead", "Undead Butcher", 3.8,
        B(9100, "BG32_324", "BG25_010", "BG28_309", "BG32_880"),
        B(9900, "BG25_010", "BG32_324", "BG28_309_G"),
        B(8700, "BG32_324", "BG25_010", "BG36_515"));
    private static readonly Composition Mech = Comp("mech", "Mech Magnet", 4.1,
        B(9500, "BG28_309", "BG24_022", "BG25_040"),
        B(8200, "BG24_022", "BG25_040"));
    private static readonly Composition[] All = { Mech, Undead };

    [Fact]
    public void InTwoComps_AtDistinctPositions_MostBoardsFirst_BoardsByMmr_UsualPosition()
    {
        var lineups = MinionLineups.For("BG28_309", All);

        Assert.True(lineups.Found);
        Assert.Equal(new[] { "undead", "mech" }, lineups.Compositions.Select(l => l.Composition.Id)); // 2 boards before 1
        Assert.Equal(new[] { 9900, 9100 }, lineups.Compositions[0].Boards.Select(b => b.Mmr)); // highest MMR first, golden copy included
        Assert.Equal(new[] { 9500 }, lineups.Compositions[1].Boards.Select(b => b.Mmr));
        Assert.Equal(3, lineups.UsualPosition); // 3rd twice, 1st once
        Assert.Equal("Usually in position 3 of 7 · in 2 compositions", lineups.Headline);
        Assert.Equal("Undead Butcher 3,8 · in 2 of 3 top boards", lineups.Compositions[0].Label);
    }

    [Fact]
    public void Absent_SaysSo_InsteadOfAnEmptyPanel()
    {
        var lineups = MinionLineups.For("BG31_808", All);
        Assert.False(lineups.Found);
        Assert.Null(lineups.UsualPosition);
        Assert.Equal("In no top final board of the compositions in this lobby", lineups.Headline);
    }

    [Fact]
    public void ATieOnPositions_TakesTheLeftmost_AndFewerCompsWhenCapped()
    {
        // BG32_324: 1st twice (undead 9100 and 8700), 2nd once: 1.
        Assert.Equal(1, MinionLineups.For("BG32_324", All).UsualPosition);
        // BG24_022: 2nd and 1st once each, a tie: the leftmost, 1.
        Assert.Equal(1, MinionLineups.For("BG24_022", All).UsualPosition);
        Assert.Single(MinionLineups.For("BG28_309", All, maxCompositions: 1).Compositions);
    }
}
