namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// Where the popup of a hovered guide line goes (GuidePopupLayout.Place): above the "Compositions" panel, right of the
/// boards, never on a zone of the game (NoGoZones, the tests' own definition, not the code's), never on the panel, on
/// what it is told to avoid (the Skip combat button) or where a card preview of the panel can show; below the panel when
/// there is no room above; nothing when there is room on neither side.
/// </summary>
public class GuidePopupLayoutTests
{
    private static readonly (double Width, double Height)[] Sizes =
    {
        (1920, 1080), (1600, 900), (1440, 1080), (2560, 1080), (2291, 1360), (1280, 720), (3840, 2160), (1600, 1200),
    };

    private static double PopupWidth(double height) => GuidePopupLayout.Width * height / 1080;

    private static LayoutRect At(double left, double top, double width, double height) => new(left + width / 2, top + height / 2, width, height);

    private static double Bottom(LayoutRect r) => r.Top + r.Height;

    /// <summary>The panel at its default place, and moved: to each corner, the middle of each side and the centre.</summary>
    private static IEnumerable<(string Name, LayoutRect Panel)> Panels(double width, double height)
    {
        var standard = TavernLayout.TargetPanel(width, height);
        var w = standard.Width;
        var h = standard.Height;
        var m = 0.01 * height;
        yield return ("default", standard);
        yield return ("top right", At(width - m - w, m, w, h));
        yield return ("top left", At(m, m, w, h));
        yield return ("bottom left", At(m, height - m - h, w, h));
        yield return ("middle left", At(m, (height - h) / 2, w, h));
        yield return ("middle right", At(width - m - w, (height - h) / 2, w, h));
        yield return ("top middle", At((width - w) / 2, m, w, h));
        yield return ("tall right", At(width - m - w, height * 0.3, w, height * 0.6));
    }

    private static void AssertClear(LayoutRect popup, LayoutRect panel, double width, double height, IReadOnlyList<LayoutRect> avoid, string what)
    {
        foreach (var (name, zone) in NoGoZones.For(width, height))
        {
            Assert.False(NoGoZones.Overlaps(popup, zone), $"{what}: the popup covers the {name}");
        }

        Assert.False(NoGoZones.Overlaps(popup, panel), $"{what}: the popup covers the panel");
        foreach (var other in avoid)
        {
            Assert.False(NoGoZones.Overlaps(popup, other), $"{what}: the popup covers an element it must avoid");
        }

        Assert.True(popup.Left >= -1e-9 && popup.Top >= -1e-9 && popup.Right <= width + 1e-9 && Bottom(popup) <= height + 1e-9, $"{what}: outside the overlay");
        Assert.Equal(PopupWidth(height), popup.Width, precision: 6);
    }

    [Fact]
    public void Default1080p_GoesAboveThePanel_InTheColumnRightOfTheBoards_WholeWhenItFits()
    {
        var panel = TavernLayout.TargetPanel(1920, 1080);

        var popup = GuidePopupLayout.Place(panel, 1920, 1080, PopupWidth(1080), 600);

        Assert.NotNull(popup);
        var rect = popup!.Value;
        Assert.True(Bottom(rect) <= panel.Top, $"bottom {Bottom(rect):0.#} below the panel's top {panel.Top:0.#}");
        Assert.True(rect.Left >= 1443, $"left {rect.Left:0.#}: on the boards (they end at x = 1443)");
        Assert.Equal(600, rect.Height, precision: 6);
        AssertClear(rect, panel, 1920, 1080, Array.Empty<LayoutRect>(), "default");
    }

    /// <summary>A popup short enough to fit right above the panel goes right above it, a gap away, not higher up.</summary>
    [Fact]
    public void AShortPopup_SitsRightAboveThePanel()
    {
        var panel = TavernLayout.TargetPanel(1920, 1080);

        var rect = GuidePopupLayout.Place(panel, 1920, 1080, PopupWidth(1080), 120)!.Value;

        Assert.Equal(panel.Top - GuidePopupLayout.Gap * 1080, Bottom(rect), precision: 6);
    }

