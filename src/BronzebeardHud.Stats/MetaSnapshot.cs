namespace BronzebeardHud.Stats;

/// <summary>
/// The Battlegrounds meta page the "Meta" button of the target panel opens in the browser: Firestone's
/// composition tier list. Firestone's web app serves it without an account (route
/// libs/shared/web-shell/src/lib/routes.ts:14 of Zero-to-Heroes/firestone; the page answers 200), whereas
/// hsreplay.net's Battlegrounds pages answer 403 to anything but a browser that passes its challenge
/// (checked 2026-09-26). The route takes no region, season or MMR parameter — those are filters inside the
/// page — so there is nothing to build: a constant.
/// </summary>
public static class MetaSnapshot
{
    public const string Url = "https://www.firestoneapp.com/battlegrounds/comps";
}
