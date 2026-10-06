<!-- visuel : dérogation — journal de séance, au format Markdown des autres journaux du dépôt ; la note illustrée est docs/plans/2026-10-04-panneau-unique-ergonomie.html § 11 -->
# Journal — 2026-10-06 : tribus du lobby, cases à cocher, puissance du board

Demandes d'Ali, mot pour mot : « il faut corriger l'algo qui choisit quelles compos à conseiller. il ne faut pas conseiller
les compos qui contiennent des tribus non présentes dans la partie. bug: quand je click sur une checkbox ça enlève
d'autres compos (je crois celles que potentiellement j'étais en train de jouer déjà). […] Pense ergonomie en premier
lieu. » Puis : « Pour les compos, on peut créer un composant visuel pour illustrer la puissance de mon board par
rapport à l'average. un indicateur rouge/jaune/vert/shiny ? »

Fait sous WSL (tests, simulation, build du plugin) ; **rien n'est encore vu en jeu**. La note illustrée pour Ali est le
§ 11 de `docs/plans/2026-10-04-panneau-unique-ergonomie.html`.

## Données de la séance (lecture seule, rien n'entre dans le dépôt)

- Journaux d'HDT d'Ali (copiés hors du dépôt) : 69 lignes `comps round=` du 2026-10-06 (13 h 44 – 18 h 16), 464 lignes
  `warband round=` sur tous les journaux conservés, 19 clics de case (du 4 au 6 octobre).
- `Power.log` du jeu des sessions du jour : les tribus du lobby n'y sont écrites nulle part ; elles se **déduisent** des
  sbires que Bob propose (contrôleur = le joueur factice `BACON_DUMMY_PLAYER`, balise `CARDRACE`). Six parties :

| Partie (CREATE_GAME) | Tribus déduites (5) |
|---|---|
| 13:48:10 | Aberration, Bête, Élémentaire, Méca, Murloc |
| 14:57:04 | Bête, Dragon, Murloc, Huran, Mort-vivant |
| 15:32:27 | Élémentaire, Méca, Murloc, Huran, Mort-vivant |
| 17:04:29 | Démon, Méca, Pirate, Huran, Mort-vivant |
| 17:47:18 | Bête, Dragon, Méca, Murloc, Pirate |
| 18:03:32 | Aberration, Élémentaire, Méca, Murloc, Huran |

