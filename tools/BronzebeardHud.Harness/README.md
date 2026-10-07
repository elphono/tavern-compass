# Simulation du plugin (hôte Windows)

Les panneaux du plugin dans une fenêtre Windows ordinaire, avec des données synthétiques, pour les déplacer, les
redimensionner et les regarder **sans lancer une partie** ni HDT.

```bash
./tools/BronzebeardHud.Harness/launch.sh                 # la fenêtre, pour déboguer à la main
./tools/BronzebeardHud.Harness/launch.sh --selftest      # vérifie la scène sans personne au clavier (code de sortie 0 = tout passe)
./tools/BronzebeardHud.Harness/launch.sh --screenshot    # écrit C:\temp\BronzebeardHarness-ci\out\shot.png (le canvas à sa taille réelle)
./tools/BronzebeardHud.Harness/launch.sh --screenshot --detail 2   # la même, détail de la 2e cible ouvert (rang, ou nom d'un guide)
./tools/BronzebeardHud.Harness/launch.sh --screenshot --tick 1   # la même, la compo n° 1 cochée : elle passe en tête, les compos en cours restent
./tools/BronzebeardHud.Harness/launch.sh --screenshot --scenario 3   # lobby sans morts-vivants ni dragons : leurs guides ni listés ni ciblés
./tools/BronzebeardHud.Harness/launch.sh --screenshot --scenario 5 --tick 3   # cases : la compo pariée cochée, la compo en cours gardée (« in progress »)
./tools/BronzebeardHud.Harness/launch.sh --screenshot --power shiny   # la rangée du joueur dans l'encart : behind, even, ahead, shiny, none, early
./tools/BronzebeardHud.Harness/launch.sh --screenshot --opp-power next   # la rangée de l'adversaire : behind, even, ahead, shiny, none, next, unseen
./tools/BronzebeardHud.Harness/launch.sh --screenshot --count 4   # − / + cliqués jusqu'à 4 compos, mode déplacement off : le panneau dimensionné sur elles
./tools/BronzebeardHud.Harness/launch.sh --screenshot --play     # mode déplacement off, comme en partie (le panneau sur N lignes)
./tools/BronzebeardHud.Harness/launch.sh --screenshot --choice discover   # la même, un choix ouvert : discover, dark-gift ou trinket
./tools/BronzebeardHud.Harness/launch.sh --screenshot --choice discover --close-choice   # le choix ouvert puis refermé : la scène rétablie
./tools/BronzebeardHud.Harness/launch.sh --screenshot --hover 1 --no-skip   # la ligne de la 1re cible survolée : son popup, comme en taverne
./tools/BronzebeardHud.Harness/launch.sh --screenshot --hover 1 --hover-card 2   # la même, et le 2e ovale de la ligne survolé : l'aperçu de sa carte
```

