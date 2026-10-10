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

## État du projet (au 2026-10-08)

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

Ce qui reste ouvert **vit dans les issues GitHub** (depuis le 2026-10-08, à la demande d'Ali), rangées par milestone :
c'est le siège unique du travail en cours, ne pas le recopier ici. `gh issue list -R elphono/tavern-compass` ; une issue se
ferme par le commit qui la règle (`Closes #n`), une case cochée dans son corps quand un point est vu en jeu.

| Milestone | Issues | Contenu |
|---|---|---|
| Vérifier en jeu | #2 à #8 | tout ce qui n'est vu que dans la simulation (card-stats et tranche à chaud, à déployer d'abord ; jauge adverse ; encart et + / − ; tribus et cases ; panneau unique, pont, masquage ; phases 5 et 6) et les arbitrages d'Ali (label `arbitrage`) |
| Stats multi-sources | #9 à #12 | chantiers b (socle), c (nomi.gg), d (composants 3 à 9), f (nos propres données) |
| Idées à explorer | #1, #14 | l'anti-danse du board (idée gardée par Ali), la simulation de la couche d'overlay d'HDT |
| — | #13 | releases empaquetées |

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
| Stats nomi.gg | **accord du propriétaire du site**, obtenu par Ali le 2026-10-08, tel quel sur les termes de la demande : l'analyse de patch (`nomi.gg/patch/analysis/<patch>.json`) peut être collectée automatiquement, dans les limites que le projet s'est fixées (note `docs/plans/2026-10-08-stats-multi-sources.html` § 10 : une requête conditionnelle par jour au plus, `User-Agent` qui nomme le plugin, crédit à l'écran, aucune redistribution ni entraînement de modèle). Le code de Nomi's Kitchen (licence « MIT NON-AI ») se lit pour en comprendre les idées, il ne se copie pas (exception : son mod anti-danse, ligne « Mod anti-danse de Nomi ») |
| Mod anti-danse de Nomi | 2026-10-10 : accord personnel du développeur de Nomi's Kitchen, obtenu par Ali, malgré la licence MIT NON-AI : analyse du mod `com.community.hs.NomiCantDance` par décompilation et désobfuscation, et reprise de son code, autorisées ; tout code repris est marqué à la source (commentaire d'origine en anglais : `Taken from NomiCantDance (Nomi's Kitchen), with its author's permission, 2026-10-10`) parce que le dépôt est public et sous MIT seule ; on préfère réécrire quand c'est aussi simple, on reprend tel quel ce que la décompilation montre plus juste que notre version |
| Notre mod anti-danse | 2026-10-10 (Ali) : **observer d'abord** une danse réelle sans le mod (journal `[Zone]`, `tools/nodance-observe.py`) ; le mod reste **non installé** ; publication **discrète** (`README.md` et `ROADMAP.md` n'en parlent pas, l'issue #1 reste ouverte tant que rien n'est vu en jeu) |
| Simulateur npm `simulate-bgs-battle` | usage personnel, autorisé ; inutile tant que Bob's Buddy (HDT) fait le travail |
| MMR des adversaires | gardé tel quel. Le leaderboard EU s'arrête à 8 000 ; Ali est à ≈ 6 840 (région EU mesurée) ; plage par défaut 8 000 – 8 050 |
| Arbitrages du 2026-10-08 | Les cibles se classent sur **plateau + main** ; une case cochée garde **N lignes, au moins toutes les cibles** ; la taille donnée par + / − **revient à celle de la poignée** à la partie suivante ; la ligne « comp ≈ » de l'encart des héros est **retirée** (17 parties en médiane) ; le bilan par adversaire est parti avec le panneau Combats (2026-09-27) ; valeur d'une carte : **moins de bruit**, le seuil de 200 parties reste (≈ une carte sur deux sans valeur, mesuré) |
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
  `.editorconfig`), `dotnet test -warnaserror`, `python3 tools/test_nodance_observe.py` et les builds Release du plugin, de la simulation et de la CLI d'inspection
  en `-warnaserror` (juger chaque étape sur son **code de sortie** : une sortie filtrée par `| tail` a laissé passer un
  build cassé le 2026-10-08) : le
  code n'a aucun avertissement, un nouveau fait échouer la construction. `RepositoryHygieneTests` refuse tout BattleTag,
  pseudo de joueur ou numéro de compte réel dans un fichier du dépôt (les données de test sont inventées), et tout fichier de
  stats réel (2026-10-08 : un `.gz`, un `.json` de plus de 100 Ko, ou portant un champ propre aux serveurs de Firestone ou de
  nomi.gg).