    [Fact]
    public void SkipCombat_IsNeverCovered_ThePopupGoesAboveIt()
    {
        var panel = TavernLayout.TargetPanel(1920, 1080);
        var skip = SkipCombatLayout.Button(1920, 1080);

        var rect = GuidePopupLayout.Place(panel, 1920, 1080, PopupWidth(1080), 600, minHeight: 150, avoid: new[] { skip })!.Value;

        Assert.False(NoGoZones.Overlaps(rect, skip), "covers Skip combat");
        Assert.True(Bottom(rect) <= skip.Top, $"bottom {Bottom(rect):0.#}, Skip combat's top {skip.Top:0.#}");
        Assert.True(rect.Height >= 150);
        AssertClear(rect, panel, 1920, 1080, new[] { skip }, "skip combat");
    }

    /// <summary>The panel moved to the top of the window leaves no room above it: the popup goes below, its right edge on the panel's.</summary>
    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(2291, 1360)]
    [InlineData(2560, 1080)]
    public void PanelAtTheTop_ThePopupGoesBelow_AlignedOnItsRightEdge(double width, double height)
    {
        var standard = TavernLayout.TargetPanel(width, height);
        var panel = At(width - 0.01 * height - standard.Width, 0.01 * height, standard.Width, standard.Height);

        var popup = GuidePopupLayout.Place(panel, width, height, PopupWidth(height), 500 * height / 1080);

        Assert.NotNull(popup);
        Assert.True(popup!.Value.Top >= Bottom(panel), $"top {popup.Value.Top:0.#}, panel's bottom {Bottom(panel):0.#}");
        Assert.Equal(panel.Right, popup.Value.Right, precision: 6);
        AssertClear(popup.Value, panel, width, height, Array.Empty<LayoutRect>(), "top");
    }

    [Fact]
    public void NoRoomAboveNorBelow_GivesNothing()
    {
        // A panel as tall as the window: nothing above it, nothing below it.
        var full = At(1920 - 10 - 488, 0, 488, 1080);
        Assert.Null(GuidePopupLayout.Place(full, 1920, 1080, PopupWidth(1080), 400));

        // Room on both sides, but less than the least the popup can show.
        var panel = TavernLayout.TargetPanel(1920, 1080);
        Assert.Null(GuidePopupLayout.Place(panel, 1920, 1080, PopupWidth(1080), 5000, minHeight: 1500));
    }

    /// <summary>
    /// A window narrower than the popup gives nothing, never a popup sticking out of it. The same window has room for a
    /// narrower popup (above the boards, the panel at the bottom): the refusal is the width's, not the lack of room.
    /// </summary>
    [Fact]
    public void AWindowNarrowerThanThePopup_GivesNothing_ThoughANarrowerOneFits()
    {
        var panel = At(10, 900, 300, 150);

        Assert.NotNull(GuidePopupLayout.Place(panel, 350, 1080, 300, 200));
        Assert.Null(GuidePopupLayout.Place(panel, 350, 1080, PopupWidth(1080), 200));
    }

    /// <summary>When the whole popup does not fit, the room there is, never less than the least it can show.</summary>
    [Fact]
    public void ATallPopup_GetsTheRoomThereIs_NotLessThanItsMinimum()
    {
        var panel = TavernLayout.TargetPanel(1920, 1080);

        var rect = GuidePopupLayout.Place(panel, 1920, 1080, PopupWidth(1080), 3000, minHeight: 200)!.Value;

        Assert.True(rect.Height < 3000 && rect.Height >= 200, $"height {rect.Height:0.#}");
        Assert.True(rect.Top >= GuidePopupLayout.Margin * 1080 - 1e-9, $"top {rect.Top:0.#} in the margin");
    }

    /// <summary>
    /// Every window size, every place of the panel, popups from tiny to taller than the window, with and without the Skip
    /// combat button to avoid: a popup is either refused or clear of every zone, the panel, the button and the card
    /// previews, inside the overlay, as wide as asked, as tall as asked or as the room, never under its minimum. The sweep
    /// must place most of them, or it proves nothing.
    /// </summary>
    [Fact]
    public void Sweep_APlacedPopupIsClearOfEverything_AndMostArePlaced()
    {
        var placed = 0;
        var asked = 0;
        foreach (var (width, height) in Sizes)
        {
            foreach (var (name, panel) in Panels(width, height))
            {
                foreach (var withSkip in new[] { false, true })
                {
                    var avoid = withSkip ? new[] { SkipCombatLayout.Button(width, height) } : Array.Empty<LayoutRect>();
                    foreach (var designHeight in new[] { 60.0, 150, 300, 450, 600, 800, 1200 })
                    {
                        var popupHeight = designHeight * height / 1080;
                        var minimum = Math.Min(popupHeight, 120 * height / 1080);
                        var what = $"{width}x{height}, panel {name}, skip {withSkip}, popup {designHeight}";
                        asked++;
                        var popup = GuidePopupLayout.Place(panel, width, height, PopupWidth(height), popupHeight, minimum, avoid);
                        if (popup is not { } rect)
                        {
                            continue;
                        }

                        placed++;
                        AssertClear(rect, panel, width, height, avoid, what);
                        Assert.True(rect.Height <= popupHeight + 1e-6 && rect.Height >= minimum - 1e-6, $"{what}: height {rect.Height:0.#}");
                        Assert.True(Bottom(rect) <= panel.Top + 1e-6 || rect.Top >= Bottom(panel) - 1e-6, $"{what}: neither above nor below the panel");
                        Assert.False(NoGoZones.Overlaps(rect, GuidePopupLayout.PreviewArea(panel, width, height)), $"{what}: on a card preview's place");
                    }
                }
            }
        }

        Assert.True(placed >= asked * 3 / 4, $"only {placed} of {asked} popups placed");
    }

    /// <summary>The area the popup keeps clear holds the preview of every vignette the panel can have, wherever the panel is.</summary>
    [Fact]
    public void PreviewArea_HoldsThePreviewOfEveryVignetteOfThePanel()
    {
        foreach (var (width, height) in Sizes)
        {
            foreach (var (name, panel) in Panels(width, height))
            {
                var area = GuidePopupLayout.PreviewArea(panel, width, height);
                for (var fx = 0.0; fx <= 1.0; fx += 0.125)
                {
                    for (var fy = 0.0; fy <= 1.0; fy += 0.125)
                    {
                        var vignette = new LayoutRect(panel.Left + fx * panel.Width, panel.Top + fy * panel.Height, 54 * height / 1080, 65 * height / 1080);
                        var (_, preview) = TavernLayout.PreviewRect(panel, vignette, width, height);
                        Assert.True(preview.Left >= area.Left - 1e-6 && preview.Right <= area.Right + 1e-6 && preview.Top >= area.Top - 1e-6 && Bottom(preview) <= Bottom(area) + 1e-6,
                            $"{width}x{height}, panel {name}, vignette at ({fx}, {fy}): preview outside the area");
                    }
                }
            }
        }
    }

    /// <summary>The code's own zones are the tests' ones: two definitions of the same HDT constants must not drift apart.</summary>
    [Fact]
    public void GameZones_AreTheTestsNoGoZones()
    {
        foreach (var (width, height) in Sizes)
        {
            var mine = GuidePopupLayout.GameZones(width, height);
            var reference = NoGoZones.For(width, height).Select(z => z.Rect).ToList();
            Assert.Equal(reference.Count, mine.Count);
            for (var i = 0; i < mine.Count; i++)
            {
                Assert.Equal(reference[i].Left, mine[i].Left, precision: 6);
                Assert.Equal(reference[i].Top, mine[i].Top, precision: 6);
                Assert.Equal(reference[i].Width, mine[i].Width, precision: 6);
                Assert.Equal(reference[i].Height, mine[i].Height, precision: 6);
            }
        }
    }
}
