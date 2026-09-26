namespace BronzebeardHud.Stats.Tests;

public class ChoiceOrderTests
{
    private static EntitySnapshot Offer(int id, string cardId) =>
        new(id, cardId, isHero: false, new Dictionary<string, int> { ["BACON_TRINKET"] = 1, ["ZONE"] = 6 });

    /// <summary>What HDT's Player.OfferedEntities hands over: the game's entities in creation order.</summary>
    private static EntitySnapshot[] InCreationOrder(params (int Id, string CardId)[] entities) =>
        entities.OrderBy(e => e.Id).Select(e => Offer(e.Id, e.CardId)).ToArray();

    [Fact]
    public void AliGame_ThreeChoices_EachInTheOrderOfTheLog_NotInCreationOrder()
    {
        // Power.log 2026-09-26, 20:04:29: lesser trinket choice, created 3434..3437, offered 3436, 3434, 3437, 3435.
        var lesser = InCreationOrder((3434, "BG30_MagicItem_426"), (3435, "BG32_MagicItem_893"), (3436, "BG30_MagicItem_706"), (3437, "BG30_MagicItem_703"));
        var shown = ChoiceOrder.Arrange(new[] { 3436, 3434, 3437, 3435 }, lesser);
        Assert.Equal(new[] { 3436, 3434, 3437, 3435 }, shown.Select(e => e.Id));
        Assert.Equal(new[] { "BG30_MagicItem_706", "BG30_MagicItem_426", "BG30_MagicItem_703", "BG32_MagicItem_893" }, shown.Select(e => e.CardId));

        // 20:10:50: greater trinket choice, created 8943..8946, offered 8945, 8943, 8944, 8946.
        var greater = InCreationOrder((8943, "BG30_MagicItem_993"), (8944, "BG30_MagicItem_418"), (8945, "BG32_MagicItem_925"), (8946, "BG30_MagicItem_420t"));
        Assert.Equal(new[] { "BG32_MagicItem_925", "BG30_MagicItem_993", "BG30_MagicItem_418", "BG30_MagicItem_420t" },
            ChoiceOrder.Arrange(new[] { 8945, 8943, 8944, 8946 }, greater).Select(e => e.CardId));

        // 20:01:09: Dark Gift discover, offered in creation order: unchanged.
        var darkGift = InCreationOrder((1283, "BG26_174"), (1284, "BG36_116"), (1285, "BG29_300"));
        Assert.Equal(new[] { 1283, 1284, 1285 }, ChoiceOrder.Arrange(new[] { 1283, 1284, 1285 }, darkGift).Select(e => e.Id));
        Assert.Equal("[3436,3434,3437,3435]", ChoiceOrder.Describe(shown));
    }

    [Fact]
    public void AnOfferedIdWithoutItsEntity_GivesNothing_RatherThanAShiftedRow()
    {
        var partial = InCreationOrder((3434, "BG30_MagicItem_426"), (3436, "BG30_MagicItem_706"), (3437, "BG30_MagicItem_703"));
        Assert.Empty(ChoiceOrder.Arrange(new[] { 3436, 3434, 3437, 3435 }, partial));
        Assert.Empty(ChoiceOrder.Arrange(Array.Empty<int>(), partial)); // no pending choice
    }
}
