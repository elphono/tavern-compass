# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Vue d'ensemble

Bronzebeard HUD est un overlay Hearthstone Battlegrounds. **Le produit actif est un plugin
Hearthstone Deck Tracker (HDT)** qui vise la parité avec Firestone et HSReplay-Tier7 ; il a été
retenu le 2026-09-26 par l'étude `docs/plans/2026-09-26-etude-stack.md` (critère unique d'Ali :
atteindre ce résultat le plus vite possible).

L'app Avalonia autonome (`src/BronzebeardHud.App`, sections *Tech Stack* à *Tests* plus bas) est
la première cible du dépôt : une réécriture C# du tracker Rust `bg_treehudder`, qui lit `Power.log`
comme seule source de vérité. Elle reste compilée et testée, mais **n'est plus développée**.

**Le dépôt `bg_treehudder` n'existe plus en local** : tout ce qui en avait de la valeur est ici.

| Ce qui venait de `bg_treehudder` | Où c'est maintenant |
|---|---|
| format annoté de `Power.log` (à lire avant de toucher au lexer) | `docs/reference/power-log-format.md` |
| étude de stack | `docs/plans/2026-09-26-etude-stack.md` |
| recherche HDT / Tier7 (inventaire, tags, mécaniques S14) | `docs/reference/recherche-hdt-tier7.md` |
| tout le dépôt Rust : code, plans, journal, logs d'exemple, branche `rust/parite-tier7-wip` | `docs/archive/bg_treehudder.bundle`, et le remote `github.com/elphono/bg_treehudder` |

Restaurer le Rust : `git clone docs/archive/bg_treehudder.bundle /tmp/rust && git -C /tmp/rust fetch
origin 'refs/remotes/origin/*:refs/remotes/bundle/*'` (la branche WIP arrive en `bundle/rust/parite-tier7-wip`).

## État du projet (au 2026-09-26)

Le plan `docs/plans/2026-09-26-parite-tier7-plan.md` fait foi ; l'historique des décisions est dans
`docs/journal/2026-09-26-plugin-hdt.md`.

| Phase | Contenu | Livré | Vu en jeu par Ali |
|---|---|---|---|
| 1 | squelette du plugin, stats Firestone des héros proposés | ✓ | ✓ |
| 2 | tranche de MMR, MMR des adversaires, **conseiller de compositions** | ✓ | ✓ compos et marqueurs de taverne |
| 3 | tribus du lobby, trinkets, épinglage | ✓ | partiel |
| 4 | historique des combats, graphe des PV, plateaux d'inspiration | ✓ puis **retiré le 2026-09-27** (Ali : « l'onglet combat est inutile ») | — |
| 5 | top 4 des héros, plateau vs courbe du héros, compo par héros, bilan par adversaire | ✓ | ✗ |
| 6 | affinité compo ↔ héros, nombre de compos réglable, épinglage au clic, pivots, « comment les tops le jouent », bouton Meta | ✓ | ✗ |

Ce qui reste ouvert :

- **Vérifier en jeu** les phases 5 et 6 (liste exhaustive : spec § 5), et les livraisons du 2026-09-27
  (panneau des compos simplifié en ovales, détail au clic, lineups à part, bouton Skip combat — reconnexion
  effective, relance directe de Hearthstone.exe hors Battle.net, état d'HDT après la mort du client :
  `docs/journal/2026-09-27-skip-combat-et-detail-compos.md` et `docs/journal/2026-09-27-panneau-compos-simplifie.md`).
- **Deux arbitrages d'Ali** : garder la ligne « comp ≈ » sous chaque héros (échantillons minces, 17
  parties en médiane) ; garder le bilan par adversaire s'il doublonne l'interface du jeu.
