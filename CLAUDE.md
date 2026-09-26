# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Bronzebeard HUD is a Hearthstone Battlegrounds overlay: it tails the game's
`Power.log`, rebuilds the game state from the log packets, and displays the
player's board, shop and opponents in a transparent window pinned to the
Hearthstone window.

The log file is the **only** source of truth — no memory reading, no network
hook. Anything the overlay shows must be derivable from `Power.log` lines.

This is a C#/Avalonia rewrite of the Rust/egui tracker in
`../bg_treehudder`. LogParser and GameState are a direct port of the Rust
logic; the overlay app was rebuilt from scratch. The Rust repo remains the
reference for parsing behaviour and, in particular, for
`docs/reference/power-log-format.md` — read that before touching the lexer.

## Plugin Hearthstone Deck Tracker (depuis le 2026-09-26)

La parité avec Firestone et HSReplay-Tier7 passe désormais par un **plugin HDT** : HDT fournit
déjà l'overlay Battlegrounds gratuit de HSReplay (Bob's Buddy compris), et le plugin ajoute le reste.
Spec et plan : `docs/plans/2026-09-26-parite-tier7-{spec,plan}.md`. L'app Avalonia ci-dessous reste
en place, mais ce n'est plus la cible. Le plugin **ne lit jamais la mémoire du jeu** : il n'utilise
que ce qu'HDT expose, et HDT, lui, la lit.

| Projet | Cible | Rôle |
|---|---|---|
| `src/BronzebeardHud.Stats` | `netstandard2.0` | logique métier : format local des stats, import Firestone, cache, tiers, héros proposés ; aucune dépendance à HDT ni à WPF |
| `src/BronzebeardHud.HdtPlugin` | `net48` | `IPlugin` et UI WPF écrite en C#, sans XAML (le XAML WPF ne compile pas sous Linux) ; **hors de la solution** |
| `tests/BronzebeardHud.Stats.Tests` | `net8.0` | xUnit, sur la bibliothèque |

```bash
dotnet test                                  # la solution : tout sauf le plugin, sans réseau
dotnet build src/BronzebeardHud.HdtPlugin    # au 1er build, télécharge HDT (zip de 20 Mo) dans lib/hdt/<version>/
```

- `lib/` est ignoré par git. La version d'HDT contre laquelle on compile est `HdtVersion`, dans le
  `.csproj` du plugin ; la cible `FetchHdtAssemblies` télécharge la release GitHub correspondante.
- Déploiement (Windows) : copier `BronzebeardHud.HdtPlugin.dll` et `BronzebeardHud.Stats.dll` dans
  `%AppData%\HearthstoneDeckTracker\Plugins\BronzebeardHud\`, **sans** `Newtonsoft.Json.dll` : HDT
  charge la sienne, dans la même version (13.0.3).
- Données : `%LocalAppData%\BronzebeardHud\stats\`, qui contient le cache Firestone (rafraîchi au
  plus une fois par 24 h) ; les saisies manuelles HSReplay vont dans son sous-dossier `manual\`, au
  format décrit par la spec.
- Sous WSL, on vérifie les tests et le build, rien de plus. Le chargement par HDT, le rendu et les
  événements réels ne se vérifient que sous Windows, avec HDT installé.
- Le dépôt est **public** : aucune donnée réelle de Firestone ni de HSReplay n'y entre ; les tests
  utilisent des données synthétiques.

## Tech Stack

.NET 8, Avalonia 11 (Fluent theme, compiled bindings), xUnit. Everything
targets plain `net8.0` and is developed and run under WSL; only a few
`OperatingSystem.IsWindows()` branches and P/Invokes are Windows-specific.

## Build & Run

```bash
dotnet build                                   # whole solution
dotnet test                                    # all test projects
dotnet test tests/BronzebeardHud.GameState.Tests   # one project
dotnet test --filter "FullyQualifiedName~OpponentTrackingTests"
dotnet run --project src/BronzebeardHud.App    # launch the overlay
```

The app writes a verbose trace to stdout (`[LogWatcher]`, `[GameStateService]`,
`[Overlay]` prefixes) — that console output is the primary debugging tool, and
`GameStateEngine.DebugLogging` adds packet-level detail.

`LogPaths.DefaultLogsDir()` hardcodes the Hearthstone logs location
(`E:\JEUX\Hearthstone\Logs`, or its `/mnt/e/...` WSL equivalent). There is no
CLI argument to override it — change that method when testing against logs
elsewhere.

### The WSL overlay helper

Under WSLg, X11 window positioning and always-on-top are ignored, so the
overlay cannot place itself. `HsWindowService` shells out to a small Windows
helper, **which lives outside this repo** at `C:\temp\HsHelper`
(`/mnt/c/temp/HsHelper`), and is looked up at the published path
`/mnt/c/Temp/HsHelper/pub/HsHelper.exe`. The helper prints
`Left,Top,Right,Bottom,IsForeground` for the Hearthstone window and moves the
overlay via Win32 `SetWindowPos`. If the overlay never appears or never moves
under WSL, check that this exe exists — the service logs "WSL helper NOT
found". Note WSLg appends ` (<distro>)` to window titles, which is why the
overlay title is rewritten before being handed to the helper.

On Windows proper none of this is needed: Avalonia's `Position`/`Topmost` work,
and `HsWindowService.ApplyOverlayExStyle` adds `WS_EX_TOOLWINDOW |
WS_EX_NOACTIVATE` once the window has a native handle.

## Architecture

One-way pipeline, no shared mutable state and no locks:

```
Power.log → LogWatcher → Lexer → RawPacket → GameStateEngine → GameStateSnapshot
          → Channel<WatcherEvent> → GameStateService → Dispatcher.UIThread
          → MainViewModel → XAML bindings
