using System.Text.RegularExpressions;

namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// HDT puts a clickable in its list when it is declared and again at its Loaded, and takes it out once at its Unloaded
/// (issue #17): an element declared before it is loaded leaves a dead entry in HDT's list every time it leaves the overlay.
/// The plugin therefore declares its clickables through OverlayClickable.Declare, which waits for the element's Loaded, and
/// nowhere else; PanelMover alone switches the property itself, on and off with the move mode, never to a literal true.
/// Reads the plugin's sources, which the test project does not compile (the plugin targets net48 and HDT); the simulation's
/// self-test measures the list itself ("mouse: three more redraws of the panel…").
/// </summary>
public class OverlayClickableSourceTests
{
    private static readonly Regex Declaration = new(@"SetIsOverlayHitTestVisible\s*\(");
    private static readonly Regex DeclaredTrue = new(@"SetIsOverlayHitTestVisible\s*\([^;]*,\s*true\s*\)");

    [Fact]
    public void PluginSources_DeclareClickablesOnlyThroughOverlayClickable()
    {
        var calls = new List<(string File, string Where)>();
        foreach (var file in Directory.GetFiles(Path.Combine(RepositoryRoot(), "src", "BronzebeardHud.HdtPlugin"), "*.cs"))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (Declaration.IsMatch(lines[i]))
                {
                    calls.Add((Path.GetFileName(file), $"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}"));
                }
            }
        }

        // The pattern does find calls: the helper's own, and PanelMover's switches.
        Assert.Contains(calls, c => c.File == "OverlayClickable.cs");
        Assert.Contains(calls, c => c.File == "PanelMover.cs");
        Assert.All(calls, c => Assert.True(c.File is "OverlayClickable.cs" or "PanelMover.cs",
            $"{c.Where}: declare a clickable with OverlayClickable.Declare, once it is loaded (issue #17)"));
        Assert.All(calls.Where(c => c.File == "PanelMover.cs"), c => Assert.False(DeclaredTrue.IsMatch(c.Where), c.Where));
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BronzebeardHud.sln")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException($"BronzebeardHud.sln not found above {AppContext.BaseDirectory}");
    }
}
