using System.Text.RegularExpressions;

namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// The repository is public: no real BattleTag, opponent name or account number may be committed. The ones that had
/// slipped into old test fixtures were purged from the history on 2026-10-06; tests and docs use made-up names.
/// Nor any real stats file: Firestone and nomi.gg agreed to our reading their data, not to our republishing it; tests use
/// synthetic data.
/// </summary>
public class RepositoryHygieneTests
{
    // Written in pieces so that this file does not contain what it forbids.
    private static readonly string[] Purged =
    {
        "elphono" + "#2437",
        "BehEh" + "#1355",
        "Voleur" + "Rapide",
        "lo=" + "17113366",
        "lo=" + "17412774",
    };

    /// <summary>A BattleTag: a name, #, four or five digits.</summary>
    private static readonly Regex BattleTag = new(@"(?<![\p{L}\p{N}_])[\p{L}\p{N}_]{3,24}#\d{4,5}\b", RegexOptions.Compiled);

    /// <summary>A Blizzard account number: "lo" is the account, and only the zero of the made-up ones is allowed.</summary>
    private static readonly Regex Account = new(@"GameAccountId=\[hi=\d+ lo=([1-9]\d*)\]", RegexOptions.Compiled);

    /// <summary>The made-up players of the tests of the leaderboard (Lord of the Rings names, not real accounts).</summary>
    private static readonly string[] MadeUp = { "Tauriel#2981", "TAURIEL#2981", "Zed#1100" };

    private static readonly string[] TextExtensions =
    {
        ".cs", ".md", ".html", ".txt", ".json", ".yml", ".yaml", ".csproj", ".props", ".sh", ".py", ".sln", ".editorconfig",
    };

    /// <summary>A JSON file this size is a downloaded stats file, not a fixture: the largest fixture is 22 KB.</summary>
    private const long MaxJsonBytes = 100_000;

    /// <summary>Fields only a server's file carries: Firestone's update date, nomi.gg's high-MMR player count.</summary>
    private static readonly string[] ServerSignatures = { "\"" + "lastUpdate" + "Date\"", "\"" + "high" + "Players\"" };

    private static readonly string[] Skipped = { "bin", "obj", "lib", ".git", ".claude", "node_modules" };

    /// <summary>Why a file looks like real stats data: a compressed download, an outsized JSON, or a server's own fields.</summary>
    internal static IReadOnlyList<string> DataProblems(string relativePath, long bytes, string jsonText)
    {
        var found = new List<string>();
        if (relativePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) || relativePath.EndsWith(".gz.json", StringComparison.OrdinalIgnoreCase))
        {
            found.Add($"{relativePath}: a downloaded stats file (.gz)");
        }

        if (relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            if (bytes > MaxJsonBytes)
            {
                found.Add($"{relativePath}: {bytes} bytes, larger than any fixture");
            }

            found.AddRange(ServerSignatures.Where(s => jsonText.Contains(s, StringComparison.Ordinal)).Select(s => $"{relativePath}: carries {s}"));
        }

        return found;
    }

    internal static IReadOnlyList<string> Problems(string text)
    {
        var found = new List<string>();
        foreach (var value in Purged)
        {
            if (text.Contains(value, StringComparison.Ordinal))
            {
                found.Add(value);
            }
        }

        foreach (Match tag in BattleTag.Matches(text))
        {
            if (!MadeUp.Contains(tag.Value))
            {
                found.Add(tag.Value);
            }
        }

        foreach (Match account in Account.Matches(text))
        {
            found.Add(account.Value);
        }

        return found;
    }

    [Fact]
    public void Scanner_FlagsEveryKindOfPersonalData_AndLetsTheMadeUpOnesThrough()
    {
        // Built at run time, so that this file holds no BattleTag of its own.
        var anyTag = "Some" + "Body" + "#" + "4321";
        var anAccount = "GameAccountId=[hi=144115198130930503 lo=" + "42]";

        Assert.Equal(new[] { anyTag }, Problems($"PlayerName={anyTag}"));
        Assert.Equal(new[] { anAccount }, Problems(anAccount));
        Assert.All(Purged, value => Assert.Contains(value, Problems($"x {value} y")));

        Assert.Empty(Problems("GameAccountId=[hi=0 lo=0]"));
        Assert.Empty(Problems("Lookup(\"TAURIEL#2981\") and Zed#1100"));
        Assert.Empty(Problems("color #2EA8FF, issue #4, C# 12, F#"));
    }

    [Fact]
    public void Repository_HoldsNoBattleTagNoOpponentNameAndNoAccountNumber()
    {
        var root = RepositoryRoot();
        var checkedFiles = 0;
        var problems = new List<string>();

        foreach (var file in RepositoryFiles(root))
        {
            var relative = Path.GetRelativePath(root, file);
            if (!TextExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            checkedFiles++;
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                problems.AddRange(Problems(lines[i]).Select(p => $"{relative.Replace('\\', '/')}:{i + 1}: {p}"));
            }
        }

        Assert.True(checkedFiles > 100, $"only {checkedFiles} text files found: is the root right ({root})?");
        Assert.Empty(problems);
    }

    [Fact]
    public void DataScanner_FlagsRealStatsFiles_AndLetsSyntheticOnesThrough()
    {
        // Built at run time, so that this file holds no signature of its own.
        var firestone = "{\"" + "lastUpdate" + "Date\": \"2026-10-08T00:10:31Z\", \"cardStats\": []}";
        var nomi = "{\"overview\": {\"" + "high" + "Players\": 30}}";

        Assert.NotEmpty(DataProblems("stats/card-stats.gz.json", 1_000, "{}"));
        Assert.NotEmpty(DataProblems("stats/hero-stats.gz", 1_000, string.Empty));
        Assert.NotEmpty(DataProblems("tests/Fixtures/big.json", 200_000, "{}"));
        Assert.NotEmpty(DataProblems("tests/Fixtures/firestone.json", 100, firestone));
        Assert.NotEmpty(DataProblems("tests/Fixtures/nomi.json", 100, nomi));

        Assert.Empty(DataProblems("tests/Fixtures/comp-cache-schema1-shape.json", 22_305, "{\"schema\": 1, \"compositions\": []}"));
        // A test file may name the fields to build synthetic data: only .json files are read for signatures.
        Assert.Empty(DataProblems("tests/TrinketTests.cs", 100, firestone));
    }

    [Fact]
    public void Repository_HoldsNoRealStatsData()
    {
        var root = RepositoryRoot();
        var problems = new List<string>();
        foreach (var file in RepositoryFiles(root))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var json = relative.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
            problems.AddRange(DataProblems(relative, new FileInfo(file).Length, json ? File.ReadAllText(file) : string.Empty));
        }

        Assert.Empty(problems);
    }

    private static IEnumerable<string> RepositoryFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar).Any(part => Skipped.Contains(part)));

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
