namespace BronzebeardHud.Stats.Tests;

public class TavernRowTests
{
    /// <summary>Round 14 of Ali's 20:27 game (Power.log 2026-09-26): six minions at 1-6, the tavern spell at 7, listed out of order.</summary>
    private static readonly TavernSlot[] AliRound14 =
    {
        new(9007, "BG28_573", isMinion: false, zonePosition: 7),
        new(9003, "BG36_318", isMinion: true, zonePosition: 3),
        new(9001, "BG_BOT_911", isMinion: true, zonePosition: 1),
        new(9006, "BG36_114", isMinion: true, zonePosition: 6),
        new(9002, "BG34_170t", isMinion: true, zonePosition: 2),
        new(9005, "BG25_016", isMinion: true, zonePosition: 5),
        new(9004, "BG36_110", isMinion: true, zonePosition: 4),
        new(9100, "BG36_999", isMinion: true, zonePosition: 0), // leaving the row: no position, no slot
    };

    [Fact]
    public void AliRound14_SevenSlots_TheSpellLast_MarkersHalfACardLeftOfTheMinionOnlyLayout()
    {
        var row = TavernRow.Arrange(AliRound14);
        Assert.Equal(new[] { 9001, 9002, 9003, 9004, 9005, 9006, 9007 }, row.Select(s => s.EntityId));
        Assert.False(row[6].IsMinion);

        // HDT's shop constants (TavernLayout): centre x = W/2 + (i + 0.5 − n/2) × 138 × H/1080, whatever W/H.
        const double width = 2290, height = 1359;
        var s = height / 1080;
        var slots = TavernLayout.CardSlots(width, height, row.Count);
        var expected = Enumerable.Range(0, 7).Select(i => width / 2 + (i - 3) * 138 * s).ToList();
        Assert.Equal(expected.Select(x => Math.Round(x, 6)), slots.Select(r => Math.Round(r.CenterX, 6)));
        Assert.Equal(width / 2, slots[3].CenterX, precision: 6); // the fourth card, not a gap, is on the axis

        // What the plugin logged at 20:52:02 ("tavern=6 … first=#2 x=976"): six slots for six minions.
        var logged = TavernLayout.Markers(width, height, 6)[2];
        var fixedMarker = TavernLayout.Markers(width, height, 7)[2];
        Assert.Equal(976, Math.Round(logged.Left));
        Assert.Equal(889, Math.Round(fixedMarker.Left));
        Assert.Equal(-138 * s / 2, fixedMarker.Left - logged.Left, precision: 6); // half a card: −87 px
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(2290, 1359)]
    [InlineData(2560, 1080)]
    [InlineData(1600, 1200)]
    public void ThreeToSevenSlots_CentredOnTheWindowAxis_EquallySpaced_WhateverTheRatio(double width, double height)
    {
        var pitch = 138 * height / 1080;
        foreach (var count in new[] { 3, 4, 5, 6, 7 })
        {
            var slots = TavernLayout.CardSlots(width, height, count);
            Assert.Equal(count, slots.Count);
            Assert.Equal(width, slots[0].CenterX + slots[count - 1].CenterX, precision: 6);
            for (var i = 1; i < count; i++)
            {
                Assert.Equal(pitch, slots[i].CenterX - slots[i - 1].CenterX, precision: 6);
            }

            Assert.True(slots[0].Left >= 0 && slots[count - 1].Right <= width, $"{width}×{height}, {count} slots: off the window");
        }
    }

    [Fact]
    public void ThreeRounds_PurchasesRerollsFreezeAndAddedCards_RedrawOnEveryChange_CountRerolls()
    {
        var tracker = new TavernRowTracker();

        // Round 3: the shop fills, a purchase, then a reroll seen through an empty row.
        Assert.True(tracker.Observe(3, new[] { 11, 12, 13, 14 }));
        Assert.False(tracker.Observe(3, new[] { 11, 12, 13, 14 }));
        Assert.True(tracker.Observe(3, new[] { 11, 13, 14 }));            // bought 12
        Assert.True(tracker.Observe(3, Array.Empty<int>()));
        Assert.True(tracker.Observe(3, new[] { 21, 22, 23, 24 }));        // reroll
        Assert.Equal((3, 3, 1), (tracker.Round, tracker.Changes, tracker.Refreshes));
        tracker.Close();
        Assert.False(tracker.IsOpen);

        // Round 4: the shop was frozen, so it starts with the same cards; a purchase; an effect adds a card.
        Assert.True(tracker.Observe(4, new[] { 21, 22, 23, 24 }));
        Assert.True(tracker.Observe(4, new[] { 22, 23, 24 }));            // bought 21
        Assert.True(tracker.Observe(4, new[] { 22, 23, 24, 25 }));        // added, not rerolled
        Assert.Equal((4, 2, 0), (tracker.Round, tracker.Changes, tracker.Refreshes));

        // Round 5: the first cards arrive one by one, then two rerolls straight from one row to the next.
        Assert.True(tracker.Observe(5, new[] { 31 }));
        Assert.True(tracker.Observe(5, new[] { 31, 32, 33, 34 }));
        Assert.True(tracker.Observe(5, new[] { 41, 42, 43, 44 }));
        Assert.True(tracker.Observe(5, new[] { 51, 52, 53, 44 }));        // the spell kept (frozen alone)
        Assert.Equal((5, 3, 2), (tracker.Round, tracker.Changes, tracker.Refreshes));
    }
}
