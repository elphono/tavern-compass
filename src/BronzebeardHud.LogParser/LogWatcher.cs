using System.Threading.Channels;

namespace BronzebeardHud.LogParser;

public class LogWatcher
{
    private readonly string _logsDir;

    public LogWatcher(string logsDir)
    {
        _logsDir = logsDir;
    }

    public ChannelReader<WatcherEvent> Watch(CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<WatcherEvent>();
        _ = Task.Run(() => WatchLoop(channel.Writer, ct), ct);
        return channel.Reader;
    }

    private async Task WatchLoop(ChannelWriter<WatcherEvent> writer, CancellationToken ct)
    {
        var lexer = new Lexer();
        string? currentPath = null;
        int lineIndex = 0;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(500, ct);

                var latest = FindLatestPowerLog();
                if (latest == null) continue;

                if (currentPath != latest)
                {
                    Console.WriteLine($"[LogWatcher] New session: {latest}");
                    if (currentPath != null)
                    {
                        await writer.WriteAsync(new WatcherEvent.SessionChanged(), ct);
                    }
                    currentPath = latest;
                    lineIndex = FindLastCreateGameLine(latest);
                    Console.WriteLine($"[LogWatcher] Starting at line {lineIndex}");
                }

                List<string> allLines;
                try
                {
                    allLines = ReadAllLinesShared(currentPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LogWatcher] Read error: {ex.Message}");
                    continue;
                }

                for (int i = lineIndex; i < allLines.Count; i++)
                {
                    var line = allLines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (i == lineIndex)
                        Console.WriteLine($"[LogWatcher] First line: {line[..Math.Min(line.Length, 120)]}");

                    if (line.Contains("CREATE_GAME") && line.Contains("GameState"))
                    {
                        Console.WriteLine("[LogWatcher] CREATE_GAME detected");
                        await writer.WriteAsync(new WatcherEvent.SessionChanged(), ct);
                    }

                    var logLine = lexer.ParseLine(line);
                    if (logLine?.IsGameState == true)
                    {
                        await writer.WriteAsync(new WatcherEvent.Line(logLine), ct);
                    }
                }

                lineIndex = allLines.Count;
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            writer.Complete();
        }
    }

    /// <summary>
    /// Read all lines from a file that may be open for writing by another process.
    /// </summary>
    private static List<string> ReadAllLinesShared(string path)
    {
        var lines = new List<string>();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
            lines.Add(line);
        return lines;
    }

    private string? FindLatestPowerLog()
    {
        if (!Directory.Exists(_logsDir)) return null;

        var latest = Directory.GetDirectories(_logsDir, "Hearthstone_*")
            .OrderDescending()
            .FirstOrDefault();

        if (latest == null) return null;
        var powerLog = Path.Combine(latest, "Power.log");
        return File.Exists(powerLog) ? powerLog : null;
    }

    private static int FindLastCreateGameLine(string path)
    {
        try
        {
            var lines = ReadAllLinesShared(path);
            int lastLine = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Contains("CREATE_GAME") && lines[i].Contains("GameState"))
                    lastLine = i;
            }

            return lastLine;
        }
        catch
        {
            return 0;
        }
    }
}