- **Import HSReplay jamais utilisé** : `stats\manual\` est vide, seules les 24 compos Firestone tournent.
- Hors périmètre, tranché : notification Timewarped (mécanique absente des parties de la saison 14,
  prouvé sur les logs), stats de quêtes (fichier Firestone vide), marqueur « prochain adversaire »
  (déjà affiché par le jeu, retiré).

## Décisions et accords à ne pas re-trancher

| Sujet | Décision (Ali, 2026-09-26) |
|---|---|
| Stack | plugin HDT, option 3 de l'étude ; HDT lit la mémoire, le plugin jamais |
| Stats Firestone | **accord de l'auteur de Firestone** pour récupérer ses JSON publics nous-mêmes (`static.zerotoheroes.com`), cache local, rafraîchissement modeste |
| Stats HSReplay | usage local accepté, mais le site renvoie un challenge Cloudflare : **on ne contourne pas** une protection anti-bot ; import semi-manuel depuis le navigateur (spec § 6) |
| Simulateur npm `simulate-bgs-battle` | usage personnel, autorisé ; inutile tant que Bob's Buddy (HDT) fait le travail |
| MMR des adversaires | gardé tel quel. Le leaderboard EU s'arrête à 8 000 ; Ali est à ≈ 6 840 (région EU mesurée) ; plage par défaut 8 000 – 8 050 |
| Visibilité | dépôt GitHub **privé** depuis le 2026-09-26 |

## Façon de travailler sur ce projet

- Ali teste en partie sous Windows ; la session **déploie elle-même** les DLL après chaque livraison
  (build Release depuis `main`, idéalement avec `HdtInstallDir`, copie, comparaison des SHA-1), puis
  Ali relance HDT. HDT ne recharge les plugins qu'à son démarrage (ou décocher / recocher le plugin).
- Diagnostic : le journal d'HDT (`/mnt/c/Users/elphono/AppData/Roaming/HearthstoneDeckTracker/Logs/hdt_log.txt`)
  porte une ligne `Bronzebeard HUD: …` par tour et par fonctionnalité ; une fonctionnalité qui lève
  est coupée seule par `FeatureGuard` et le dit une fois. Lire cette ligne **avant** de supposer une cause.
- Retours constants d'Ali sur l'UI : aucun texte tronqué, chaque indication alignée sur la carte ou
  le héros qu'elle concerne, couleurs vives et distinctes, rien ne masque l'interface du jeu, ne pas
  dupliquer ce que le jeu ou HDT affichent déjà.

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
- GitHub s'arrête à la 1.55.6 : les versions suivantes ne sortent que par l'auto-updater d'HDT. Pour
  compiler contre l'HDT réellement installé (recommandé avant un déploiement) :
  `dotnet build src/BronzebeardHud.HdtPlugin -c Release -p:HdtInstallDir=/mnt/c/Users/<user>/AppData/Local/HearthstoneDeckTracker/app-<version>/`.
  Mesuré le 2026-09-26 : le plugin compile sans erreur ni avertissement contre la 1.58.3.
- Déploiement (Windows) : copier `BronzebeardHud.HdtPlugin.dll` et `BronzebeardHud.Stats.dll` dans
  `%AppData%\HearthstoneDeckTracker\Plugins\BronzebeardHud\`, **sans** `Newtonsoft.Json.dll` : HDT
  charge la sienne, dans la même version (13.0.3).
- Données : `%LocalAppData%\BronzebeardHud\stats\`, qui contient le cache Firestone (héros et
  trinkets : 24 h ; compositions : 7 jours). Dans son sous-dossier `manual\`, les fichiers écrits à la
  main : `*.json` (stats de héros HSReplay, spec § 2), `*.comps.txt` (compositions HSReplay, spec § 6)
  et `pins.txt` (sbires à signaler en taverne, un par ligne ; en partie, le bouton ◇ au-dessus d'une carte
  de Bob l'épingle ou la désépingle pour la partie, sans toucher au fichier). Un cache d'un format antérieur (champ
  `schema` : 4 pour les compositions depuis leurs cinq plateaux finaux, 2 pour les stats de héros depuis
  la courbe de plateau, les fichiers de héros tapés à la main pouvant rester en 1) ou illisible est retéléchargé ;
  si ce téléchargement échoue, la ligne `Bronzebeard HUD: tavern …` du journal d'HDT dit pourquoi
  (`comps=0 (cache: schema 3 ≠ 4, redownload failed: …)`). On ne supprime jamais le cache à la main. Le
  cache des compositions est du JSON compact (≈ 77 Ko sur last-patch).
- Panneaux déplaçables (compos visées, lineups, Skip combat) : menu Plugins d'HDT › Bronzebeard HUD › « Move panels »
  (ou le bouton du plugin dans les options). Hors de ce mode, rien n'est cliquable au-dessus du jeu. Les
  positions sont gardées dans `%LocalAppData%\BronzebeardHud\layout.json`, en fractions de la taille de
  l'overlay : `{"schema": 1, "panels": {"lineups": {"left": 0.76, "top": 0.07}}}`. Un fichier illisible
  donne la disposition par défaut (message dans le journal d'HDT) ; « Reset panel positions » la rétablit.
  Une entrée d'un panneau qui n'existe plus (celui des combats, retiré) est ignorée sans message.
  Les marqueurs attachés à une carte, un héros ou une tuile du classement ne bougent pas.
- Panneau des compos, overlay verrouillé : une ligne par compo — case, nom et place moyenne, sept ovales
  (cerclés de vert + ✓ si tenus, de rouge sinon) ; survoler un ovale montre la carte entière, et la case
  fait viser la compo seule (quatre au plus, une couleur chacune, oubliées à la partie suivante).
  Ses − et + règlent le nombre de compos suggérées (1 à 8, 3 par défaut), gardé dans
  `%LocalAppData%\BronzebeardHud\settings.json` : `{"schema": 1, "suggestedCompositions": 3}`. Les
  suggestions sont les compos atteignables, meilleure place moyenne d'abord (avec le héros joué quand
  elle est connue).
- Détail d'une compo : un clic sur son nom ou un de ses ovales le montre à la place de la liste (« ← back »
  pour revenir) — enablers de tier ≤ 3, pièces clés, pivots, tour final médian — **dérivé** des cartes et
  plateaux finaux par `CompDetail`, car aucune source n'a de donnée early game. Une pièce clé de tier ≤ 3
  reste dans les enablers : choix d'Ali du 2026-09-27, fixé par un test.
- « Comment les tops le jouent » : le bouton ? au-dessus de chaque sbire de Bob (à droite du ◇) ouvre un
  panneau à part, déplaçable (`lineups`), par défaut sur celui des compos ; fermé par son × ou en fin de taverne.
- Bouton « Skip combat » (jaune, en combat seulement, panneau déplaçable `skip-combat`) : tue Hearthstone et
  le fait relancer **par Battle.net** (`--exec="launch WTCG"`, redemandé chaque seconde : ≈ 7 s mesurées),
  jamais par son exécutable (connexion refusée, mesuré) ; sans Battle.net, rien n'est tué. Un clic par combat,
  lignes `Bronzebeard HUD: skip combat …` (parent, commande, demandes, nouveau pid, vivant 3 s après).
- Sous WSL, on vérifie les tests et le build, rien de plus. Le chargement par HDT, le rendu et les
  événements réels ne se vérifient que sous Windows, avec HDT installé.
- Le dépôt est **privé** (il était public jusqu'au 2026-09-26) : on garde malgré tout la règle
  qu'aucune donnée réelle de Firestone ni de HSReplay n'y entre ; les tests utilisent des données
  synthétiques.

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

| Dossier | Contenu |
|---|---|
| `docs/plans/` | 2026-03-08 : conception et plan du portage Rust → C# ; 2026-09-26 : étude de stack, spec et plan du plugin HDT |
| `docs/reference/` | format de `Power.log`, recherche HDT / Tier7 |
| `docs/journal/` | ce qui s'est décidé, séance par séance |
| `docs/archive/` | le dépôt Rust complet, en bundle git |
