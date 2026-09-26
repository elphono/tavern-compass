namespace BronzebeardHud.Stats.Tests;

public class OfferedHeroesTests
{
    private static EntitySnapshot Hero(int id, string cardId, int position, string? parent = null, params (string Tag, int Value)[] tags) =>
        Entity(id, cardId, isHero: true, position, parent, tags);

    private static EntitySnapshot Entity(int id, string cardId, bool isHero, int position, string? parent, (string Tag, int Value)[] tags)
    {
        var all = tags.ToDictionary(t => t.Tag, t => t.Value);
        all["ZONE_POSITION"] = position;
        return new EntitySnapshot(id, cardId, isHero, all, parent);
    }

    [Fact]
    public void Select_KeepsOfferedHeroesInScreenOrder_WithBaseIds()
    {
        var entities = new[]
        {
            Hero(11, "TB_BaconShop_HERO_39", 3, null, ("BACON_HERO_CAN_BE_DRAFTED", 1)),
            Hero(12, "BG22_HERO_004_SKIN_B", 1, null, ("BACON_SKIN", 1)),
            // The card data names the parent: it wins over the id pattern.
            Hero(13, "TB_BaconShopSkin_Custom", 2, "BG31_HERO_802", ("BACON_SKIN", 1)),
            Hero(14, "BG20_HERO_101", 5, null, ("BACON_HERO_CAN_BE_DRAFTED", 1), ("BACON_LOCKED_MULLIGAN_HERO", 1)),
            Entity(15, "BG28_504", isHero: false, 6, null, new[] { ("BACON_HERO_CAN_BE_DRAFTED", 1) }),
            Hero(16, "TB_BaconShop_HERO_16", 7),
            Hero(17, "BG25_HERO_103", 8, null, ("BACON_HERO_CAN_BE_DRAFTED", 0)),
            Hero(18, "TB_BaconShop_HERO_59t", 4, null, ("BACON_HERO_CAN_BE_DRAFTED", 1)),
        };

        var offered = OfferedHeroes.Select(entities);

        Assert.Equal(new[] { 12, 13, 11, 18 }, offered.Select(h => h.EntityId));
        Assert.Equal(new[] { 1, 2, 3, 4 }, offered.Select(h => h.Position));
        Assert.Equal(
            new[] { "BG22_HERO_004_SKIN_B", "TB_BaconShopSkin_Custom", "TB_BaconShop_HERO_39", "TB_BaconShop_HERO_59t" },
            offered.Select(h => h.CardId));
        Assert.Equal(
            new[] { "BG22_HERO_004", "BG31_HERO_802", "TB_BaconShop_HERO_39", "TB_BaconShop_HERO_59" },
            offered.Select(h => h.BaseCardId));
    }

    [Fact]
    public void Select_NothingOffered_IsEmpty()
    {
        Assert.Empty(OfferedHeroes.Select(new[] { Hero(21, "TB_BaconShop_HERO_16", 1) }));
    }

    [Theory]
    [InlineData("TB_BaconShop_HERO_39", null, "TB_BaconShop_HERO_39")]
    [InlineData("BG22_HERO_004_SKIN_B", null, "BG22_HERO_004")]
    [InlineData("BG22_HERO_004_SKIN_B", "BG31_HERO_802", "BG31_HERO_802")]
    [InlineData("TB_BaconShop_HERO_59t", null, "TB_BaconShop_HERO_59")]
    [InlineData("BG22_HERO_007t", null, "BG22_HERO_007")]
    [InlineData("BG22_HERO_007t_SKIN_A", null, "BG22_HERO_007")]
    public void Normalize_MapsSkinsAndTokensToTheBaseHero(string cardId, string? parent, string expected)
    {
        Assert.Equal(expected, HeroIdNormalizer.Normalize(cardId, parent));
    }
}
