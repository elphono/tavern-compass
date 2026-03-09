using BronzebeardHud.GameState;
using BronzebeardHud.LogParser;

namespace BronzebeardHud.GameState.Tests;

public static class LogReplayHelper
{
    private static readonly Lexer Lexer = new();

    public static List<LogLine> ParseFixture(string fixtureName)
    {
        var path = Path.Combine("Fixtures", fixtureName);
        var lines = File.ReadAllLines(path);
        var result = new List<LogLine>();
        foreach (var line in lines)
        {
            var parsed = Lexer.ParseLine(line);
            if (parsed is { IsGameState: true, Packet: not null })
                result.Add(parsed);
        }
        return result;
    }

    public static GameStateEngine ReplayFixture(string fixtureName, GameStateEngine? engine = null)
    {
        engine ??= new GameStateEngine();
        var lines = ParseFixture(fixtureName);
        foreach (var line in lines)
            engine.Process(line);
        return engine;
    }

    public static GameStateEngine ReplayFixtures(params string[] fixtureNames)
    {
        var engine = new GameStateEngine();
        foreach (var name in fixtureNames)
            ReplayFixture(name, engine);
        return engine;
    }
}
