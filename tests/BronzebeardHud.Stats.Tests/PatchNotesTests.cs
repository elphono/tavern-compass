using static BronzebeardHud.Stats.Tests.GuideTestData;

namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// What nomi.gg says of the patch (issue #11, components 4 to 7): the banner under the heroes for the first games of a patch,
/// a guide's tribe since the patch, the tier-up pace and the data's freshness in HDT's log. Invented figures.
/// </summary>
public class PatchNotesTests
{
    private static NomiAnalysisFile Nomi(int? build = 253216, params NomiTribe[] tribes) => new(
        new StatProvenance(StatsSources.NomiGg, null, null, null, "since 2026-10-02", null, "36.6.3"), build,
        new[] { "quilboar", "mech" }, new[] { "aberration" }, Array.Empty<NomiHero>(),
        tribes.Length > 0 ? tribes : new[]
        {
            new NomiTribe("undead", 300, 4.48, 900, 3.93),   // 2.3 × √(1/300 + 1/900) = 0.153: 0.55 is beyond 2σ
            new NomiTribe("beast", 111, 3.80, 441, 3.49),    // σ = 0.244: 0.31 is within 2σ
            new NomiTribe("mech", 101, 3.96, 575, 4.89),     // worse, beyond 2σ
            new NomiTribe("naga", 0, null, 0, null),
        },
        Array.Empty<NomiTrinket>(),
        new[]
        {
            new NomiTierMedian("all", "top4", 5, 8, 0.99), new NomiTierMedian("all", "first", 5, 7, 1.0),
            new NomiTierMedian("high", "top4", 5, 7.5, 1.0), new NomiTierMedian("all", "top4", 6, 10, 0.78),
        });

    [Fact]
    public void Banner_NamesThePatchItsBuffsAndNerfs_AndTheSource()
    {
        Assert.Equal("Patch 36.6.3 · buffed Quilboar, Mech · nerfed Aberration · nomi.gg", PatchNotes.Banner(Nomi()));
        Assert.Null(PatchNotes.Banner(null));
    }

    /// <summary>The banner shows for the first three games of a patch, then goes by itself; a new patch starts again.</summary>
    [Fact]
    public void Banner_TheFirstThreeGamesOfAPatch_ThenNoMore_ANewPatchAgain()
    {
        string? state = null;
        var shown = new List<bool>();
        foreach (var patch in new[] { "36.6.3", "36.6.3", "36.6.3", "36.6.3", "36.6.3", "36.7", "36.7" })
        {
            var (show, next) = PatchNotes.CountGame(state, patch);
            shown.Add(show);
            state = next;
        }

        Assert.Equal(new[] { true, true, true, false, false, true, true }, shown);
        Assert.True(PatchNotes.CountGame("not json", "36.6.3").Show); // an unreadable state is a state never written
    }

    /// <summary>Component 7: the guide's main tribe, before and after the patch; ▲ or ▼ only beyond the noise (2σ).</summary>
    [Fact]
    public void TribeSince_TheGuidesTribe_ArrowOnlyBeyondTheNoise()
    {
        Assert.Equal("Undead ▲ 4.48 → 3.93 since 36.6.3 (nomi.gg, 900 games)", PatchNotes.TribeSince(Guide("U", 1, 0, new[] { "X" }, tribe: 11), Nomi()));
        Assert.Equal("Beast 3.80 → 3.49 since 36.6.3 (nomi.gg, 441 games, within noise)", PatchNotes.TribeSince(Guide("B", 1, 0, new[] { "X" }, tribe: 20), Nomi()));
        Assert.Equal("Mech ▼ 3.96 → 4.89 since 36.6.3 (nomi.gg, 575 games)", PatchNotes.TribeSince(Guide("M", 1, 0, new[] { "X" }, tribe: 17), Nomi()));
        Assert.Null(PatchNotes.TribeSince(Guide("N", 1, 0, new[] { "X" }, tribe: 92), Nomi())); // no games either side
        Assert.Null(PatchNotes.TribeSince(Guide("Menagerie", 1, 0, new[] { "X" }, tribe: 0), Nomi())); // no main tribe
        Assert.Null(PatchNotes.TribeSince(Guide("U", 1, 0, new[] { "X" }, tribe: 11), null));
        var noGamesBefore = Nomi(253216, new NomiTribe("demon", 0, 4.0, 50, 3.9));
        Assert.Null(PatchNotes.TribeSince(Guide("D", 1, 0, new[] { "X" }, tribe: 15), noGamesBefore)); // an average over no game is no figure
    }

    /// <summary>Component 6, the log first (decision 9): the tier reached, at which turn, against nomi.gg's medians.</summary>
    [Fact]
    public void TierPace_TheTierReachedAgainstTheMedians()
    {
        Assert.Equal("tier pace tier=5 turn=9 · nomi.gg medians: top4 8 (99%), first 7 (100%), high top4 7.5 (100%)", PatchNotes.TierPace(5, 9, Nomi()));
        Assert.Equal("tier pace tier=3 turn=4 · nomi.gg medians: none for tier 3", PatchNotes.TierPace(3, 4, Nomi()));
        Assert.Equal("tier pace tier=5 turn=9 · nomi.gg not loaded", PatchNotes.TierPace(5, 9, null));
    }

    /// <summary>Component 5, the log first: the game's build against the build of nomi.gg's patch.</summary>
    [Theory]
    [InlineData(253216, "same build")]
    [InlineData(253500, "game newer")]
    [InlineData(253000, "data newer")]
    public void Freshness_TheGamesBuildAgainstTheData(int game, string verdict)
    {
        Assert.Equal($"freshness game build={game} nomi.gg build=253216 (patch 36.6.3): {verdict}", PatchNotes.Freshness(game, Nomi()));
    }

    [Fact]
    public void Freshness_WhatIsNotKnown_IsSaid()
    {
        Assert.Equal("freshness game build=unknown nomi.gg build=253216 (patch 36.6.3): unknown", PatchNotes.Freshness(null, Nomi()));
        Assert.Equal("freshness game build=253216 nomi.gg build=unknown (patch 36.6.3): unknown", PatchNotes.Freshness(253216, Nomi(build: null)));
        Assert.Equal("freshness game build=253216 nomi.gg not loaded", PatchNotes.Freshness(253216, null));
    }
}
