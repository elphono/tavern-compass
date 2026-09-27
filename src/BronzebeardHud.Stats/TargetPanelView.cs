namespace BronzebeardHud.Stats;

/// <summary>
/// What the target composition panel shows: the list of compositions, or one composition's detail in its place
/// (Ali, 2026-09-27). A click on a composition's line (its name or any of its cards) opens that composition's
/// detail, and nothing else: the "how top boards field it" view lives in its own panel, opened from Bob's
/// cards. In the detail, a card click does nothing (hover still shows the card); "← back" returns to the list.
/// </summary>
public sealed class TargetPanelView
{
    /// <summary>The composition whose detail replaces the list; null while the list shows.</summary>
    public string? DetailId { get; private set; }

    public bool ShowsDetail => DetailId != null;

    /// <summary>A click on a composition's line in the list: its detail replaces the list. Ignored in the detail.</summary>
    /// <returns>True when the detail of <paramref name="compositionId"/> was opened.</returns>
    public bool LineClicked(string compositionId)
    {
        if (ShowsDetail)
        {
            return false;
        }

        DetailId = compositionId;
        return true;
    }

    /// <summary>"← back", or the detail could not be computed: the list again.</summary>
    public void Back() => DetailId = null;
}