Chez Bob, une ou deux cartes d'autres tribus apparaissent par partie (une seule carte par tribu), contre 4 à 19 cartes
distinctes pour chaque tribu du lobby (la dernière partie n'est que partiellement journalisée).

## 1. Tribus absentes : la cause, mesurée

| Mesure | Valeur |
|---|---|
| rondes avec au moins une cible d'une tribu absente du lobby | **15 sur 69** (5 parties sur 6) |
| lignes `tavern highlights` avec un cadre pour une tribu absente | 7 |
| exemple | 17:18:28.80 (Power.log) Ali achète **Titus Rivendare**, sans tribu ; 17:18:29 (journal d'HDT) les cibles deviennent un guide Mort-vivant, un Démon, **un Bêtes et un Aberrations**, deux tribus absentes ; cadres « + … » sur des cartes de Bob pour le guide Bêtes à 17:20:46 et 17:21:02 |

**Cause** : le chemin des cibles (`Plugin.UpdateComps` → `CompGuideMatch.Rank` → `CompTargets.Choose`) classait la liste
entière de HDT et ne lisait jamais les tribus du lobby ; seul le dernier repli des aides de choix les lisait
(`GuideTribes.InLobby`). Et la liste gratuite de HDT n'est pas filtrée (sa liste Tier 7 l'est : journal du 2026-10-04).
Une carte neutre est carte clé de plusieurs guides : en la tenant, des guides de tribus absentes prennent ★1 et entrent
dans les cibles. Départagé d'une autre cause possible (des tribus du lobby mal lues, « PET » pour BEAST) : ce chemin ne
les lit pas du tout ; le test `CompRoundTests` reproduit les cibles fautives quand le lobby est inconnu, et les écarte
quand il est connu.

**Ce que le filtre écarte** (`LobbyGuides`, table de cas, chaque ligne testée dans `LobbyGuidesTests`) :

| Cas | Décision |
|---|---|
| tribu principale du guide absente | écarté, **même si ses cartes clés sont neutres** : le guide est écrit pour cette tribu, et les cartes neutres sont précisément celles qui le rendaient « probable » partout |
| guide sans tribu (« Menagerie ») | gardé, sauf la règle des cartes clés ci-dessous |
| au moins la moitié des cartes clés introuvables (toutes leurs tribus absentes) | écarté : « key cards X, Y: no QUILBOAR, DRAGON » |
| une carte clé introuvable sur quatre | gardé |
| carte à deux tribus | trouvable si l'une est là |
| amalgame (« ALL »), carte sans tribu | toujours trouvable |
| carte inconnue de HearthDb, valeur de tribu inconnue | jamais retenue contre un guide |
| carte dorée | cherchée comme sa carte de base |

Les tribus d'une carte viennent de HearthDb (`Card.Race`, `Card.SecondaryRace`, par valeur : 20 est BEAST, jamais PET).
Les listes de cartes clés des vrais guides ne sont pas sur le disque (HDT les tient en mémoire) : la règle « moitié des
cartes clés » n'a **pas** été éprouvée sur les vrais guides. La ligne de journal la rendra mesurable (ci-dessous).

**La liste du panneau** : les guides écartés **ne sont plus listés**. Arbitré pour le temps d'Ali en partie : la liste de
27 guides ne tenait pas à la place par défaut (« 3 of 27 shown ») ; les six lobbies du jour en gardent 12 à 16 (compté
sur la seule tribu principale, avant la règle des cartes clés) ; une ligne grisée se lit
quand même, une ligne absente non. C'est aussi ce que fait la liste Tier 7 de HDT. Rien n'est caché sans trace : une
ligne de journal par lobby nomme chaque guide écarté et pourquoi. Écarté : griser en place (le bruit reste), reléguer en
bas (la place manque déjà, ces lignes seraient omises de toute façon).

**Lobby pas encore connu.** HDT lit les tribus dans la mémoire du jeu (`BattlegroundsUtils.GetAvailableRaces`, décompilé :
cache par partie, HDT lui-même attend jusqu'à 15 s). Mesure indirecte dans le journal du jour : à chacune des 6 parties,
les 24 compos nommées par l'encart des héros (`hero comps`, qui filtre sur ces tribus) sont du lobby ou sans tribu, dès
la première ligne, moins de 2 s après `CREATE_GAME` (le journal d'HDT est à la seconde). Décision : tant qu'elles sont inconnues, **rien n'est écarté**, le
panneau le dit (« Lobby tribes unknown: every guide listed ») et le journal aussi ; les tribus sont redemandées une fois
par seconde au plus, puis gardées pour la partie. Une case cochée sur un guide que le lobby, une fois connu, ne joue pas
est décochée, avec une ligne `unticked … : no BEAST`.

## 2. Cases à cocher : ce que montrent les clics d'Ali

| Clic | Cibles juste avant | Ce que la case a fait (règle du 2026-10-04) | Décoché |
|---|---|---|---|
| 2026-10-06 17:14:43 | 1. la compo cochée ★3/5 ; 2. une autre ★1/5 | la 2ᵉ disparaît | 2 s après |
| 2026-10-05 22:00:11 | 1. la compo cochée ★2/5 ; trois autres à ★1 | les trois disparaissent | — |
| 2026-10-05 13:02:02 | trois compos à ★2, dont la cochée | les deux autres disparaissent | — |
| 2026-10-05 10:15:25 | trois compos | deux disparaissent | 6 s après |
| 2026-10-04 20:12:41 → 48 | une compo ★2/4 | cocher / décocher quatre fois en 7 s | — |

Les deux demandes ne s'opposent pas : cocher dit « je vais là », et Ali ne veut pas perdre de vue ce qu'il construit.

**Comportement retenu** (`CompTargets.Choose`) :

| Situation | Cibles |
|---|---|
| rien de coché | les plus probables (plateau + main), jusqu'à n (− n +) |
| au moins une case | les cochées (≤ 4, ordre de coche), **puis les guides en cours** — deux cartes clés tenues (ou toutes, pour un guide qui en a moins) —, dans la limite de 4 ; les paris (une carte clé) se taisent |
| à score égal (sans case) | une cible du tour d'avant garde sa place : il faut un score plus haut pour la remplacer |

- « in progress » s'écrit sous le nom d'une compo gardée ainsi : une ligne colorée que personne n'a cochée ne se lit pas
  comme une case qui n'a pas pris.
- − n + reste grisé quand une case est cochée : n compte les paris, que la case fait taire ; une compo en cours n'est
  pas un pari. Le titre dit toujours « k chosen ».
- Seuil de deux cartes clés : une seule ne prouve rien (une carte neutre est carte clé de plusieurs guides, cf. § 1) ;
  deux, ce sont les deux anneaux verts qu'Ali voit sur la ligne.
- Sur les clics ci-dessus : le 13:02 aurait gardé les deux autres compos (★2 chacune) ; le 22:00 et le 17:14 (★1)
  les auraient laissées partir, et c'est voulu — à vérifier avec Ali en partie.

Écarté : revenir à « cochées puis probables jusqu'à n » (les paris reviennent, la case ne restreint plus rien, ce qu'Ali
avait demandé le 2026-10-04) ; tout garder au moment du clic (rien ne change à l'écran : la case paraîtrait sans effet) ;
seuil sur le score (enablers et add-ons sont souvent communs à plusieurs guides).

## 3. Indicateur de puissance du board

Global seulement : le board entier contre la moyenne du héros au même tour (Firestone `warbandStats`). **Par compo : non
fait** — Firestone ne publie pour une compo que sa place, ses plateaux finaux et leur tour, aucune courbe de stats par
tour : il faudrait l'inventer.

| Palier | Seuil (board ÷ moyenne) | Rendu |
|---|---|---|
| rouge | < 1/√1,8 (−25 %) : plus d'un demi-tour de retard | ▼, 1ᵉʳ segment |
| jaune | jusqu'à √1,8 (+34 %) | ≈, 2ᵉ segment |
| vert | jusqu'à 1,8 (+80 %) | ▲, 3ᵉ segment |
| shiny | ≥ 1,8 : un tour d'avance, le board moyen du tour suivant | ★, 4ᵉ segment, badge doré lumineux |
| aucun | moyenne < 12 (tours 1–2), courbe qui retombe, héros sous 100 parties, pas de courbe | segments gris, badge « – », raison écrite |

**Origine** : 1,8 est la croissance médiane, d'un tour au suivant, des moyennes de Firestone (tours 4 à 11, 116 héros) :
1,79 (mmr-100), 1,82 (mmr-50), 1,84 (mmr-25), quartiles 1,68 à 2,06, mesurée sur le cache d'Ali. Les courbes retombent
d'abord aux tours 15 à 18 pour la plupart des héros (jusqu'au tour 5 dans une tranche maigre) : les lignes d'Ali y
lisaient +248 % à +4885 %. 100 parties : un jugement, pas une mesure (Firestone ne donne pas d'effectif par tour ; le
plus petit mesuré est 78, en mmr-25). Sur les 464 rondes journalisées d'Ali : rouge 128, jaune 166, vert 34, shiny 49,
sans couleur 87 (81 trop tôt, 6 courbe retombée ; courbe de mmr-100, approximation).

Rendu (`BoardPowerView`) : jauge de quatre segments (le palier allumé, les autres atténués), badge coloré avec signe et
pourcentage (« ▲ +58% »), puis « Board 190 · hero avg 120 at turn 8 » ; 12 px, le texte passe à la ligne plutôt que d'être
coupé ; à la largeur par défaut du panneau, qui est aussi sa largeur minimale. Journal : `warband round=… · +58% power=ahead`.

## 4. Défauts voisins

| Défaut | Suite |
|---|---|
| repli des aides de choix filtré sur la seule tribu principale | corrigé : elles reçoivent la liste du lobby |
| pivots (détail, popup, choix) vers un guide de tribu absente | corrigé : même liste |
| la simulation ne connaissait aucun lobby | corrigé : chaque scénario a ses tribus, trois scènes nouvelles |
| encart des héros : tribus redemandées à chaque mise à jour tant qu'inconnues | borné à une fois par seconde |
| lobby inconnu : la ligne « Lobby tribes unknown » poussait une cible hors de la liste à la taille par défaut (vu dans la simulation) | corrigé : elle cède sa place à une cible (`CompGuideLayout.KeepsOptionalLine`), se montre quand elle ne coûte rien |
| trois cibles dans trois tiers ne tiennent pas à la taille par défaut (trois barres de tier), la 3ᵉ reste hors de vue | laissé, comportement d'avant ; agrandir le panneau |
| le contrôle de la simulation s'arrêtait sur une exception (une ligne cherchée non dessinée) sans écrire de rapport | corrigé : chaque groupe de contrôles est gardé, une exception devient un échec nommé |

## 5. Pas vu

- Rien sous HDT : le rendu réel, la lecture des tribus, le moment où HDT les a (la ligne `lobby tribes=[…] read at …` le
  dira à la première partie).
- La règle « moitié des cartes clés » sur les vrais guides de HSReplay (listes non disponibles hors de HDT).
- L'effet réel du seuil « deux cartes clés » sur le jeu d'Ali.

## 6. À vérifier en partie (Ali)

1. La ligne `lobby tribes=[…] read at hero selection` (ou `shop turn 1`) et la ligne `… guides playable; left out: …`.
2. Plus aucune compo d'une tribu absente dans la liste, les cadres de Bob et les étiquettes de choix.
3. Cocher la compo qu'on vise : celles qu'on construit (deux cartes clés) restent, « in progress » sous leur nom.
4. La jauge sous la liste : couleur, signe, pourcentage, et le gris des tours 1–2.

## Preuves

Commandes et sorties dans le rapport de la séance ; résumé : `dotnet test` 640 tests verts (610 avant : Stats 558, LogParser
23, GameState 52, App 7) ; simulation `--selftest` 61 contrôles « ALL PASSED » (49 avant ; nouveaux : scénarios 3, 4 et 5,
les six scènes de puissance) ; chaque commit exporté seul passe les tests de Stats et compile le plugin et la simulation.

Mutations, sur une copie du dépôt (jamais l'arbre commité), `bin/obj` purgés entre chaque essai, chaque commande bornée,
la suite entière (`dotnet test`) et/ou `--selftest` ; chaque restauration revérifiée verte :

| Mutation | Tombe |
|---|---|
| filtre : `LobbyGuides.Of` ne filtre rien | 8 tests (LobbyGuides, CompRound) ; selftest : 5 contrôles (scénarios, lobby, case décochée) |
| filtre : `Round` classe toute la liste au lieu des jouables | 2 tests (CompRound) ; selftest : 3 contrôles |
| filtre : tribu principale ignorée | 6 tests |
| cases : les cochées seules (règle du 4 octobre) | 10 tests ; selftest : 3 contrôles |
| cases : la case ne fait plus taire les paris | 22 tests ; selftest : 3 contrôles |
| cases : une carte clé suffit à être « en cours » | 23 tests |
| stabilité : plus de préférence à la cible du tour d'avant | 2 tests |
| jauge : shiny à ×2 au lieu de ×1,8 | 2 tests |
| jauge : vert dès +20 % | 3 tests |
| jauge : courbe qui retombe ignorée | 1 test |
| simulation aveugle au lobby (`LobbyGuides.Unknown`) | selftest : 4 contrôles |
| jauge : toujours le 2ᵉ segment allumé | selftest : 5 contrôles |
| « in progress » jamais écrit | selftest : 1 contrôle |
| ligne « lobby inconnu » toujours gardée | 1 test ; selftest : 1 contrôle |

Le détail (noms des tests et contrôles tombés) est dans le rapport de la séance. Avant que les groupes de contrôles de la
simulation soient gardés, trois de ces mutations la faisaient **planter** (rapport non écrit) : ce n'était pas une
détection, et c'est ce qui a fait garder chaque groupe.
