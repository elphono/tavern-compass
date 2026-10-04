namespace BronzebeardHud.Stats.Tests;

/// <summary>Synthetic comp guides for the tests: no real HSReplay data, every card id made up.</summary>
internal static class GuideTestData
{
    public static CompGuide Guide(string name, int tier, int tierRank, string[] core, string[]? addons = null, string[]? enablers = null, int tribe = 0) =>
        new(name, tier, tierRank, core, addons ?? Array.Empty<string>(), enablers ?? Array.Empty<string>(), Array.Empty<string>(), primaryTribe: tribe);

    /// <summary>The guides grouped by tier, ascending, each tier in the order given (as the parser leaves them).</summary>
    public static CompGuideSet Set(params CompGuide[] guides) =>
        new(CompGuideSources.HdtFree, guides.GroupBy(g => g.Tier).OrderBy(g => g.Key).Select(g => new CompGuideTier(g.Key, g.ToList())).ToList());

    public static PlayerCards Board(params string[] ids) => new(ids.Select(id => new OwnedCard(id)).ToList(), Array.Empty<OwnedCard>());

    public static OwnedCard[] Owned(params string[] ids) => ids.Select(id => new OwnedCard(id)).ToArray();

    /// <summary>The targets a fresh tracker gives for these cards: <paramref name="ticked"/> first, then the most probable.</summary>
    public static IReadOnlyList<CompTarget> Targets(CompGuideSet set, PlayerCards cards, int count, params CompGuide[] ticked)
    {
        var tracker = new CompTargetTracker();
        foreach (var guide in ticked)
        {
            Assert.True(tracker.Toggle(guide.Id));
        }

        return tracker.Next(CompGuideMatch.Rank(set, cards), count);
    }
}