`--hover <rang ou nom>` passe le mode déplacement à off (pas de popup en mode déplacement), lève le `MouseEnter` de la
ligne comme la sonde de HDT, et laisse passer le délai ; la capture échoue (`error.txt`) si le popup ne s'est pas montré.
`--hover-card k` survole aussi le k-ième ovale de la même ligne. `--no-skip` cache le bouton Skip combat (il ne se montre
qu'en combat dans le plugin) : le popup a alors toute la colonne au-dessus du panneau.

`--choice discover|dark-gift|trinket` (ou la liste « No choice / Discover / Dark Gift / Trinket » de la barre) ouvre un choix au-dessus de la scène : 3 options (découverte, Dark Gift) ou 4 trinkets, comme dans les parties d'Ali, en rectangles gris
nommés aux places de `ChoiceLayout.Cards`, avec les étiquettes du vrai `ChoiceAdvicePanel` au-dessus. Options et stats de trinkets synthétiques (`HarnessData.Choice`) : en découverte, les trois étiquettes du pont
(« + Pirate Discover · 3/5 boards », « + Mech Magnet 3/5 boards », « pivot → Beast Pack (A) ») ; en Dark Gift, enabler d'une cible non pontée, carte clé
d'un guide S non ciblé, carte sans rapport ; trinkets nommant la tribu d'une cible ; elles suivent `--scenario` et `--tick`. `--close-choice` referme le
choix avant la capture : la scène telle que le plugin la rétablit.

Le pont (`GuideBridge`) relie les guides à deux compos Firestone **synthétiques** (`HarnessData.FirestoneComps`, cinq plateaux finaux
chacune, sur les ids de `Pool`, un héros inventé) : `pirate_fs` → Pirate Discover, `mech_fs` → Mech Magnet ; Mech Divine Shield partage
trois cartes avec `mech_fs` mais seulement 3 de ses 7 cartes clés : « no match », le contre-exemple. La ligne `bridge:` est au journal.

Arguments en plus : `--size 1600x900`, `--layout <fichier>`, `--wait <ms>` (attente des noms et images avant la capture),
`--scenario 0…5` (plateau tenu et tribus du lobby, `HarnessData.Scenarios` ; 2 par défaut), `--power <scène>` (la rangée
du joueur dans l'encart de puissance, `HarnessData.Power` ; even par défaut), `--opp-power <scène>` (la rangée de
l'adversaire, `HarnessData.OpponentFacts` ; even par défaut), `--count <1…4>` (− et + cliqués comme la souris jusqu'à ce
nombre, mode déplacement off ; une case cochée les grise et fait échouer la capture), `--play` (mode déplacement off),
`--detail <rang ou nom>`
(ouvre le détail avant la capture ; un rang qui n'existe pas fait échouer la capture plutôt que de capturer la liste).
Un panneau en bas de l'écran : `--layout 'C:\temp\BronzebeardHarness-ci\layout-bottom.json'` avec
`{"schema": 1, "panels": {"target-compositions": {"left": 0.6151, "top": 0.9}}}` (ramené dans l'écran : il touche le bas).

| `--opp-power` | Ce que lit la rangée de l'adversaire (héros adverse inventé : 143 au tour 8, 50 au tour 5 ; le joueur : 120 au tour 8) |
|---|---|
| behind, even, ahead, shiny | en combat, tour 8 : plateaux de 90, 160, 210, 300 contre 143 |
| none | en combat, un héros sans courbe : « no curve for … » |
| next | en taverne, tour 9 : le dernier plateau vu du prochain adversaire, 70 au tour 5, contre 50 (au tour 9, 250 : il lirait « behind ») |
| unseen | en taverne : un prochain adversaire jamais affronté, « not fought yet » |
Un nom de guide avec des espaces ne passe pas par `launch.sh` (PowerShell le coupe) : donner le rang de la cible.

| Scénario | Plateau, main | Lobby | Cibles (3 voulues) |
|---|---|---|---|
| 0 Nothing yet | rien | 5 tribus (bêtes, élémentaires, mécas, pirates, morts-vivants) | 0 |
| 1 Two cards of one composition | 2 cartes clés d'Elemental Cycle | idem | 1 |
| 2 A strong board, one in hand | Pirate Discover, Mech Magnet, Mech Divine Shield | idem | 3 |
| 3 A neutral key card of absent tribes | la carte neutre `Pool[0]`, carte clé de 3 guides morts-vivants et dragons, + 2 mécas | sans morts-vivants ni dragons | 2 (les mécas) |
| 4 The lobby not known yet | la carte neutre et une carte clé méca | inconnu | 3 (rien n'est écarté ; la ligne « Lobby tribes unknown » cède sa place à une cible à la taille par défaut, se montre avec une seule cible) |
| 5 Ticks: in progress and guesses | Elemental Cycle ★2/4, une carte clé de quatre autres guides | 5 tribus | 3 ; cocher Pirate Discover garde Elemental Cycle (« in progress ») et fait taire les paris ; seul Pirate Discover encadre alors les cartes de Bob |

Les tribus des cartes de la simulation sont synthétiques (`HarnessData.CardTribes` : celle du premier guide qui la liste,
`Pool[0]` neutre) ; la courbe de puissance est inventée (×1,8 par tour, 120 au tour 8).
Le script compile sous WSL, copie dans `C:\temp\BronzebeardHarness` et lance l'exécutable côté Windows (par `Start-Process` : lancée par `cmd.exe /c start`, la fenêtre garde la console de WSL attachée et le script ne rend jamais la main). **Fermer la fenêtre avant de la relancer** : un exécutable en cours ne se remplace pas, et le script le dit.
`--selftest` et `--screenshot` tournent depuis leur propre copie, `C:\temp\BronzebeardHarness-ci` (sortie dans son `out\`) : ils passent même fenêtre ouverte, sans toucher à son dossier.
Ils lisent aussi leur propre disposition, `C:\temp\BronzebeardHarness-ci\layout.json` (jamais écrite : la disposition par défaut), sauf `--layout` explicite : la capture ne dépend pas de la fenêtre. Le journal et le cache d'images restent partagés.

## Ce qui est réel, ce qui est simulé

| Réel : les sources du plugin, compilées telles quelles | Simulé : `HdtShim.cs` |
|---|---|
| `PanelMover` (déplacer, poignée, cadre), `CompsPanel` (le panneau « Compositions »), `GuideView` et `GuidePopup` (le guide en popup au survol d'une ligne), `TavernMarkers` (cadres et étiquettes sur les cartes de Bob, boutons ◇), `SkipCombatPanel`, `ChoiceAdvicePanel` (étiquettes des choix ; son cache de stats de trinkets n'est jamais interrogé ici, aucune requête), `CardImages`, `PreviewPlacer`, `OverlayLayer` | `OverlayExtensions` (les deux premières sans effet : la fenêtre normale reçoit la souris ; l'infobulle est dessinée comme HDT la dessine, `HdtTooltip` : **un seul** emplacement sur le canvas, au-dessus des éléments du plugin, placé d'après la taille mesurée de l'infobulle, son placement et ses décalages, puis gardé dans la fenêtre — d'après `OverlayWindow.SetTooltip` de HDT 1.58.6 décompilé), `Log` (le volet de droite), `Database` (noms et paliers de HearthstoneJSON), les téléchargeurs d'images (art.hearthstonejson.com, cache dans `%TEMP%\BronzebeardHarness`) |

La logique de `Plugin.cs` (lecture d'HDT, `CompTargetTracker`, `TavernHighlights`) est rejouée par `HarnessWindow` avec les
mêmes appels de `BronzebeardHud.Stats` : cibles et couleurs sont celles que le plugin calculerait sur ces cartes.

**Pas simulé, et un défaut qui y vivrait ne se reproduit pas ici :** la couche d'HDT autour des panneaux, c'est-à-dire
la fenêtre d'overlay transparente aux clics au-dessus du jeu et le survol sondé à 60 Hz (le `--selftest` lève lui-même
`MouseEnter` et `MouseLeave` ; le double `MouseEnter` de la sonde et de WPF, et le `MouseLeave` que WPF lève quand la
fenêtre repasse en clic-transparent, n'y sont qu'imités) ; la vraie rangée de Bob (ses sept cartes sont des boîtes grises
placées où `TavernLayout.CardSlots` met les cartes du jeu) ; la lecture du plateau adverse par
`HdtEntityAdapter.OpponentFacts` (héros contrôlé par `game.Opponent`, `NEXT_OPPONENT_PLAYER_ID`, plateau figé par HDT au
début du combat) : la simulation lui donne des faits inventés (`HarnessData.OpponentFacts`), seul le calcul et le rendu qui
suivent sont réels. L'infobulle unique de HDT n'est vue qu'à travers sa copie,
`HdtTooltip`, écrite d'après le code décompilé. Ce que l'hôte reproduit : la logique des panneaux, leurs événements WPF,
leur mise en page et leur remplissage.

## Ce qui est à l'écran

- Le canvas à la taille d'une fenêtre Hearthstone (liste déroulante : 1920 × 1080, 1600 × 900, 4:3, 21:9, 2291 × 1360).
- Les deux panneaux déplaçables (Compositions, Skip combat), mode déplacement activé au départ, et les zones à ne pas
  masquer en rouge dessous (définies par `NoGoZones` des tests : une seule définition).
- La rangée de Bob : deux cartes clés d'une cible (cadre plein), un add-on et un enabler (pointillés), une carte épinglée
  (◆, blanc), une qui ne sert à rien, et une qu'aucun guide de cible ne nomme mais qui est sur 3 des 5 plateaux de la compo
  Firestone de Mech Magnet (pointillés « + M. Magnet 3/5 », par le pont).
- Six scénarios (liste déroulante, tableau ci-dessus : chacun est une partie, cases et couleurs oubliées en changeant) ;
  le bouton « Detail of target 1 / list » ouvre et ferme le détail.
- L'encart de puissance sous le cadre, entre − et + (listes déroulantes « power … » et « opp … ») : les quatre paliers et
  les cas sans donnée, pour le joueur et pour l'adversaire.
- Un choix (liste déroulante) : ses options au-dessus de la rangée de Bob, comme dans le jeu, sous les étiquettes du
  plugin. Tant qu'il est ouvert, les cadres, étiquettes et ◇ des cartes de Bob et le panneau « Compositions » (et son popup)
  sont retirés de l'écran, puis remis tels quels à sa fermeture (`ChoiceCover`, comme le plugin).
- Un volet de journal : les lignes du plugin (`resize start`, `resize end`, `panel moved`, `ticked guides`…).
- La disposition est enregistrée dans `%TEMP%\BronzebeardHarness\layout.json`, **jamais** dans le `layout.json` du plugin.
- Les données sont inventées (quinze guides, leurs textes et leurs listes de cartes) sur de vrais identifiants de cartes ;
  rien ne vient de Firestone ni de HSReplay. Les noms et images viennent de HearthstoneJSON, téléchargés dans le cache temporaire.

## Ce que vérifie `--selftest`

Deux panneaux visibles et dans l'overlay ; une poignée et un cadre (seul le panneau des compositions se redimensionne) ;
le mode déplacement ; 0, 1, 3, 2, 3 et 3 cibles selon le scénario, de couleurs distinctes ; **les cases** (scénario 5,
cases cliquées dans la ligne du guide nommé) : la cochée en tête, la compo en cours gardée dans sa couleur avec « in
progress », le pari parti, les cadres de Bob qui suivent, « 1 chosen » puis « 2 chosen », − et + grisés sans effet, tout
décocher rend les mêmes cibles dans les mêmes couleurs ; **les tribus du lobby** (scénarios 3 et 4) : aucune cible, ligne
ni cadre d'un guide d'une tribu absente (calculé sur les données de la simulation, pas pris à `LobbyGuides`), la ligne de
journal qui les nomme ; lobby inconnu : rien d'écarté et la ligne « Lobby tribes unknown » ; une case cochée lobby
inconnu puis écartée une fois le lobby connu : décochée, une ligne `unticked`, plus cible ; **l'encart de puissance** :
hors du cadre, dessous, aussi large que le panneau, − à gauche et + à droite centrés, la rangée du joueur au-dessus de celle
de l'adversaire, aucun texte sous 12 px ni coupé ; **chaque rangée**, pour chaque scène : badge, signe et pourcentage,
couleur, le seul feu allumé à sa place et le seul halo, de la couleur du palier (or pour shiny), les autres éteints,
chiffres écrits ; sans donnée, tout gris, aucun halo, « – » ; pour l'adversaire, une ligne `opponent power` par
changement, qui nomme son héros et le vôtre, le tour vu, le plateau et la moyenne ; **+ / −** (mode déplacement off,
cliqués comme la souris) : N de 1 à 4 et retour, trois fois, N lignes dont les meilleures cibles par rang (ou, faute de
place, le bas sur l'or), aucune zone du jeu couverte, le même rectangle pour le même N, une ligne `targets n=` par appui
dont les chiffres sont ceux dessinés (+ à 4 compris) ; contre le bas de l'écran le bas gardé, grandissant vers le haut ;
une boîte donnée par la poignée gardée jusqu'à un appui, retrouvée à la partie suivante, « Reset » qui rend la place par
défaut ; **une rangée en panne** (dessin qui lève) retirée seule, la rangée du joueur, − et + restant ; les cadres sur les cartes de Bob, pleins
et pointillés, tels que `TavernHighlights` les demande ; un clic sur le nom d'une cible ouvre son détail, « ← All comp
guides » rend la liste ; les sections du détail dans l'ordre de HDT et « k of n sections » quand il en manque ; **aucun
texte du panneau sous 12 px × échelle, aucun texte coupé** (liste et détail : l'encre de chaque texte, pas sa boîte, doit
tenir dans chaque découpe de ses parents) ; pour chaque choix (découverte, Dark Gift, trinket), une étiquette par option à
sa place, mêmes contrôles de texte, « ★ core T » ou « + T » dans la couleur de T (lue sur le texte et sur le fond dessiné),
chaque trinket avec sa place moyenne, une option sans rapport qui dit « — » ; le **survol d'une ligne** (mode déplacement
off, `MouseEnter` et `MouseLeave` levés sur la ligne, le vrai délai de 250 ms laissé passer) : quittée avant 250 ms, pas
de popup ; deux `MouseEnter`, un seul affichage ; le popup dans l'overlay, hors des `NoGoZones`, du panneau et de Skip
combat ; ses textes (mêmes contrôles) ; ses sections dans l'ordre de HDT, les six d'un guide qui les a ; une ligne de
journal par affichage ; l'aperçu de carte d'un ovale de la ligne en même temps, sans recouvrir ni le popup ni le panneau ;
un `MouseLeave` alors que le curseur est encore dans la ligne ignoré, un vrai le cache ; le clic sur le nom (détail) et
le mode déplacement le cachent ; **le pont** : une ligne `bridge:` qui nomme chaque guide, les deux pontés et le contre-exemple
« no match » (mesuré sur les données : ≥ 2 cartes communes mais moins de la moitié des cartes clés) ; en découverte, « + T k/n boards »
dans la couleur de T, un rôle suivi de « · k/n boards », « pivot → G (X) » sur fond neutre, lus sur ce qui est dessiné ; sur une carte
de Bob, un cadre pointillé « + T 3/5 » dans la couleur de T et la ligne `tavern highlights=[…:boards 3/5:…]` ; la ligne de contexte
sous l'en-tête du détail et du popup d'un guide ponté (texte attendu calculé à la main, 12 px, gris), absente pour le non ponté ;
**un choix ouvert** (Dark Gift puis découverte) retire cadres, étiquettes, ◇, panneau, encart et popup, aucun popup au survol pendant ce
temps, une ligne `choice open` et une seule ; refermé, les mêmes cibles, les mêmes marqueurs (places, couleurs, textes), le panneau
à sa place avec **les mêmes éléments** (pas reconstruit), une ligne `choice closed`, et le survol remarche ; aucune ligne Warning/Error, **lue après que le dispatcher a livré les lignes** (avant le
2026-10-04 elle lisait 0 ligne : elles arrivent par `Dispatcher.BeginInvoke`) ; le fichier de disposition est celui de la
simulation.
Le contrôle des textes a été éprouvé par mutation le 2026-10-04 : un nom sans retour à la ligne et un texte à 11 px le font
échouer ; celui des étiquettes de choix aussi : une couleur forcée (magenta partout) et un padding × 20 le font échouer.
Ceux du survol aussi (2026-10-04) : popup posé sur les plateaux, délai supprimé, sortie « curseur encore dans la ligne »
non reconnue, aperçu de carte sans sa boîte de taille fixe, mode déplacement qui ne cache plus ; et un avertissement
journalisé juste avant le contrôle du journal le fait échouer (sans la vidange du dispatcher, il passait inaperçu).
Ceux du pont et du masquage aussi (2026-10-04) : marqueurs dessinés malgré le choix, panneau laissé visible, panneau
reconstruit au retour, journal à chaque mise à jour, pont non passé aux aides ou aux cadres, couleur de « + T k/n boards »
neutre, ligne de contexte absente, à 13 px ou absente du seul popup, seuil du pont relâché (le contre-exemple ponte). Une
mutation survit, et c'est voulu : retirer « suspendu » de ce qui bloque le popup ne le fait pas revenir pendant un choix,
`PopupContent` refuse aussi un panneau suspendu, et un panneau replié a une largeur nulle (trois gardes, la propriété tient).
Ceux du 2026-10-06 aussi : simulation aveugle au lobby (4 contrôles tombent), filtre des tribus retiré (5), cochées seules
(3), paris non tus (3), jauge toujours au 2ᵉ segment (5), « in progress » jamais écrit (1), ligne « lobby inconnu » jamais
cédée (1). C'est en les lançant qu'on a vu trois de ces mutations faire planter la simulation sans écrire de rapport : chaque
groupe de contrôles est depuis gardé, une exception y devient un échec nommé.
Ceux de l'encart et de + / − (2026-10-06, soir), avec la suite xUnit, sur une copie : 25 mutations, 25 détectées. Celles que
seule la simulation voit : tout feu allumé en jaune (7 contrôles), − jamais grisé (3), un appui qui ne dimensionne pas une
boîte de la poignée, la partie suivante qui garde la taille de + / −, une boîte périmée prise au dernier redessin, les deux
rangées sous un seul garde-fou (1 chacune). Le contrôle « panneau remis tel quel après un choix » lit désormais le contenu du
cadre (`CompsPanel.Content`) : l'enfant du panneau (cadre + encart) ne change jamais, et le comparer ne prouvait plus rien.
