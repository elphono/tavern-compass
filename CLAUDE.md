# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Vue d'ensemble

**Tavern Compass** (nom de code `BronzebeardHud`, nom affiché « Bronzebeard HUD » jusqu'au 2026-10-07) est un plugin
Hearthstone Deck Tracker (HDT) pour Battlegrounds : les compos à viser, des cadres sur les cartes de Bob, des aides pour
les choix et une jauge de puissance du board. Dépôt public `github.com/elphono/tavern-compass` (ex-`bg_ultimate_hud`),
licence MIT (le dossier local s'appelle encore `bg_ultimate_hud`). **Langue** : `README.md` et `ROADMAP.md` en anglais (le public), toute l'autre documentation en français,
code et commentaires en anglais.

Le plugin vise la parité avec Firestone et HSReplay-Tier7 ; la stack a été retenue le 2026-09-26 par l'étude
`docs/plans/2026-09-26-etude-stack.md` (critère unique d'Ali : atteindre ce résultat le plus vite possible).

**Retiré le 2026-10-06** : l'app Avalonia autonome (`BronzebeardHud.App`, `GameState`, `LogParser` et leurs tests), une
réécriture C# du tracker Rust `bg_treehudder` qui lisait `Power.log`, plus développée depuis le plugin. Elle reste dans
l'historique git (tag `archive/standalone-app`, dernier état avant son retrait). Le dépôt Rust vit dans son propre remote
privé, `github.com/elphono/bg_treehudder`. Le format annoté de `Power.log`, les plans de portage Rust → C# et la maquette
de l'ancien panneau (`docs/mock/`) ont été retirés avec elle.

## État du projet (au 2026-10-06)

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

- **Vérifier en jeu les deux correctifs du 2026-10-07** (`docs/journal/2026-10-07-jauge-adverse-cadres-coches.md`) : la
  rangée de l'adversaire — sur une partie entière, aucune ligne `opponent power …` n'avait de plateau (`seen=none` partout) ; la prochaine doit
  porter `seen=<tour>` en combat et, en taverne, contre un adversaire déjà affronté ; sinon, `read=[…]` dit à lui seul
  pourquoi (aucun héros, pas de plateau chez HDT, plateau vide ou d'un autre tour). Et, une case cochée, plus aucun cadre
  sur les cartes de Bob hors des guides cochés (ligne `tavern highlights=[…] … frames from ticked=[…]`) ; vu seulement dans
  la simulation (`--selftest`, `--scenario 5 --tick 3`).
- **Vérifier en jeu l'encart de puissance et le resize par + / −** (2026-10-06, `docs/journal/2026-10-06-encart-puissance-resize.md`,
  note HTML § 12) : l'encart sous le cadre (feux, halo, − à gauche, + à droite), le panneau qui suit N à chaque appui
  (ligne `targets n=… panel resized to …`), son bas gardé en bas d'écran, la boîte de la poignée gardée jusqu'à un appui ; la
  rangée de l'adversaire (ligne `opponent power …` : plateau, héros, tour vu, moyenne), en combat le plateau affronté, en
  taverne le dernier plateau vu du prochain adversaire. **Trois arbitrages à confirmer par Ali** : « avec notre board
  seulement » gardé plateau + main ; une case cochée garde N lignes (au moins toutes les cibles) ; la taille donnée par
  + / − oubliée à la partie suivante. Vus seulement dans la simulation (`--count`, `--opp-power`, `--play`).
- **Vérifier en jeu le filtre des tribus du lobby, la règle des cases et l'indicateur de puissance** (2026-10-06,
  `docs/journal/2026-10-06-tribus-cases-puissance.md`, note HTML § 11) : la ligne `lobby tribes=[…] read at …` (quand HDT a
  les tribus : jamais mesuré, seulement déduit), `lobby tribes=[…] (…): k/n guides playable; left out: …` ; plus aucune
  compo d'une tribu absente (liste, cadres, choix) ; une case cochée garde les compos en cours (« in progress ») ; la
  jauge rouge / jaune / verte / dorée sous la liste. Vus seulement dans la simulation (`--scenario 3|4|5`, `--power …`).
- **Vérifier en jeu le panneau unique « Compositions »** (2026-10-04, `docs/journal/2026-10-04-panneau-unique.md`) :
  liste des guides de HDT et couleurs des cibles, détail au clic, cadres sur les cartes de Bob, étiquettes des choix,
  tribus du lobby (bêtes), popup du guide au survol d'une ligne (et l'aperçu de carte au premier survol d'un ovale). Vu
  seulement dans la simulation (captures, `--selftest` ; les étiquettes des choix depuis le 2026-10-04, `--choice` ; le
  survol, `--hover`).
- **Vérifier en jeu le pont guides HDT ↔ compos Firestone et le masquage pendant un choix** (2026-10-04,
  `docs/journal/2026-10-04-panneau-unique.md` § « Stats Firestone → aides ») : le taux de recoupement réel (24 compos
  Firestone contre ≈ 23 guides de HDT) n'a **jamais été mesuré** — lire la ligne `bridge:` du journal d'HDT à la première
  partie ; le masquage des marqueurs et du panneau pendant un choix est une décision du pilote, **à confirmer par Ali**.
  Vus seulement dans la simulation, sur des compos Firestone synthétiques.
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
| Visibilité | dépôt GitHub **public** (privé du 2026-09-26 au 2026-10-06). Le 2026-10-07 (Ali) : licence MIT, nom « Tavern Compass », historique purgé de ses données personnelles (BattleTags, pseudo d'adversaire, numéros de compte, bundle Rust), nom affiché dans HDT changé sans toucher aux noms internes |

## Façon de travailler sur ce projet

- Ali teste en partie sous Windows ; la session **déploie elle-même** les DLL après chaque livraison
  (build Release depuis `main`, idéalement avec `HdtInstallDir`, copie, comparaison des SHA-1), puis
  Ali relance HDT. HDT ne recharge les plugins qu'à son démarrage (ou décocher / recocher le plugin).
- Diagnostic : le journal d'HDT (`/mnt/c/Users/elphono/AppData/Roaming/HearthstoneDeckTracker/Logs/hdt_log.txt`)
  porte une ligne `Bronzebeard HUD: …` par tour et par fonctionnalité ; une fonctionnalité qui lève
  est coupée seule par `FeatureGuard` et le dit une fois. Lire cette ligne **avant** de supposer une cause.
- **Avant de pousser** (la CI GitHub, `.github/workflows/ci.yml`, refait la même chose et construit en plus le plugin et la
  simulation contre HDT 1.55.6) : `dotnet format whitespace --folder --verify-no-changes .` (le style est celui du
  `.editorconfig`), `dotnet test -warnaserror` et les builds Release du plugin et de la simulation en `-warnaserror` : le
  code n'a aucun avertissement, un nouveau fait échouer la construction. `RepositoryHygieneTests` refuse tout BattleTag,
  pseudo de joueur ou numéro de compte réel dans un fichier du dépôt (les données de test sont inventées).
- **Historique réécrit le 2026-10-07** (purge des données personnelles, voir « Décisions ») : les hashes de commit cités
  dans les documents ont été recalculés ; un clone fait avant cette date est à refaire (`git clone`, ou `git fetch` puis
  `git reset --hard origin/main` si l'arbre est propre). L'ancienne app autonome reste atteignable par le tag
  `archive/standalone-app`.
- Retours constants d'Ali sur l'UI : aucun texte tronqué, chaque indication alignée sur la carte ou
  le héros qu'elle concerne, couleurs vives et distinctes, rien ne masque l'interface du jeu, ne pas
  dupliquer ce que le jeu ou HDT affichent déjà.

## Plugin Hearthstone Deck Tracker (depuis le 2026-09-26)

La parité avec Firestone et HSReplay-Tier7 passe désormais par un **plugin HDT** : HDT fournit
déjà l'overlay Battlegrounds gratuit de HSReplay (Bob's Buddy compris), et le plugin ajoute le reste.
Spec et plan : `docs/plans/2026-09-26-parite-tier7-{spec,plan}.md`. Le plugin **ne lit jamais la mémoire du jeu** : il n'utilise
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
  Les marqueurs attachés à une carte, un héros ou une tuile du classement ne bougent pas. Le panneau « Compositions » et
  son encart de puissance (dessous) sont un seul panneau pour `PanelMover` (même clé, même entrée de `layout.json`) : ils
  bougent, se redimensionnent et se cachent ensemble ; en mode déplacement, le cadre principal porte la bordure cyan
  (`PanelMover.Place(…, frame:)`) et le cadre pointillé entoure les deux.
- Redimensionner (même mode : poignée ◢ au coin bas-droit du panneau « Compositions », cadre
  pointillé cyan autour de la place donnée ; **pas de poignée sur Skip combat**, un bouton n'a rien à montrer en plus
  ou en moins). On donne de la **place** au contenu, jamais un zoom (Ali, 2026-10-04) : le panneau montre plus ou
  moins de guides (« 2 of 15 shown ») ou de sections du détail, au même corps de texte, donc le plancher de
  12 px tient. La taille est gardée à côté de la position, `"width"` et `"height"` en fractions de l'overlay,
  facultatifs ensemble : `{"left": 0.76, "top": 0.07, "width": 0.22, "height": 0.5}` ; un fichier sans taille se lit
  comme avant, et « Reset panel positions » rend aussi la taille. La hauteur gardée compte l'encart de puissance
  (2026-10-06) : une boîte choisie avant laisse ≈ 42 px de conception de moins à la liste. Minimum (`PanelFit.TargetMin*`) :
  la largeur par défaut (case, nom, six ovales), le titre + 1 ligne et l'encart. Une boîte plus petite que ce que le panneau montre toujours
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
  de l'overlay, et `CompGuideLayout` décide ce qui tient (`Fit` et `Sections`, avec la même tolérance).
- **Hauteur réglée par + / −** (Ali, 2026-10-06 : « avec move panel on détermine la taille et l'emplacement par défaut ; un
  appui sur les + ou − resize la fenêtre pour afficher les N meilleurs compos »). Hors mode déplacement, un panneau
  **jamais redimensionné par sa poignée** est toujours dimensionné sur son contenu : N lignes (`CompTargets.FitRows` :
  − n + ; avec une case cochée, au moins toutes les cibles), la barre de titre et l'encart ; « Reset panel positions »
  le remet dans ce cas. Un panneau **redimensionné par sa poignée** garde sa boîte (sa taille par défaut) jusqu'à un appui
  sur + ou − dans la partie, puis est dimensionné sur son contenu jusqu'à la partie suivante (`CompsPanel.Hide`) ou un
  changement de mode déplacement ; en mode déplacement on voit toujours la boîte que la poignée édite. Le détail d'un
  guide, dimensionné, prend la hauteur de toutes ses sections. Placement (`PanelGrowth.Place`, testé) : le panneau garde
  le haut de sa boîte et grandit vers le bas jusqu'à la ligne de l'or (`PanelFit.BottomLimit`) ; une boîte posée **sous**
  cette ligne (en bas de l'écran) garde son bas et grandit vers le haut ; sans place en dessous il monte jusqu'à la
  première zone du jeu ou le premier panneau au-dessus de lui (plateaux, classement, héros : `GuidePopupLayout.GameZones` ;
  Skip combat), jamais hors de l'écran ; si même là les N lignes ne tiennent pas, il montre ce qui tient (« k of n
  shown »), les **meilleures cibles par rang** (`CompGuideLayout.Fit` : jamais un guide qui n'est pas une cible à la place
  d'une cible laissée dehors), et se resserre sur ce qu'il montre. Le placement part toujours de la boîte du layout, jamais
  de la place du dernier redessin (pas de dérive). Mesuré dans la simulation en 1080p à la place par défaut : N = 1 →
  177 px, 2 → 247, 3 (deux tiers) → 339 (≈ 3 px de marge entre les plateaux et l'or), 4 → 3 lignes sur 4 ; trois cibles
  dans trois tiers → 2 lignes. Journal : `targets n=4 panel resized to (1181,682 488x339) anchor=bottom lines=3/4 shown,
  of 8` par appui.
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
  mesure et forme des données : `docs/journal/2026-10-04-comp-guides-hdt.md`. **Seuls les guides que le lobby peut jouer
  sont listés** (`LobbyGuides`, ci-dessous ; la liste gratuite de HDT n'est pas filtrée) ; tant que HDT n'a pas les tribus,
  une ligne grise « Lobby tribes unknown: every guide listed ». Titre : « Compositions », « k of n shown »
  quand des guides manquent, la source (« HDT free » / « Tier 7 »), « Meta ↗ », « n targets » (1 à 4, 3 par défaut,
  gardé dans `%LocalAppData%\BronzebeardHud\settings.json` : `{"schema": 1, "suggestedCompositions": 3}` ; dès qu'une
  compo est cochée il dit « k chosen ») ; une ligne dorée tant que HDT n'a pas de guides. **− et + sont dans l'encart de
  puissance**, sous le cadre (− à gauche, + à droite, depuis le 2026-10-06) : ils changent n et redimensionnent le panneau
  (ci-dessus) ; grisés et sans effet dès qu'une compo est cochée (`CompTargets.CountAdjustable`).
- **Cibles** (`CompTargetTracker`, `CompTargets.Choose`) : **une case cochée désigne la compo visée sans effacer ce qu'on
  construit** (Ali, 2026-10-04 : « une compo checkboxée est une compo vers laquelle on veut se diriger » ; 2026-10-06 :
  « quand je click sur une checkbox ça enlève d'autres compos […] celles que j'étais en train de jouer »). S'il y a des
  guides cochés (quatre au plus, ordre de coche) : eux d'abord (`TargetKind.Chosen`), puis les guides **en cours**
  (`InProgress` : deux cartes clés tenues, plateau + main, ou toutes celles d'un guide qui en a moins ; « in progress » sous
  leur nom), dans la limite de quatre cibles ; les paris (une seule carte clé) se taisent, et − n + (qui compte les paris)
  est grisé. Sur les cartes de Bob, seuls les guides cochés encadrent alors (2026-10-07, « Taverne » ci-dessous). Sinon, les plus probables d'après le plateau **et** la main (3 × carte clé, 2 × enabler, 1 × add-on,
  `CompGuideMatch`), jusqu'à n (− n +) ; à score égal, une cible du tour d'avant garde sa place (il faut un score plus
  haut pour la remplacer). Tout décocher rend les cibles automatiques. Une cible garde sa
  couleur tant qu'elle le reste (magenta, lime, bleu ciel, blanc), cases et couleurs sont oubliées à la partie suivante.
  Le tout passe par `CompTargets.Round`, qui ne prend que `LobbyGuides` : aucun guide d'une tribu absente n'est classé.
- **Tribus du lobby** (`LobbyGuides`, 2026-10-06 : 15 rondes sur 69 d'Ali avaient une cible d'une tribu absente, à cause
  d'une carte clé neutre tenue). Un guide est écarté si sa tribu principale n'est pas dans la partie (même si ses cartes
  clés sont neutres), ou si au moins la moitié de ses cartes clés ne peuvent pas y apparaître (tribus de HearthDb,
  `HdtEntityAdapter.CardTribes` : une carte à deux tribus apparaît si l'une est là, un amalgame toujours, une carte inconnue
  n'est jamais retenue contre un guide). Écarté : ni listé, ni cible, ni cadre, ni étiquette de choix, ni pivot. Tribus lues
  par `HdtEntityAdapter.LobbyTribeNames` (mémoire du jeu lue par HDT), redemandées une fois par seconde au plus tant
  qu'inconnues, puis gardées pour la partie ; **inconnues : rien n'est écarté**, et le panneau le dit. Une case cochée sur
  un guide que le lobby, une fois connu, ne joue pas est décochée (une ligne de journal).
- **Puissance du board** (`BoardPowerView`, `BoardPowerLevels`, 2026-10-06) : **dans un encart sous le cadre** (Ali, le
  soir : « on sort l'indicateur de force de compo pour en faire un petit encart en dessous du cadre principal […] un effet
  lumineux sur le composant feu rouge ; on l'encadre des + et − »), aussi large que le panneau, entre − et + : deux
  rangées, chacune quatre feux dans un boîtier sombre (rouge, jaune, vert, or ; le feu du palier allumé avec un halo de
  sa couleur, `BoardPowerLevels.Halo`, or profond pour shiny ; les autres éteints), un badge de la couleur du palier avec
  son signe et le pourcentage (« ▼ −33% », « ≈ +18% », « ▲ +58% », « ★ +117% », doré lumineux), puis les chiffres.
  **Rangée du joueur** : « Board 190 · hero avg 120 at turn 8 ». **Rangée de l'adversaire** (`OpponentPower`, même code de
  rendu) : son plateau contre la moyenne de **son** héros (jamais la courbe du joueur) — en combat le plateau affronté, tel
  qu'HDT le fige au début du combat (`GameV2.GetBattlegroundsBoardStateFor`, héros en jeu contrôlé par `game.Opponent`) :
  « Opp. 160 · their hero avg 143 at turn 8 » ; HDT 1.58.9 range ce plateau sous le `PLAYER_ID` et n'y garde **que les
  sbires, jamais le héros** (`BattlegroundsBoardState.SnapshotCurrentBoard`, décompilé) : le héros de référence est pris
  dans les entités, celui du classement (`OpponentBoards` : `Pick`, `Read`, `Leaderboard` ; contre un fantôme, le héros en
  jeu est Kel'Thuzad avec le `PLAYER_ID` du mort). Jusqu'au 2026-10-07 le plugin cherchait le héros dans le plateau et
  n'en a lu aucun (`docs/journal/2026-10-07-jauge-adverse-cadres-coches.md`) ; **pas encore vu en jeu**. En taverne le dernier plateau vu du prochain adversaire
  (`NEXT_OPPONENT_PLAYER_ID`), contre la moyenne de son héros au tour où il a été vu : « Next opp. 70 at turn 5 · their
  hero avg 50 » ; sans donnée, gris et la raison (« Next opp. – not known yet », « Next opp. Rakanishu – not fought yet »,
  « Opp. – board not read yet », « no curve for Rakanishu »). Chaque rangée a son garde-fou (`warband-curve`,
  `opponent-power`) : une rangée qui lève est retirée seule. L'encart est caché avec le panneau pendant un choix.
  Paliers en tours de croissance de la courbe (×1,8 par tour, mesuré) : < −25 %
  rouge, jusqu'à +34 % jaune, jusqu'à +80 % vert, au-delà shiny. Sans couleur (feux gris, aucun halo, « – », raison écrite) : pas
  de courbe, pas de moyenne au tour, moyenne < 12 (« too early »), courbe qui retombe à ce tour (« curve falls after turn
  16 »), héros sous 100 parties (« few games (78) »). Global seulement : Firestone n'a pas de courbe par compo.
  Liste dans l'ordre de HDT par tier (S → D, barres aux dégradés de HDT), les cibles en tête de leur tier ; une ligne =
  case, nom (deux lignes au besoin, jamais coupé ; couleur et gras d'une cible, blanc si quelque chose est tenu, gris
  sinon), les **cartes clés** seules en ovales (anneau vert + ✓ si tenues, tier en badge ; au-delà de six : cinq et
  « +k »). Une cible porte en plus un liseré de 3 px, une teinte et une pastille de rang dans sa couleur. Ce qui ne tient
  pas est omis, les cibles en dernier (`CompGuideLayout.Fit`). Survoler un ovale montre la carte entière (infobulle de
  HDT ; `CardImages.FullCard` est une boîte de la taille de la carte, car HDT place l'infobulle d'après sa taille mesurée
  dès l'ajout, avant que l'image ne charge : une `Image` sans source mesure 0 × 0 et l'aperçu tombait sur le panneau au
  premier survol — vu dans la simulation le 2026-10-04, pas encore en jeu).
- **Survol d'une ligne** (`GuidePopup`, Ali, 2026-10-04 : « le guide complet, comme dans HDT, en popup au survol ») :
  après 250 ms sur une ligne de la liste, tout le guide dans une boîte à part, hors de la boîte du panneau — nom (couleur
  de la cible, sinon blanc), badges de tier et de difficulté, la ligne de contexte Firestone d'un guide ponté (comme le
  détail, ci-dessous), les six sections dans l'ordre de HDT (mêmes constructeurs
  que le détail : `GuideView`), « k of n sections » si tout ne tient pas. Place (`GuidePopupLayout.Place`, testée) :
  au-dessus du panneau, à droite des plateaux (1080p : x = 1452, bas à 9 px du panneau ; au-dessus de Skip combat
  quand il est là), bord droit sur celui du panneau quand rien ne gêne, poussé de côté sinon ; jamais sur une zone du jeu,
  le panneau, Skip combat ni la place d'un aperçu de carte du panneau ; en dessous si le panneau est en haut ; rien s'il
  n'y a de place nulle part (`guide popup: no room`, une fois par partie). **Pas une infobulle de HDT** : HDT n'a qu'un
  emplacement d'infobulle pour tout l'overlay, une infobulle sur la ligne aurait empêché l'aperçu de carte de ses ovales ;
  le popup est un élément du canvas, rien n'y est survolable ni cliquable, et l'aperçu d'un ovale de la ligne peut se
  montrer en même temps. Événements de la sonde de HDT et de WPF traités pareil (`GuideHover`) : une deuxième entrée ne
  change rien, une sortie alors que le curseur est encore dans le rectangle de la ligne est ignorée. Caché : sortie de
  la ligne, clic qui ouvre le détail, changement de phase, mode déplacement, panneau caché, choix ouvert. Journal :
  `guide popup <nom> shown at (x,y w×h) sections=k/n` par affichage. Mesure et décision :
  `docs/journal/2026-10-04-panneau-unique.md` § « Survol ».
- **Détail** (clic sur un nom ou un ovale, comme dans HDT) : « ← All comp guides » à la place du titre, case, nom, badges
  de tier et de difficulté (couleurs de HDT, `CompGuideDifficulty`), la ligne de contexte Firestone d'un guide ponté
  (`TargetContext`, `GuideView.Context` : « ≈ 3,5 with your hero (23) · final turn ≈ 13 · 5 top boards », 12 px, gris ;
  omise sans pont), puis HOW TO PLAY (première ligne, noms de cartes en
  gras), CORE CARDS, ADDON CARDS, WHEN TO COMMIT (une pastille par ligne), COMMON ENABLERS, PIVOTS (`GuidePivots`). Une
  section qui ne tient pas est omise entière (« k of n sections », `CompGuideLayout.Sections`) : à la place par défaut
  en 1080p, deux ou trois tiennent (deux pour un guide ponté : la ligne de contexte prend la place d'une) ; agrandir le
  panneau, ou survoler la ligne (popup ci-dessous), pour tout voir.
- **Taverne** (`TavernMarkers`, `TavernHighlights.For(Bob, cibles, pont)`) : carte clé d'une cible → cadre plein, enabler ou
  add-on → pointillés, dans la couleur de la cible (carte clé d'abord, puis l'ordre des cibles), étiquette « core Nom
  k/N », « enabler Nom » ou « + Nom » ; avec le pont, une carte qu'aucun guide de cible ne nomme mais qui est sur ≥ 2
  plateaux finaux de la compo Firestone d'une cible → pointillés « + Nom 3/5 », après tous les rôles ; le ◇ au-dessus
  de chaque sbire l'épingle (cadre blanc). **Dès qu'un guide est coché, seuls les guides cochés encadrent** (Ali,
  2026-10-07 : « quand on sélectionne des compos vers lesquelles on veut tendre, on ne devrait plus surligner aucun autre
  sbire dans le shop » ; `TavernHighlights.Framing`) : une cible « in progress » reste listée dans le panneau mais n'encadre
  plus rien, ni par ses rôles ni par les plateaux de sa compo pontée, et n'est plus nommée sous l'étiquette d'une cochée ;
  sans case cochée, rien ne change. Les étiquettes des choix et les ◇ n'en dépendent pas. **Choix** (découverte, Dark Gift, trinket : `ChoiceAdvisor`, avec le pont) :
  carte d'une cible → étiquette dans sa couleur (« ★ core Nom 2/3→3/3 », « + Nom », suivies de « · 4/5 boards » quand la
  compo pontée de la cible a des plateaux finaux) ; sinon une carte qu'aucune liste de la cible ne nomme mais sur ≥ 2
  plateaux de sa compo pontée (« + Nom 3/5 boards », dans sa couleur) ; sinon une carte clé d'un guide vers lequel une
  cible peut pivoter (« pivot → Nom (S) », neutre) ; sinon le guide jouable dans le lobby dont elle est carte clé
  (« core Nom (S) », neutre) ; sinon « — ». Les tribus du lobby sont lues par valeur (`GuideTribes.NameOrEnum` : 20 est
  à la fois BEAST et PET). **Pendant un choix** ouvert en taverne (toute sorte que `ChoiceClassifier` distingue de
  `None`, y compris sans disposition connue), cadres, étiquettes et ◇ des cartes de Bob, le panneau « Compositions » et
  son popup sont retirés de l'écran (`ChoiceCover`, `TavernMarkers.Suspend`, `CompsPanel.Suspend`), puis remis tels quels
  à sa fermeture, sans recalcul (le panneau : les mêmes éléments si rien n'a changé). Raison, mesurée dans la simulation :
  les ◇ tombaient dans les cartes d'un Dark Gift (un clic épinglait au lieu de choisir) et le panneau, à sa place par
  défaut en 1080p, couvrait le bas de la 3e option (302 × 43 px en découverte, 359 × 162 px en Dark Gift). Décision du
  pilote, réversible, **à confirmer en jeu par Ali** ; garde-fou `choice-cover` (s'il tombe, tout est rétabli).
- **Journal** : `comp guides loaded from HDT (…)` à chaque nouvelle liste, `… comp guides: none from HDT (state …)` tant
  que HDT n'a rien ; `lobby tribes=[…] read at hero selection` (ou `shop turn n`) quand les tribus sont connues, `lobby
  tribes unknown at shop turn n: …` une fois si la taverne s'ouvre sans elles ; `lobby tribes=[…] (shop turn n): k/n guides
  playable; left out: Guide (no BEAST), Autre (key cards X, Y: no QUILBOAR)` (ou `lobby tribes unknown (…)`) à chaque
  nouvelle liste ou nouveau lobby ; `unticked Guide/20: no BEAST` ; `bridge: Guide → compo (k/N keys, m cards, n games); Autre → no match (k/n guides bridged, against m
  compositions; Firestone ok)` à chaque recalcul du pont ; `comps round=… source=… comps=… board=… hand=… targets=[1. Nom
  #couleur ★k/N ticked; 2. Nom #couleur ★k/N in progress; …]` à la fin de chaque tour de taverne (`comps` = guides du
  lobby) ; `warband round=… hero=… Board … · +18% power=even` (`power=none (too early)`) ; `tavern highlights=[carte:core|enabler|addon:guide, carte:boards
  3/5:guide, …] targets=[…]` quand ils changent (`TavernHighlights.Summary`, `LogLine`), suivi de `frames from
  ticked=[…]` quand une case cochée réduit les cadres aux guides cochés ; `choice kind=…` par choix (raison et
  évidence de chaque étiquette) ; `choice open: markers and panel hidden` / `choice closed: restored` à chaque transition ;
  `comp detail id=… sections=k of n` à chaque détail ouvert ; `ticked guides=[…]` ; `targets n=4 panel resized to (x,y w×h)
  anchor=top|bottom lines=k/N shown, of m` à chaque appui sur + ou − (hors mode déplacement) ; `power inset at (x,y w×h): you
  ▲ +58% (ahead) · opp ≈ +12% (even)` quand ce que montre l'encart change (`– (none: too early)` sans donnée, `off` pour une
  rangée coupée) ; `opponent power scope=combat|next|none id=… hero=… (yours …) turn=… seen=… board=… (k minions)
  read=[heroes 12,40 asked 40 → turn 8, 7 entities, 7 minions] · Opp. … power=…` quand la jauge de l'adversaire change
  (`read` : les entités héros de ce `PLAYER_ID`, celle demandée à HDT, puis `no snapshot` ou ce que contient le plateau ;
  `no hero entity` si aucun héros ne le porte).
- Les compositions de Firestone (`CompService`, `TavernAdvisor`, `CompositionRows`, `CompDetail`, `CompTransitions`,
  `MinionLineups`) restent chargées et dans le code ; elles ne sont plus affichées en liste (hors ligne « comp ≈ » de
  l'encart des héros), mais orientent les aides par le pont ci-dessous. Une erreur d'un fichier de `manual\` est dite une
  fois par `compositions data: …` (avertissement). `docs/mock/` est la maquette de l'ancien panneau Firestone.
- **Pont guides HDT ↔ compos Firestone** (`GuideBridge`, 2026-10-04 ; branché par `Plugin.UpdateBridge`, garde-fou
  `guide-bridge`). Deux nomenclatures sans clé commune, rapprochées par les
  cartes : core ∪ add-on du guide contre tout ce que Firestone donne de la compo (listes, plateau de référence, plateaux
  finaux, dorées comprises). Pont seulement si **≥ 2 cartes partagées ET ≥ la moitié des cartes clés du guide** ; meilleure
  compo = plus de clés, puis plus de cartes, puis plus de parties, puis l'id ; sinon aucun pont. Deux variantes d'un guide
  peuvent partager une compo. Recalculé à chaque nouvelle liste de guides de HDT et à chaque chargement des compos
  (`CompService.Version`), contre toutes les compos connues (Firestone, et `manual\` s'il y en a) ; sans guides de HDT, pas
  de pont. Ce qu'il ouvre : les aides de choix et les cadres ci-dessus, et `TargetContext` (« ≈ 3,4 with your hero (23) ·
  final turn ≈ 13 · 5 top boards », jamais la place moyenne de la compo ; le morceau héros vient de `HeroCompAffinity`,
  recalculé quand le héros joué ou les compos changent). Sans `bridge` (null, ou le garde-fou tombé), les textes d'avant
  mot pour mot, pivots compris.
- Bouton « Skip combat » (jaune, en combat seulement, panneau déplaçable `skip-combat`) : tue Hearthstone et
  le fait relancer **par Battle.net** (`--exec="launch WTCG"`, redemandé chaque seconde : ≈ 7 s mesurées),
  jamais par son exécutable (connexion refusée, mesuré) ; sans Battle.net, rien n'est tué. Un clic par combat,
  lignes `Bronzebeard HUD: skip combat …` (parent, commande, demandes, nouveau pid, vivant 3 s après).
- Sous WSL, on vérifie les tests et le build. Le chargement par HDT et les événements réels ne se vérifient que
  sous Windows, avec HDT installé. **Exception : la simulation** `tools/BronzebeardHud.Harness/` (README) fait tourner
  les vrais panneaux (`PanelMover`, « Compositions » et son popup de survol, cadres sur les cartes de Bob, Skip combat, étiquettes des choix) dans une fenêtre Windows ordinaire, sans
  HDT ni partie, avec des données synthétiques (dont deux compos Firestone inventées pour le pont, un contre-exemple
  « no match », un lobby de cinq tribus par scénario — `--scenario 3` sans morts-vivants, `4` lobby inconnu, `5` cases —
  et une courbe de héros inventée pour la jauge, `--power behind|even|ahead|shiny|none|early`, une autre pour le héros
  adverse, `--opp-power behind|even|ahead|shiny|none|next|unseen` ; `--count n` clique − / + jusqu'à n, `--play` coupe le
  mode déplacement) ; `launch.sh` la compile sous WSL et la lance côté Windows, `--selftest`
  la vérifie sans personne au clavier, `--screenshot` écrit une capture que la session peut regarder. Elle ne simule
  pas la couche d'HDT (clics transparents au-dessus du jeu, survol sondé à 60 Hz) ni la lecture d'HDT (plateau adverse,
  `NEXT_OPPONENT_PLAYER_ID` : faits synthétiques) : un défaut qui y vivrait ne s'y voit pas.
- Le dépôt est **public** : aucune donnée réelle de Firestone ni de HSReplay n'y entre, et aucun pseudo, BattleTag ni
  identifiant de compte réel (un test refuse ceux qu'on a déjà purgés) ; les tests utilisent des données synthétiques.

## Docs

| Dossier | Contenu |
|---|---|
| `README.md`, `ROADMAP.md` | la vitrine publique (anglais) : ce que fait le plugin, comment l'installer, où en est le projet |
| `docs/plans/` | 2026-09-26 : étude de stack, spec et plan du plugin HDT |
| `docs/plans/2026-10-04-panneau-unique-ergonomie.html` | note de conception HTML pour Ali (images dans `img/2026-10-04-panneau-unique/`, `img/2026-10-06-tribus-cases-puissance/` et `img/2026-10-06-encart-puissance/`) : les 7 demandes du panneau unique → décisions, avant / après, flux des cibles, ce qui n'a pas été vu, ce qui reste à décider ; § 11 : tribus du lobby, cases à cocher, puissance du board (2026-10-06) ; § 12 : encart de puissance, + / − qui redimensionnent, jauge de l'adversaire (2026-10-06, soir) |
| `docs/reference/` | recherche HDT / Tier7 |
| `docs/journal/` | ce qui s'est décidé, séance par séance |
