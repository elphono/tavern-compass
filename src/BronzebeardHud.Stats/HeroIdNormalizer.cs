using System.Text.RegularExpressions;

namespace BronzebeardHud.Stats;

/// <summary>
/// Maps a hero card id as it appears in game (possibly a skin, or a transformed token) to the base
/// hero id that stats are keyed by. Mirrors Firestone's normalizeHeroCardId (reference-data,
/// bgs-utils): the card's parent first, then the <c>_SKIN_</c> naming pattern, then two known tokens.
/// </summary>
public static class HeroIdNormalizer
{
    private static readonly Regex SkinPattern = new("^(.*)_SKIN_.*$", RegexOptions.CultureInvariant);

    /// <param name="cardId">Card id of the hero entity.</param>
    /// <param name="parentCardId">
    /// Card id of the parent hero when the card data declares one (HDT: the card's
    /// BACON_SKIN_PARENT_ID dbfId, resolved through HearthDb). Null when unknown.
    /// </param>
    public static string Normalize(string cardId, string? parentCardId = null)
    {
        var afterSkin = !string.IsNullOrEmpty(parentCardId)
            ? parentCardId!
            : SkinPattern.Match(cardId) is { Success: true } match
                ? match.Groups[1].Value
                : cardId;

        return afterSkin switch
        {
            "TB_BaconShop_HERO_59t" => "TB_BaconShop_HERO_59",
            "BG22_HERO_007t" => "BG22_HERO_007",
            _ => afterSkin,
        };
    }
}
