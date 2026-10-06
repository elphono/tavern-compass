using System.Text.RegularExpressions;

namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// The repository is public: no real BattleTag, opponent name or account number may be committed. The ones that had
/// slipped into old test fixtures were purged from the history on 2026-10-06; tests and docs use made-up names.
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
        var skipped = new[] { "bin", "obj", "lib", ".git", ".claude", "node_modules" };
        var checkedFiles = 0;
        var problems = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file);
            if (relative.Split(Path.DirectorySeparatorChar).Any(part => skipped.Contains(part))
                || !TextExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
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