- **Release** (2026-10-08, issue #13) : la version vit dans `Directory.Build.props` (`<Version>`, que le plugin lit dans son
  assembly) ; `tools/release.sh <app-version HDT installé>` refuse un arbre sale ou un tag existant, refait les contrôles de
  la CI, construit contre l'HDT installé et écrit `out/TavernCompass-v<version>-hdt-<hdt>.zip` ; la release GitHub se crée
  ensuite (la commande est imprimée, en brouillon). Monter `<Version>` avant la suivante.
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
  **Contenu** (2026-10-10, composant 3, issue #11) : une ligne consolidée (`HeroConsensus.For`, vue de la tranche du
  joueur : fichiers de héros du lobby et nomi.gg) — tier, « 3.41 · 4,102 games » (l'effectif remplace le taux de choix, décision 7),
  ou « 2.7 ↔ 3.8 contested » en ambre (décision 6, jamais la moyenne des deux) — puis ses sources (« FS 25% + nomi.gg », le crédit
  de nomi.gg à l'écran), puis top 4 / 1er de la première source. Sans vue, ou sous 10 parties partout, les lignes par source
  d'avant. Le « pourquoi » (composant 8) va au journal : `hero pick why=[H consensus 3.41 (4102 games) ← FS 25% 3.42 (4051) ·
  nomi.gg 3.10+0.30 (51, ×0.5) · pick 18%; …]`. Simulation : `--screenshot --heroes --play`.
- Aucun texte du plugin sous 12 px en 1080p (`PanelTypography`) et aucun `Viewbox` : ce qui ne tient pas est
  omis, jamais rétréci (MMR des
  adversaires : le rang disparaît, la cote reste). Un test lit les sources du plugin et y refuse `Viewbox` et
  `FontSize = <nombre>`.
- **Panneau « Compositions »** (`CompsPanel`, un seul panneau depuis le 2026-10-04 à la place de « Target compositions »
  et « HDT comp guides » : `docs/journal/2026-10-04-panneau-unique.md`), **dès la sélection du héros** (Ali, 2026-10-08 : « ça aide d'avoir les
  compos » ; aucune carte tenue, la liste du lobby par tier et les cases à cocher, qui restent cochées en taverne ; à la
  place par défaut il recouvre les encarts des héros de droite, 1 ou 2 selon leur nombre, mesuré), en taverne et en
  combat, par défaut sous le
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
  clés sont neutres), ou si au moins la moitié de ses cartes clés ne peuvent pas y apparaître, ou si une carte clé manque **et** au moins la moitié de ses add-ons (2026-10-10, Ali : Ménagerie sans quilboars ni le scaling des sorts, « quasi injouable » ; mesuré sur les 3 guides dont le journal garde les cartes, contre 18 lobbies : seule Ménagerie change, dans 8 lobbies) (tribus de HearthDb,
  `HdtEntityAdapter.CardTribes` : une carte à deux tribus apparaît si l'une est là, un amalgame toujours, une carte inconnue
  n'est jamais retenue contre un guide). Écarté : ni listé, ni cible, ni cadre, ni étiquette de choix, ni pivot. **Une carte clé
  d'une tribu absente dans un guide gardé** (2026-10-08 : un quilboar — « huran » en français — parmi les cartes clés de
  Menagerie, sans quilboars dans le lobby ; Menagerie n'a pas de tribu principale et n'a qu'une carte clé sur quatre
  impossible) : retirée des ovales de la ligne, grisée et barrée dans le détail et le popup (`LobbyGuides.CannotShowUp`,
  `CardImages.Unavailable`, décision d'Ali). Tribus lues
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
  n'en a lu aucun (`docs/journal/2026-10-07-jauge-adverse-cadres-coches.md`) ; **pas encore vu en jeu**. En fin de
  combat, HDT n'a plus de héros adverse en jeu pendant 2 à 5 s (mesuré, 28 combats sur 28) : la rangée garde l'adversaire
  du combat et sa jauge tels quels jusqu'à la taverne (Ali, 2026-10-07 ; `CombatOpponentKeeper`, oublié en taverne, à un
  autre tour, ou remplacé par un autre adversaire trouvé ; journal `id=3 (kept)`). En taverne le dernier plateau vu du prochain adversaire
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
  n'y a de place nulle part (`guide popup: no room`, une fois par partie). **À côté du panneau** (2026-10-08, issue #15) : quand ni
  au-dessus ni en dessous ne tiennent le popup entier, il prend la place la plus proche du panneau sur toute la hauteur, à
  côté de lui, si elle en montre plus (disposition d'Ali, panneau sur toute la hauteur à gauche : 1 section sur 6 en dessous,
  le popup entier à droite des plateaux). **Pas une infobulle de HDT** : HDT n'a qu'un
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
  `None`, y compris sans disposition connue), cadres, étiquettes et ◇ des cartes de Bob sont retirés de l'écran
  (`ChoiceCover`, `TavernMarkers.Suspend`), puis remis tels quels à sa fermeture, sans recalcul : les ◇ tombaient dans les
  cartes d'un Dark Gift (un clic épinglait au lieu de choisir). **Le panneau « Compositions » reste toujours affiché**
  (Ali, 2026-10-08 : « le panneau simplement toujours être visible », découverte, Dark Gift et trinkets compris ; il était
  retiré depuis le 2026-10-04). À sa place par défaut en 1080p, il couvre le bas de la 3e option (302 × 53 px en
  découverte, 359 × 172 px en Dark Gift, mesuré dans la simulation) : c'est à la disposition choisie par le joueur de
  l'éviter. Garde-fou `choice-cover` (s'il tombe, les marqueurs sont rétablis).
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
  évidence de chaque étiquette) ; `choice open: markers hidden, panel kept` / `choice closed: markers restored` à chaque transition ;
  `comp detail id=… sections=k of n` à chaque détail ouvert ; `ticked guides=[…]` ; `targets n=4 panel resized to (x,y w×h)
  anchor=top|bottom lines=k/N shown, of m` à chaque appui sur + ou − (hors mode déplacement) ; `power inset at (x,y w×h): you
  ▲ +58% (ahead) · opp ≈ +12% (even)` quand ce que montre l'encart change (`– (none: too early)` sans donnée, `off` pour une
  rangée coupée) ; `opponent power scope=combat|next|none id=…[ (kept)] hero=… (yours …) turn=… seen=… board=… (k minions)
  read=[heroes 12,40 asked 40 → turn 8, 7 entities, 7 minions] · Opp. … power=…` quand la jauge de l'adversaire change
  (`read` : les entités héros de ce `PLAYER_ID`, celle demandée à HDT, puis `no snapshot` ou ce que contient le plateau ;
  `no hero entity` si aucun héros ne le porte).
- **card-stats de Firestone** (2026-10-08, chantier a de `docs/plans/2026-10-08-stats-multi-sources.html`, plan
  `docs/plans/2026-10-08-chantier-a-card-stats-plan.md`) : la première source au format commun (`StatProvenance` : source,
  URL, dates, fenêtre, tranche, patch ; `CardStatsFile`, un fichier par tranche, `firestone-card-stats-mmr-25-last-patch.json`,
  ≈ 0,6 Mo, même règle quotidienne que les héros ; `CardStatsRefresh` : une instance par tranche, jamais le fichier d'une
  autre tranche à l'écran). **Valeur d'une carte à ce tour** (`CardTurnValue`) : sa place moyenne quand elle est jouée à
  ce tour **contre la moyenne de toutes les cartes jouées à ce tour** (pondérée par l'effectif), jamais contre les parties
  où elle ne l'est pas — mesuré le 2026-10-08, celles-là sont pires pour 320 cartes sur 320 au tour 6 : jouer va avec
  survivre. Rien sous le bruit : 200 parties au moins, écart ≥ max(0,1 ; 2 × 2,3 / √n) (points de départ, décision 8).
  Montrée sur la **dernière ligne libre** de l'étiquette d'une carte de Bob (« t6 ▲ 3.6 vs 3.9 » ; jamais à la place d'un
  rôle ; une carte qui n'a que sa valeur : étiquette neutre, sans cadre) et **à la place du « — »** d'un choix
  (`ChoiceReason.CardValue`, après cible, plateaux, pivot et guide ; jamais pour un trinket). Corrélation, pas cause :
  jamais « achète ». Garde-fou `card-stats` ; lignes `data card-stats mmr-25 last-patch: …` et `tavern values turn=6
  bracket=mmr-25 [id:t6 ▲ 3.6 vs 3.9 (400), …]`. Les tranches publiées : `mmr-100`, 50, 25, 10, 1 (médiane par carte et par
  tour, tours 3–10 : 191, 143, 118, 92 jouées). **Tranche changeable à chaud** (Ali, 2026-10-08 : « on devrait pouvoir
  hot-swap dans l'overlay ») : un bouton « top 25% » dans la barre de titre du panneau « Compositions », un clic → la
  tranche suivante (`BracketChoice` : 100 → 50 → 25 → 10 → 1 → 100) pour le reste de la partie ; stats de héros, jauge,
  trinkets et card-stats la suivent (tous lisent `StatsService.Bracket`) ; oubliée à la partie suivante ; ligne `bracket
  top 10% chosen in the overlay (was top 25%, rating …)`. Vu seulement dans la simulation (`--card-values`).
- **Cartes du début de partie** (2026-10-08, Ali : « les meilleures cartes en T1/T2/T3… pour savoir quelles cartes on vise
  quand on level up en early » ; « ceux de notre rang et celui d'au-dessus » ; « au tour actuel » ; section **et** popup,
  pour comparer) : `EarlyCards.For` (fonction pure, testée, 9 mutations) prend, parmi les sbires du lobby
  (`EarlyCards.Pool` : `HearthDb.Cards.BaconPoolMinions`, tier par HDT, règle d'apparition de `LobbyGuides`), ceux du
  **tier de taverne du joueur** (`PLAYER_TECH_LEVEL` de son héros) **et du suivant** que card-stats dit meilleurs que
  toutes les cartes jouées **à ce tour** (`CardTurnValue` : 200 parties, bruit), 4 par tier au plus, les meilleurs
  d'abord ; aucun plafond de tier (une anomalie peut amener des sbires de tier 7, c'est le pool qui décide). Jusqu'au
  **tour 6** ; à la sélection du héros, tier 1 et tour 1. Section « EARLY · turn n » sous la barre de titre du panneau
  (« T2 », « T3 next », leurs ovales, teinte sarcelle) ; survolée, le popup des guides montre chaque carte, son nom et
  « 3.5 vs 4.0 (400) ». Elle prend de la place à la liste des guides en début de partie (simulation, taille par défaut :
  1 guide sur 8 restant). Garde-fou `card-stats` ; ligne `early cards turn=3 tier=2 [2: id 3.4 vs 3.8 (400), …; 3 next:
  …]` quand elles changent. Simulation : `--card-values --hover early` (pool et tier inventés, fixes). **Pas encore vu en
  jeu.**
- **Socle des sources** (2026-10-08, chantier b, plan `docs/plans/2026-10-08-chantier-b-socle-plan.md`, issue #9) :
  `source` est une liste ouverte (un fichier d'une source inconnue se charge, affiché sous son nom ; « FS », « HSR » pour
  les connues, `StatsSources.Label`) ; chaque fichier de stats expose sa `Provenance` au format commun ;
  `SourceSnapshot.Of(fichier)` le traduit en chiffres (`StatRecord` : genre, sujet, mesure, valeur, effectif, unité) ;
  `StatsConsolidation.Consolidate` (fonction pure, § 6.2 de la note) aligne, décote de moitié hors tranche du joueur,
  rappelle de 30 parties vers 4,5 et juge : `Single`, `Consensus`, `Contested` (deux intervalles x ± 2σ/√n disjoints),
  `Apart` (sous 10 parties, ou sans fenêtre). **Aucune aide ne lit encore la vue** (composant 3, chantier d) : le plugin
  la calcule quand une source change et l'écrit au journal, `stats view bracket=mmr-25 sources=[…] · heroes 116 (116
  single, …) · trinkets … · cards 783 in 9866 figures (…)`, garde-fou `stats-view`. Un garde-fou par source :
  `data-firestone` (fichiers de Firestone) et `data-manual` (`stats\manual\`, lu au démarrage puis une fois par partie) à
  la place de `data-refresh`. Un seul fetcher et un seul cache, créés par le plugin et passés aux services. CLI
  `tools/BronzebeardHud.Inspect` (README) : lit le cache sans réseau ni écriture ; mesuré le 2026-10-08, un écart de
  **population** suffit à dire « contested » (top 25 % contre tous : 20 héros sur 116, +0,16 place en moyenne) — la
  décote ne corrige pas un biais, à revoir avec nomi.gg.
- **nomi.gg** (2026-10-08, chantier c, issue #10) : `NomiCache` lit `/patch/data/latest.json` puis
  `/patch/analysis/<patch>.json` en requêtes conditionnelles, **une tentative par jour au plus, redémarrages d'HDT et
  échecs compris** (date de la tentative écrite dans `stats\nomi-state.json` **avant** la première requête ; le cache de
  Firestone, lui, redemande à chaque démarrage) ; un patch clos n'est plus jamais demandé ; le nom du patch, venu du
  réseau, ne passe que s'il n'a que des chiffres et des points (il nomme une URL et un fichier). `NomiAnalysis` ne garde
  que ce que lira le chantier d : héros, tribus avant / après, buffs et nerfs, trinkets gagnants et perdants, médianes de
  montée de tier ; ni noms, ni plateaux, ni cartes. Fichier `stats\nomi-patch-analysis-<patch>.json`. `User-Agent` de
  toutes les requêtes : `TavernCompass/<version> (+https://github.com/elphono/tavern-compass)`. Service `NomiService`,
  garde-fou `data-nomi`, ligne `data nomi.gg patch analysis: downloaded|unchanged (304)|cached|FAILED … (patch 36.6.3)` ;
  la source entre dans la vue consolidée (`stats view`). **Rien ne s'affiche encore** : le crédit « data: nomi.gg »
  viendra avec le premier chiffre montré (chantier d). Mesuré le 2026-10-08 (CLI, section 4) : nomi.gg place ses héros
  ≈ 0,3 place mieux que Firestone, systématiquement (population de joueurs volontaires, place moyenne 3,78) ; contre le
  top 25 %, 8 héros sur 58 « contested ». **Recentrage** (Ali, 2026-10-10) : chaque source est recentrée sur sa propre moyenne (`SourceSnapshot.Means` : moyenne des héros pondérée par les parties ; pour les trinkets de nomi.gg, la moyenne de chaque sorte sur tous les choix, `NomiTrinketKind`, jamais les seuls gagnants et perdants), décalée sur celle de la source de référence (celle de la tranche du joueur, sinon la plus grosse), qui sert aussi de moyenne de rappel ; une place à un tour (cartes) n'est jamais recentrée. Mesuré (CLI) : nomi.gg contre le top 25 %, 2 héros « contested » sur 58 au lieu de 8 ; top 25 % contre tous, 5 sur 116 au lieu de 20. Cache nomi.gg en schéma 2 (un cache en 1 est redemandé à la prochaine tentative du jour). Le vrai fichier avait une tribu sans partie et **sans** champ `avg` : l'import
  l'accepte (test), il refusait le fichier entier.
- Les compositions de Firestone (`CompService`, `TavernAdvisor`, `CompositionRows`, `CompDetail`, `CompTransitions`,
  `MinionLineups`) restent chargées et dans le code ; elles ne sont plus affichées (la ligne « comp ≈ » de l'encart
  des héros est retirée depuis le 2026-10-08, décision d'Ali), mais orientent les aides par le pont ci-dessous. Une erreur d'un fichier de `manual\` est dite une
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

## Mod anti-danse (depuis le 2026-10-10, issue #1 ouverte, rien vu en jeu)

**État : mod écrit, non installé, en attente d'observation** (décision d'Ali : observer d'abord une danse réelle sans
le mod). Journal, conception, écarts, procédure, marche à suivre d'observation (§ 10) et accord de l'auteur du mod de
Nomi (§ 11) : `docs/journal/2026-10-10-anti-danse.md`.

- **Observer** (sans le mod) : section `[Zone]` à ajouter à `%LocalAppData%\Blizzard\Hearthstone\log.config`, jeu fermé
  (`LogLevel=1`, `FilePrinting=True`, `ConsolePrinting=False`, `ScreenPrinting=False`, `Verbose=True` ; HDT la garde, ne la
  remet pas si on la retire). Les journaux tombent dans `E:\JEUX\Hearthstone\Logs\Hearthstone_<lancement>\` (`Power.log`,
  `Power_old.log`, puis `Zone.log`). Ali note l'heure à la seconde, le geste, ce qui bouge, taverne ou combat, et rend le
  dossier de la session (jamais dans le dépôt ni une issue : pseudos des adversaires).
- **Lire** : `python3 tools/nodance-observe.py --at HH:MM:SS [--at …] [--zone Zone.log] Power_old.log Power.log` (lecture
  seule, Python standard ; pour chaque heure : options et réponses dans les ± 5 s, puis D-a, D-b ou « neither D-a nor
  D-b: cause not established » ; le joueur retrouvé partie par partie, aucun nom de joueur écrit). Tests :
  `python3 tools/test_nodance_observe.py` (journaux inventés ; une étape de la CI depuis le 2026-10-10). Les fréquences de la recherche votaient le
  joueur une fois par fichier, qui contient plusieurs parties : à revérifier.

- **Ce que c'est** : un plugin **BepInEx 5** (5.4.23.5, HarmonyX 2.9.0) chargé **dans le client Hearthstone**, qui corrige
  par Harmony la « danse » des sbires de la rangée du joueur en taverne. À l'inverse du plugin HDT, il **modifie le
  client** : l'EULA de Blizzard l'interdit en toutes lettres (« hacks », faits cités au § 8 du journal, sans avis).
  L'installer, le publier (`README.md` et `ROADMAP.md` n'en parlent pas) et fermer l'issue #1 sont des décisions d'Ali.
- **Le mécanisme** (établi en lisant le client, démontré sur un modèle, **aucune danse réelle observée**) : trois
  écrivains de la place d'un sbire dans trois repères (prédiction au lâcher, temps réel carte par carte, liste traitée en
  retard). **Correctif** : un seul écrivain, en fin d'image (`NoDanceDriver.LateUpdate`) : l'ordre affiché tant que l'action
  du joueur n'a pas de réponse (en vol, délai 3 s), sinon l'ordre temps réel du serveur ; une carte déjà retirée par le
  serveur reste derrière sa voisine de gauche ; les places pures rejouées par une liste serveur non confirmée sont
  neutralisées sur la rangée du joueur. Portée : Battlegrounds hors Duos et spectateur, phase de taverne animée, étape temps
  réel `MAIN_ACTION`, pas de combat temps réel ; rien n'est écrit tant qu'une carte est tenue ou qu'un choix est ouvert.
- **Cibles** (`PatchTargets`, la seule liste, lue par le mod au chargement et par le test de signatures) : H1
  `ZoneMgr.Awake` postfix, H2 `GameState.SendOption` prefix, H3 `PowerTask.DoRealTimeTask` postfix, H4
  `ZoneMgr.AddPredictedLocalZoneChange` prefix + postfix, H5 `ZoneMgr.PostProcessServerChangeList` postfix (requises) ; H6
  `ZoneMgr.OnRealTimeZonePosChange` prefix (facultative, réglage `Fixes.SkipPerCardRealTimeWrites`, désactivée). Les membres
  appelés sont dans `ClientMembers.CalledByMod` ; au chargement, une cible requise ou un membre absent : **rien n'est patché**,
  ligne `… disabled (missing: …)`. Une exception dans une partie requise arrête tout le mod pour la session (une ligne),
  H6 s'arrête seule.
- **Écarts avec la recherche** (journal § 3, chacun testé) : ancrage par clé (voisine, après elle) au lieu de « + 0,5 »
  (trois cartes sortantes de suite) ; réconciliation quand une carte entre ou sort de la rangée (H5 marque au **début** de la
  liste ; contre-exemple : deux jetons invoqués devant) ; H5 ne neutralise pas une entité qu'un changement du serveur fait
  changer de zone dans la même liste ; rien n'est écrit pendant un choix ouvert ; une seule DLL.
- **Ce que la lecture du mod de Nomi a changé** (journal § 12, règles réécrites, **aucun code de Nomi dans le dépôt**) :
  **C1**, un défaut réel : quand une prédiction est en attente, le client fusionne la liste serveur et invente, sans tâche,
  un changement qui nomme la zone pour chaque carte de la rangée ; H5 les prenait pour des transferts et ne neutralisait
  plus rien. Désormais seul un changement du serveur (`ZoneChange.GetPowerTask() != null`) fait un transfert, et ceux du
  client sont neutralisés (ligne `… k merged position(s) neutralized`). **C1b** : tant qu'une liste serveur est en cours
  (`ZoneMgr.HasActiveServerChange()`), la rangée est sale à chaque image (`reconcile (server list active)`), et l'écrivain
  signale `position written by a server list after the row was reconciled` (de quoi trancher C1 en jeu). **C2** : une
  carte posée depuis la main est montrée d'emblée à la place serveur prédite (`reconcile (drop): … placed at server slot k
  (drawn slot d)`), pas tant qu'un sbire est une cible magnétique ; la même règle pour un déplacement (C2') reste à trancher
  après observation. **Ne jamais installer ce mod avec celui de Nomi** : mêmes méthodes patchées, sans coordination.

| Projet | Cible | Rôle |
|---|---|---|
| `src/TavernCompass.NoDance.Core` | `netstandard2.0`, dans la solution | `BoardOrder.Target`, `RowReconciler` (sale, vol, délai, rangée qui change, compteurs), `ReplayedPositions`, `PatchTargets`, `ClientMembers`, `ClientSignature` ; sans le jeu, sans BepInEx |
| `tests/TavernCompass.NoDance.Core.Tests` | `net8.0`, dans la solution | `ClientModel` (règles du client, cadencées par images comme le mod, fusion des listes comprise), trois scénarios, recherche exhaustive (1 724 / 2 072 sans le mod, 0 avec, H6 ou non), trois déplacements, gel en vol, ancrage, jetons, prédiction fausse, C1, C1b, C2 ; signatures |
| `mods/TavernCompass.NoDance` | `net48`, **hors de la solution** | le plugin (`com.tavern-compass.nodance`, « Tavern Compass — No Dance », version de `Directory.Build.props`) ; compile les sources du cœur |
| `tools/nodance-deploy.sh` | WSL | installe BepInEx (si absent) et le mod dans le dossier du jeu |

```bash
dotnet build mods/TavernCompass.NoDance -c Release -warnaserror -p:HearthstoneManagedDir=/mnt/e/JEUX/Hearthstone/Hearthstone_Data/Managed/
HEARTHSTONE_MANAGED=/mnt/e/JEUX/Hearthstone/Hearthstone_Data/Managed dotnet test tests/TavernCompass.NoDance.Core.Tests -warnaserror
tools/nodance-deploy.sh --dry-run        # puis sans --dry-run (demande y) ; --uninstall [--purge] pour revenir
```

- **Build** : `HearthstoneManagedDir` n'a **pas de défaut** (erreur qui dit quoi passer) ; `Assembly-CSharp.dll`,
  `UnityEngine.CoreModule.dll` et `UnityEngine.dll` lus là, jamais copiés (`Private=false`) ; `BepInEx.dll` et `0Harmony.dll`
  extraits de l'archive officielle téléchargée dans `lib/bepinex/5.4.23.5/` (ignoré), refusée si son SHA-256 n'est pas celui
  du `.csproj`. La CI ne construit pas le mod (il faut le jeu) ; elle teste le cœur.
- **Signatures** : sans `HEARTHSTONE_MANAGED`, les 4 tests de `GameSignatureTests` sont **sautés avec la raison** ; avec, ils
  lisent les métadonnées du client (rien n'est chargé) : chaque cible, ses paramètres liés par nom, chaque membre listé, un
  témoin inventé rendu manquant, et chaque membre du client que **la DLL construite** référence doit être vérifié au
  chargement. À relancer après chaque mise à jour du jeu et chaque changement du mod.
- **Déploiement** (`tools/nodance-deploy.sh`, cible `/mnt/e/JEUX/Hearthstone`, `--game-dir` ou `HEARTHSTONE_DIR`) : refuse si
  Hearthstone tourne, si la DLL est plus vieille que les sources, ou devant une installation de BepInEx à moitié ; n'écrase
  jamais un BepInEx existant ; signale les autres plugins (celui de Nomi se battrait avec celui-ci) ; demande `y` ; compare
  les SHA-1 ; imprime le retour arrière. `--uninstall` retire la DLL, `--purge` aussi BepInEx (`winhttp.dll`,
  `doorstop_config.ini`, `.doorstop_version`, `changelog.txt` s'ils sont ceux de l'archive, et `BepInEx/`). Exercé sur un faux
  dossier de jeu ; jamais lancé sur le vrai.
- **Journal** (`BepInEx/LogOutput.log`) : `patch … : ok`, `5/5 required patches applied, 0/1 optional (game …)`, `attached to
  ZoneMgr (game n)`, `option sent: entity=… position=… (in flight)`, `option answered after … ms` / `rejected` / `flight
  timeout`, `prediction: entity=… slot=… predicted=… list=… (renumbered k)`, `server list … (PLAY): k replayed position(s)
  neutralized, m merged position(s) neutralized`, `reconcile (raisons): shown [ids] -> [ids], k position(s) changed[, kept
  visual order (in flight)][, placed at server slot k (drawn slot d)]`, `position written by a server list after the row
  was reconciled: [id: a->b, …]`, `game
  summary: flights=… reconciles=… order changes=… kept=… renumbered=… neutralized=… (merged …) drops=… late writes=… (game n)`. `order changes` et
  `neutralized` à zéro sur plusieurs parties : le mod n'a rien fait.
- **Pas vu en jeu** : tout, à commencer par BepInEx 5.4.23.5 et HarmonyX sur ce client Unity 6 Mono. Supposé : que les
  danses vues par Ali soient celles du modèle (journal § 4, et le scénario d'Ali § 7).

## Docs

| Dossier | Contenu |
|---|---|
| `README.md`, `ROADMAP.md` | la vitrine publique (anglais) : ce que fait le plugin, comment l'installer, où en est le projet |
| `docs/plans/` | 2026-09-26 : étude de stack, spec et plan du plugin HDT |
| `docs/plans/2026-10-04-panneau-unique-ergonomie.html` | note de conception HTML pour Ali (images dans `img/2026-10-04-panneau-unique/`, `img/2026-10-06-tribus-cases-puissance/` et `img/2026-10-06-encart-puissance/`) : les 7 demandes du panneau unique → décisions, avant / après, flux des cibles, ce qui n'a pas été vu, ce qui reste à décider ; § 11 : tribus du lobby, cases à cocher, puissance du board (2026-10-06) ; § 12 : encart de puissance, + / − qui redimensionnent, jauge de l'adversaire (2026-10-06, soir) |
| `docs/plans/2026-10-08-stats-multi-sources.html` | note de conception HTML pour Ali (2026-10-08), sans implémentation : intégrer plusieurs sources de stats (Firestone dont card-stats, nomi.gg, HDT, HSReplay) — droits de chaque source, format commun qui garde la provenance (source, patch, tranche, fenêtre, effectif), consolidation qui dit « contesté » au lieu de choisir, mise à jour par source, nouveaux composants d'interface, ce que fait Nomi's Kitchen ; architecture (une classe par source, une fonction pure de consolidation) et séquence type ; décisions d'Ali rendues le 2026-10-08 (l'anti-danse du board, étudiée : un correctif Harmony dans le client, obfusqué) et ce qui n'est pas établi |
| `docs/reference/` | recherche HDT / Tier7 |
| `docs/journal/` | ce qui s'est décidé, séance par séance |
