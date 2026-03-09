using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using BronzebeardHud.GameState;
using BronzebeardHud.LogParser;
using BronzebeardHud.App.ViewModels;

namespace BronzebeardHud.App.Services;

public class GameStateService
{
    private readonly MainViewModel _viewModel;
    private readonly string _logsDir;

    public GameStateService(MainViewModel viewModel, string logsDir)
    {
        _viewModel = viewModel;
        _logsDir = logsDir;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine($"[GameStateService] Starting. Logs dir: {_logsDir}");
        Console.WriteLine($"[GameStateService] Dir exists: {System.IO.Directory.Exists(_logsDir)}");

        var watcher = new LogWatcher(_logsDir);
        var reader = watcher.Watch(ct);
        var engine = new GameStateEngine { DebugLogging = true };
        var playerIdentified = false;
        var lastPhase = GamePhase.NotStarted;
        var lineCount = 0;

        Console.WriteLine("[GameStateService] Waiting for log events...");

        await foreach (var evt in reader.ReadAllAsync(ct))
        {
            switch (evt)
            {
                case WatcherEvent.SessionChanged:
                    Console.WriteLine("[GameStateService] === SESSION CHANGED ===");
                    engine = new GameStateEngine { DebugLogging = true };
                    playerIdentified = false;
                    Dispatcher.UIThread.Post(() => _viewModel.State = new GameStateSnapshot());
                    break;

                case WatcherEvent.Line(var logLine):
                    lineCount++;
                    if (lineCount <= 5 || lineCount % 100 == 0)
                        Console.WriteLine($"[GameStateService] Line #{lineCount}: {logLine.Packet?.GetType().Name ?? "null"}");

                    engine.Process(logLine);

                    if (!playerIdentified)
                    {
                        var snap = engine.Snapshot();
                        if (snap.Phase == GamePhase.HeroSelect)
                        {
                            engine.IdentifyLocalPlayer();
                            playerIdentified = true;
                            Console.WriteLine("[GameStateService] Player identified!");
                        }
                    }

                    var state = engine.Snapshot();
                    if (state.Phase != lastPhase)
                    {
                        Console.WriteLine($"[GameStateService] Phase: {lastPhase} -> {state.Phase}");
                        lastPhase = state.Phase;
                    }

                    Dispatcher.UIThread.Post(() => _viewModel.State = state);
                    break;
            }
        }

        Console.WriteLine("[GameStateService] Watch loop ended.");
    }
}