```

### `src/BronzebeardHud.LogParser`

Pure text extraction, no game rules.

- `LogWatcher` — polls every 500 ms, picks the newest `Hearthstone_*` session
  folder's `Power.log`, and publishes `WatcherEvent.Line` /
  `WatcherEvent.SessionChanged` over an unbounded `Channel`. Two details that
  matter: the file is opened with `FileShare.ReadWrite` because Hearthstone
  holds it open for writing, and on attaching to a file the watcher starts at
  the **last** `CREATE_GAME` line, so launching mid-session picks up the
  current game rather than replaying the whole day.
- `Lexer` — compiled regexes turning one line into a
  `LogLine { Timestamp, Indent, IsGameState, Packet }`. Indentation is
  meaningful: 4 spaces = one nesting level, and the engine relies on it to
  attribute bare `tag=…` lines to the entity being defined.

Two facts about `Power.log` shape the whole parser:

1. Every packet is logged **twice** — by `GameState.DebugPrintPower()` and
   again by `PowerTaskList.DebugPrintPower()`. Only `IsGameState` lines are
   forwarded; processing both double-applies everything.
2. Entities appear in four shapes — `GameEntity`, a numeric id, a player name,
   or a `[entityName=… id=… zone=…]` bracket ref — hence the `EntityRef`
   hierarchy.

### `src/BronzebeardHud.GameState`

- `EntityRegistry` / `Entity` — every entity is just an id, a card id and a
  `Dictionary<string, string>` of tags. Nothing is typed at ingest time;
  meaning is applied when the snapshot is built.
- `EntityResolver` — resolves an `EntityRef` to a numeric id. Player names only
  resolve once a `PlayerName` packet has registered them.
- `GameStateEngine` — applies packets and tracks phase/turn. It holds no
  `GameStateSnapshot`: `Snapshot()` rebuilds the whole read-model from the
  registry on demand, so new state belongs in the registry rather than in
  engine fields. Battlegrounds rules concentrated here:
  - The **local player** is the `Player` entity *without* `BACON_DUMMY_PLAYER=1`;
    `IdentifyLocalPlayer()` must be called once the game reaches `HeroSelect`.
  - **Opponents are not player entities.** They are hero entities in the
    `SETASIDE` zone carrying `PLAYER_LEADERBOARD_PLACE`, excluding the local
    hero.
  - Derived at snapshot time, never stored: HP is `HEALTH - DAMAGE`, gold is
    `RESOURCES - RESOURCES_USED + TEMP_RESOURCES`, tavern tier is
    `PLAYER_TECH_LEVEL`. Board and shop are both in the `PLAY` zone and are
    told apart by `CONTROLLER`.
  - `EnrichFromBracketRef` back-fills card id and zone from bracket refs for
    entities never announced by a `FULL_ENTITY` packet — which is how opponent
    heroes usually arrive.
- `GamePhase` — from the GameEntity `STEP` tag: `BEGIN_MULLIGAN` → HeroSelect,
  `MAIN_READY` → Shopping, `MAIN_START_TRIGGERS` → Combat, `FINAL_GAMEOVER` →
  GameOver.
- `CardDb` — card id → display name, from `Data/bg_cards.tsv` shipped as an
  **embedded resource** (no runtime file dependency).

### `src/BronzebeardHud.App`

- `GameStateService` — consumes the channel on a background task, runs the
  engine, and posts snapshots to the UI with `Dispatcher.UIThread.Post`. UI
  updates are throttled to one per 100 ms except on phase change; without that
  throttle, catching up on a large log floods the dispatcher.
- `MainViewModel` — `INotifyPropertyChanged`; the whole UI binds to a single
  `State` snapshot plus derived flags (`IsInGame`, `IsShopping`, `PhaseText`).
  Panels show and hide by binding to those, not by imperative code.
- `CardImageCache` — singleton, memory + disk cache under
  `%LocalAppData%/BronzebeardHud/cards`, downloading from
  `art.hearthstonejson.com`. Because bitmaps arrive asynchronously after a
  binding has already evaluated, the view model bumps an `ImageVersion`
  counter to force re-evaluation — that indirection is deliberate.
- `MainWindow.axaml.cs` — owns the HS-window tracking loop (poll, compute the
  overlay rect on the right edge of the Hearthstone window, show/hide when
  Hearthstone loses foreground).

## Tests

`tests/BronzebeardHud.GameState.Tests/Fixtures/*.txt` are excerpts of real
`Power.log` sessions, one per game moment (`game_start`, `hero_select`,
`first_shopping`, `opponents_appear`, `game_over`), copied to the output
directory at build time. `LogReplayHelper.ReplayFixture(s)` runs them through
the real lexer and engine, so a parsing or state bug is reproduced by capturing
the offending lines into a new fixture and asserting on the resulting snapshot.
Fixtures can be chained to build up a full timeline.

## Docs

`docs/plans/` holds the migration design and the step-by-step implementation
plan the port followed; the design doc is the place to look for *why* the
C# structure mirrors the Rust one.
