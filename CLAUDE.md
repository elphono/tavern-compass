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

- **Vérifier en jeu le panneau unique « Compositions »** (2026-10-04, `docs/journal/2026-10-04-panneau-unique.md`) :
  liste des guides de HDT et couleurs des cibles, détail au clic, cadres sur les cartes de Bob, étiquettes des choix,
  tribus du lobby (bêtes). Vu seulement dans la simulation (captures, `--selftest`).
- **Vérifier en jeu la poignée de redimensionnement** (2026-10-04) : le calcul est testé et éprouvé par mutation,
  mais le pointage sous HDT, le cadre pointillé, le rendu d'un panneau étroit et le retour au défaut par « Reset » ne
  se voient que sous Windows.
- **Vérifier en jeu** les phases 5 et 6 (liste exhaustive : spec § 5), et le Skip combat relancé par Battle.net
  (`docs/journal/2026-09-27-*.md`).
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
| Stats Firestone | **accord de l'auteur de Firestone**, étendu le 2026-09-27 à **toutes ses données publiques**, pour tous nos usages, pas seulement les JSON de stats (`static.zerotoheroes.com`) : aussi card-stats, battlegrounds-strategies, perfect-games, card-rules ; cache local, rafraîchissement modeste. Inchangé : aucune donnée réelle dans le dépôt, tests sur données synthétiques |
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
  trinkets : 24 h ; compositions : 7 jours). **Au démarrage du plugin**, chaque fichier est redemandé au
  serveur quel que soit son âge, en requête conditionnelle (ETag dans `*.etag` : `304` s'il n'a pas changé) ;
  les âges ne valent qu'ensuite, dans la session. Une ligne `Bronzebeard HUD: data …` par chargement dit
  `downloaded`, `unchanged (304)`, `cached` ou `FAILED` et la date des données. Dans son sous-dossier `manual\`, les fichiers écrits à la
  main : `*.json` (stats de héros HSReplay, spec § 2), `*.comps.txt` (compositions HSReplay, spec § 6)
  et `pins.txt` (sbires à signaler en taverne, un par ligne ; en partie, le bouton ◇ au-dessus d'une carte
  de Bob l'épingle ou la désépingle pour la partie, sans toucher au fichier). Un cache d'un format antérieur (champ
  `schema` : 4 pour les compositions depuis leurs cinq plateaux finaux, 2 pour les stats de héros depuis
  la courbe de plateau, les fichiers de héros tapés à la main pouvant rester en 1) ou illisible est retéléchargé ;
  si ce téléchargement échoue, la ligne `Bronzebeard HUD: data comp-stats …` du journal d'HDT dit pourquoi
  (`FAILED, cache: schema 3 ≠ 4, redownload failed: …`). On ne supprime jamais le cache à la main. Le
  cache des compositions est du JSON compact (≈ 77 Ko sur last-patch).
- Panneaux déplaçables (`target-compositions`, le panneau « Compositions », et `skip-combat`) : menu Plugins d'HDT › Bronzebeard HUD › « Move panels »
  (ou le bouton du plugin dans les options). Hors de ce mode, rien n'est cliquable au-dessus du jeu. Les
  positions sont gardées dans `%LocalAppData%\BronzebeardHud\layout.json`, en fractions de la taille de
  l'overlay : `{"schema": 1, "panels": {"target-compositions": {"left": 0.76, "top": 0.07}}}`. Un fichier illisible
  donne la disposition par défaut (message dans le journal d'HDT) ; « Reset panel positions » la rétablit.
  Une entrée d'un panneau qui n'existe plus (`combats`, `lineups`, `comp-guides`) est ignorée sans message.
  Les marqueurs attachés à une carte, un héros ou une tuile du classement ne bougent pas.
- Redimensionner (même mode : poignée ◢ au coin bas-droit du panneau « Compositions », cadre
  pointillé cyan autour de la place donnée ; **pas de poignée sur Skip combat**, un bouton n'a rien à montrer en plus
  ou en moins). On donne de la **place** au contenu, jamais un zoom (Ali, 2026-10-04) : le panneau montre plus ou
  moins de guides (« 2 of 15 shown ») ou de sections du détail, au même corps de texte, donc le plancher de
  12 px tient. La taille est gardée à côté de la position, `"width"` et `"height"` en fractions de l'overlay,
  facultatifs ensemble : `{"left": 0.76, "top": 0.07, "width": 0.22, "height": 0.5}` ; un fichier sans taille se lit
  comme avant, et « Reset panel positions » rend aussi la taille. Minimum (`PanelFit.TargetMin*`) : la largeur par
  défaut (case, nom, six ovales) et le titre + 1 ligne. Une boîte plus petite que ce que le panneau montre toujours
  (une ligne de guide, sous la barre de son tier) grandit pour le tenir. Hors mode déplacement la boîte épouse son
  contenu jusqu'à la taille choisie. La poignée et le cadre sont des éléments du canvas gérés par `PanelMover`, pas
  des enfants du panneau, qui remplace tout son contenu à chaque redessin. La poignée **ne garde aucun rectangle** :
  la place d'où part un redimensionnement est demandée au `PanelLayout` à chaque geste (`Resize` prend la place par
  défaut et lit le reste), car un rectangle gardé au dernier redessin est périmé dès qu'on déplace le panneau (un
  déplacement finit sans redessin) et renvoyait le panneau à sa place d'avant (constaté le 2026-10-04). Le journal d'HDT
  dit `resize start` / `resize end` (place du panneau sur le canvas et place que dit le layout : elles doivent être
  égales) et `panel moved`. Le calcul en lignes de l'ancien panneau (`PanelFit.Rows`, `DetailPivots`, et leur
  `Tolerance` : une boîte exactement de la hauteur de n lignes, divisée par une échelle qui n'est pas une fraction
  binaire, rendait n − 1 lignes) est retiré depuis la fusion : le panneau unique mesure ses pièces en place, en pixels
  de l'overlay, et `CompGuideLayout` décide ce qui tient (`Sections` avec une tolérance, `Fit` sans).
- Encart des héros proposés (sélection du héros, fixe) : sous le bouton de reroll du jeu (« Réinitialiser »,
  0,632 → 0,718 H), de 0,725 à 0,805 H, 0,17 H de large, dans la colonne du héros (grille d'HDT, un héros tous
  les 340/1080 H). Un encart qui approcherait à moins de 0,01 H du bouton OK (0,751 → 0,825 H) se décale de
  côté, à l'écart (0,009 H au plus mesuré), ou passe dessous s'il faudrait plus de 0,03 H (celui du milieu à
  trois héros : 0,835 → 0,915 H). Cotes mesurées sur la capture Hearthstone d'Ali du 2026-09-26 18:29:44
  (2291 × 1360), fixées par `HeroPickLayoutTests` ; l'encart d'origine, centré à 0,667 H, cachait le reroll.
- Aucun texte du plugin sous 12 px en 1080p (`PanelTypography`) et aucun `Viewbox` : ce qui ne tient pas est
  omis, jamais rétréci (encart des héros : la ligne « comp ≈ » passe sur deux lignes ou disparaît ; MMR des
  adversaires : le rang disparaît, la cote reste). Un test lit les sources du plugin et y refuse `Viewbox` et
  `FontSize = <nombre>`.
- **Panneau « Compositions »** (`CompsPanel`, un seul panneau depuis le 2026-10-04 à la place de « Target compositions »
  et « HDT comp guides » : `docs/journal/2026-10-04-panneau-unique.md`), en taverne et en combat, par défaut sous le
  plateau du joueur à droite du héros (`TavernLayout.TargetPanel`). **Source** : les Comp Guides que HDT affiche lui-même,
  lus par son API publique (`API.Core.OverlayWindow.BattlegroundsCompsGuidesVM` : `CurrentState`, `Comps` gratuite ou
  `CompsByTier` Tier 7, objets `HSReplay.Responses.BattlegroundsCompGuide`) ; HDT la charge à chaque début de partie, le
  plugin ne fait aucune requête ; le `.csproj` référence `HSReplay.dll` (fourni par HDT, jamais copié) pour ce seul type ;
  mesure et forme des données : `docs/journal/2026-10-04-comp-guides-hdt.md`. Titre : « Compositions », « k of n shown »
  quand des guides manquent, la source (« HDT free » / « Tier 7 »), « Meta ↗ », « − n targets + » (1 à 4, 3 par défaut,
  gardé dans `%LocalAppData%\BronzebeardHud\settings.json` : `{"schema": 1, "suggestedCompositions": 3}`) ; une ligne
  dorée tant que HDT n'a pas de guides.
- **Cibles** (`CompTargetTracker`) : les guides cochés (quatre au plus, ordre de coche), puis les plus probables d'après
  le plateau **et** la main (3 × carte clé, 2 × enabler, 1 × add-on, `CompGuideMatch`), jusqu'à n. Une cible garde sa
  couleur tant qu'elle le reste (magenta, lime, bleu ciel, blanc), cases et couleurs sont oubliées à la partie suivante.
  Liste dans l'ordre de HDT par tier (S → D, barres aux dégradés de HDT), les cibles en tête de leur tier ; une ligne =
  case, nom (deux lignes au besoin, jamais coupé ; couleur et gras d'une cible, blanc si quelque chose est tenu, gris
  sinon), les **cartes clés** seules en ovales (anneau vert + ✓ si tenues, tier en badge ; au-delà de six : cinq et
  « +k »). Une cible porte en plus un liseré de 3 px, une teinte et une pastille de rang dans sa couleur. Ce qui ne tient
  pas est omis, les cibles en dernier (`CompGuideLayout.Fit`). Survoler un ovale montre la carte entière.
- **Détail** (clic sur un nom ou un ovale, comme dans HDT) : « ← All comp guides » à la place du titre, case, nom, badges
  de tier et de difficulté (couleurs de HDT, `CompGuideDifficulty`), puis HOW TO PLAY (première ligne, noms de cartes en
  gras), CORE CARDS, ADDON CARDS, WHEN TO COMMIT (une pastille par ligne), COMMON ENABLERS, PIVOTS (`GuidePivots`). Une
  section qui ne tient pas est omise entière (« k of n sections », `CompGuideLayout.Sections`) : à la place par défaut
  en 1080p, deux ou trois tiennent ; agrandir le panneau pour tout voir.
- **Taverne** (`TavernMarkers`, `TavernHighlights.For(Bob, cibles)`) : carte clé d'une cible → cadre plein, enabler ou
  add-on → pointillés, dans la couleur de la cible (carte clé d'abord, puis l'ordre des cibles), étiquette « core Nom
  k/N », « enabler Nom » ou « + Nom » ; le ◇ au-dessus de chaque sbire l'épingle (cadre blanc). **Choix** (découverte,
  Dark Gift, trinket : `ChoiceAdvisor`) : carte d'une cible → étiquette dans sa couleur (« ★ core Nom 2/3→3/3 »,
  « + Nom ») ; sinon le guide jouable dans le lobby dont elle est carte clé (« core Nom (S) », neutre) ; sinon « — ».
  Les tribus du lobby sont lues par valeur (`GuideTribes.NameOrEnum` : 20 est à la fois BEAST et PET).
- **Journal** : `comp guides loaded from HDT (…)` à chaque nouvelle liste, `… comp guides: none from HDT (state …)` tant
  que HDT n'a rien ; `comps round=… source=… comps=… board=… hand=… targets=[1. Nom #couleur ★k/N; …]` à la fin de
  chaque tour de taverne ; `tavern highlights=[carte:core|enabler|addon:guide, …] targets=[…]` quand ils changent ;
  `choice kind=…` par choix ; `comp detail id=… sections=k of n` à chaque détail ouvert ; `ticked guides=[…]`.
- Les compositions de Firestone (`CompService`, `TavernAdvisor`, `CompositionRows`, `CompDetail`, `CompTransitions`,
  `MinionLineups`) restent chargées et dans le code, pour orienter les aides plus tard, mais ne sont plus affichées
  (hors ligne « comp ≈ » de l'encart des héros). Une erreur d'un fichier de `manual\` est dite une fois par
  `compositions data: …` (avertissement). `docs/mock/` est la maquette de l'ancien panneau Firestone.
- Bouton « Skip combat » (jaune, en combat seulement, panneau déplaçable `skip-combat`) : tue Hearthstone et
  le fait relancer **par Battle.net** (`--exec="launch WTCG"`, redemandé chaque seconde : ≈ 7 s mesurées),
  jamais par son exécutable (connexion refusée, mesuré) ; sans Battle.net, rien n'est tué. Un clic par combat,
  lignes `Bronzebeard HUD: skip combat …` (parent, commande, demandes, nouveau pid, vivant 3 s après).
- Sous WSL, on vérifie les tests et le build. Le chargement par HDT et les événements réels ne se vérifient que
  sous Windows, avec HDT installé. **Exception : la simulation** `tools/BronzebeardHud.Harness/` (README) fait tourner
  les vrais panneaux (`PanelMover`, « Compositions », cadres sur les cartes de Bob, Skip combat) dans une fenêtre Windows ordinaire, sans
  HDT ni partie, avec des données synthétiques ; `launch.sh` la compile sous WSL et la lance côté Windows, `--selftest`
  la vérifie sans personne au clavier, `--screenshot` écrit une capture que la session peut regarder. Elle ne simule
  pas la couche d'HDT (clics transparents au-dessus du jeu, survol sondé à 60 Hz) : un défaut qui y vivrait ne s'y voit pas.
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
| `docs/plans/2026-10-04-panneau-unique-ergonomie.html` | note de conception HTML pour Ali (images dans `img/2026-10-04-panneau-unique/`) : les 7 demandes du panneau unique → décisions, avant / après, flux des cibles, ce qui n'a pas été vu, ce qui reste à décider |
| `docs/reference/` | format de `Power.log`, recherche HDT / Tier7 |
| `docs/journal/` | ce qui s'est décidé, séance par séance |
| `docs/archive/` | le dépôt Rust complet, en bundle git |
