namespace TavernCompass.NoDance.Core.Tests;

/// <summary>
/// Where the installed client is: the HEARTHSTONE_MANAGED variable names its Hearthstone_Data/Managed folder. The
/// tests only read Assembly-CSharp.dll's metadata; nothing of the game is loaded, run, or copied into the repository.
/// </summary>
internal static class GameAssembly
{
    public const string Variable = "HEARTHSTONE_MANAGED";

    public const string SkipReason =
        "HEARTHSTONE_MANAGED is not set: set it to the game's Hearthstone_Data/Managed folder to check the Harmony "
        + "targets against the installed client (e.g. HEARTHSTONE_MANAGED=/mnt/e/JEUX/Hearthstone/Hearthstone_Data/Managed)";

    public static string? ManagedDirectory
    {
        get
        {
            string? value = Environment.GetEnvironmentVariable(Variable);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    /// <summary>The client's Assembly-CSharp.dll; set but missing is an error, not a skip.</summary>
    public static string Path => System.IO.Path.Combine(
        ManagedDirectory ?? throw new InvalidOperationException(SkipReason), "Assembly-CSharp.dll");

    /// <summary>The repository's root, found from the test's output directory.</summary>
    public static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(System.IO.Path.Combine(dir.FullName, "BronzebeardHud.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("BronzebeardHud.sln not found above " + AppContext.BaseDirectory);
    }

    /// <summary>The mod's DLL as built by its Release (or Debug) build, if it was built.</summary>
    public static string? ModDll()
    {
        string bin = System.IO.Path.Combine(RepositoryRoot(), "mods", "TavernCompass.NoDance", "bin");
        foreach (string configuration in new[] { "Release", "Debug" })
        {
            string path = System.IO.Path.Combine(bin, configuration, "net48", "TavernCompass.NoDance.dll");
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}

/// <summary>A test that reads the installed client: skipped, with the reason, when HEARTHSTONE_MANAGED is not set.</summary>
public sealed class GameFactAttribute : FactAttribute
{
    public GameFactAttribute()
    {
        if (GameAssembly.ManagedDirectory == null)
        {
            Skip = GameAssembly.SkipReason;
        }
    }
}

/// <summary>A theory that reads the installed client: skipped, with the reason, when HEARTHSTONE_MANAGED is not set.</summary>
public sealed class GameTheoryAttribute : TheoryAttribute
{
    public GameTheoryAttribute()
    {
        if (GameAssembly.ManagedDirectory == null)
        {
            Skip = GameAssembly.SkipReason;
        }
    }
}

/// <summary>A test that reads both the installed client and the mod's built DLL: skipped, with the reason, without them.</summary>
public sealed class GameAndModFactAttribute : FactAttribute
{
    public GameAndModFactAttribute()
    {
        if (GameAssembly.ManagedDirectory == null)
        {
            Skip = GameAssembly.SkipReason;
        }
        else if (GameAssembly.ModDll() == null)
        {
            Skip = "the mod is not built: dotnet build mods/TavernCompass.NoDance -c Release -p:HearthstoneManagedDir=$HEARTHSTONE_MANAGED/";
        }
    }
}
