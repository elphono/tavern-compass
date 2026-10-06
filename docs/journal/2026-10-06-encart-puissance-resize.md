<!-- visuel : dérogation — journal de séance, au format Markdown des autres journaux du dépôt ; la note illustrée est docs/plans/2026-10-04-panneau-unique-ergonomie.html § 12 -->
# Journal — 2026-10-06 (soir) : encart de puissance, + / − qui redimensionnent, jauge de l'adversaire

Demandes d'Ali, mot pour mot :

> « il faut améliorer l'ergonomie de target comp. Avec "move panel" on détermine la taille et l'emplacement par défaut de
> la fenêtre. Un appui sur les + ou - resize le fenêtre pour afficher les N meilleurs compos que l'on peut faire avec
> notre board seulement. au niveau design on sort l'indicateur de force de compo pour en faire un petit encart en dessous
> du cadre principal avec un joli design pour l'ensemble qui aurait un effet lumineux sur le composants "feu rouge". On
> l'encadre des + et - de l'autre fonctionnalité »

> « on rajoute un autre indicateur similaire pour la compo de l'ennemie quand elle apparait. »

Fait sous WSL (tests, simulation, build du plugin contre HDT 1.58.9) ; **rien n'est encore vu en jeu**. La note illustrée
pour Ali est le § 12 de `docs/plans/2026-10-04-panneau-unique-ergonomie.html`.

## Ce qui a été fait

| Demande | Ce qui est à l'écran | Code |
|---|---|---|
| l'indicateur sorti dans un encart sous le cadre | un encart aussi large que le panneau, 2 px sous le cadre, sur fond plus clair en haut, bord orange | `CompsPanel` (cadre + encart dans un même panneau de `PanelMover`) |
| « effet lumineux sur le composant feu rouge » | quatre feux ronds dans un boîtier sombre ; le feu du palier allumé, cerclé de blanc, avec un halo de sa couleur (or profond pour shiny) ; les autres éteints, à 22 % ; sans palier, tout gris, aucun halo | `BoardPowerView`, `BoardPowerLevels.LitLamp` / `Halo` |
| « encadré des + et − » | − à gauche de l'encart, + à droite, centrés ; le titre garde « n targets » / « k chosen » | `CompsPanel.BuildInset` |
| + / − redimensionnent pour N compos | le panneau prend la hauteur de N lignes, de la barre de titre et de l'encart | `PanelGrowth`, `CompGuideLayout.HeightFor`, `CompTargets.FitRows` |
| un indicateur pour la compo de l'ennemi | une seconde rangée, même composant : « Opp. 160 · their hero avg 143 at turn 8 » en combat, « Next opp. 70 at turn 5 · their hero avg 50 » en taverne | `OpponentPower`, `HdtEntityAdapter.OpponentFacts`, `Plugin.UpdateOpponentPower` |

## Décisions prises à la place d'Ali (réversibles)

| # | Décision | Motif | Pour revenir en arrière |
|---|---|---|---|
| 1 | **Move panels et + / −** : un panneau jamais redimensionné par sa poignée est **toujours** dimensionné sur N lignes (sa taille par défaut, c'est « N compos ») ; un panneau redimensionné garde sa boîte (la taille par défaut qu'Ali a choisie) **jusqu'à un appui sur + ou −** dans la partie, puis suit N jusqu'à la partie suivante ou un changement de mode déplacement ; « Reset » rend le premier cas. Rien de neuf dans `layout.json` ni `settings.json`. | la phrase d'Ali (« Move panel détermine la taille par défaut ; + / − resize ») et la consigne du pilote (« une partie neuve revient au défaut ») ; sans taille choisie, il n'y a pas d'autre « défaut » que N lignes, et la place par défaut ne tient trois cibles dans deux tiers qu'en montant jusqu'aux plateaux (mesure ci-dessous) | garder « dimensionné sur N » d'une partie à l'autre : un champ facultatif dans l'entrée `layout.json` (rétro-compatible) ; question pour Ali |
| 2 | **Bord d'ancrage** : le panneau garde le haut de sa boîte et grandit vers le bas jusqu'à la ligne de l'or ; une boîte posée **sous** cette ligne (en bas de l'écran) garde son bas ; sans place en dessous, il monte jusqu'à la première zone du jeu ou le premier panneau au-dessus, jamais hors de l'écran ; si même là N lignes ne tiennent pas, il en montre moins (« k of n shown ») et se resserre sur elles | « en bas de l'écran il grandit vers le haut » ; « sans chevaucher les zones du jeu » | `PanelGrowth.Place` |
| 3 | **Case cochée** : − et + restent grisés (n ne change pas) ; le panneau garde **N lignes, et au moins toutes les cibles** (cochées + en cours, quatre au plus) | **écart à la recommandation du pilote** (« dimensionné sur les cibles affichées ») : dimensionné sur la seule case cochée (souvent une ligne), le panneau cache tous les autres guides, et − / + étant grisés, Ali ne pourrait plus les faire réapparaître pour cocher une seconde compo. Vu dans la simulation : le second clic de case n'avait plus de ligne où cliquer | `CompTargets.FitRows` : `Math.Max(1, targets)` à la place de `Math.Max(wanted, targets)` |
| 4 | **« avec notre board seulement »** : **gardé plateau + main** (classement inchangé) | la main, c'est ce qu'Ali posera au tour suivant, et c'était sa toute première demande (« grâce à notre board et notre main ») ; en direct, une compo dont il tient les cartes clés en main est celle qu'il va jouer. **À confirmer par Ali** | `CompTargets.Round` : passer `cards.Board` seul à `CompGuideMatch.Rank` (une ligne, et un test) |
| 5 | **Les cibles par rang** : quand toutes ne tiennent pas, les meilleures restent, quel que soit leur tier ; une cible laissée dehors n'est jamais remplacée par un guide qui n'en est pas une | vu dans la simulation : + de 3 à 4 cibles, place pour trois, et la 3ᵉ (tier C) cédait la place à la 4ᵉ (tier B, plus haut dans la liste) — un appui sur + faisait disparaître une meilleure compo ; et un guide de S sans couleur prenait la place d'une cible de C dont les cadres étaient sur les cartes de Bob | `CompGuideLayout.Fit` (rang des cibles, `targetLeftOut`) |
| 6 | **« Quand elle apparaît »** : en combat, le plateau affronté, tel qu'HDT le fige au début du combat ; en taverne, le dernier plateau vu du prochain adversaire | ce qu'HDT expose réellement (décompilé, HDT 1.58.9) : `TagChangeActions.OnBattlegroundsCombatSetupChange` passe en combat **et** appelle `SnapshotBattlegroundsBoardState` (héros en jeu contrôlé par `game.Opponent`, ses sbires, clonés, avec le tour) ; `GameV2.GetBattlegroundsBoardStateFor(id héros)` est public ; le prochain adversaire est `NEXT_OPPONENT_PLAYER_ID` sur l'entité du joueur (déjà lu par le panneau Combats retiré). En taverne la question utile est « le prochain adversaire est-il devant ? » ; en combat, le plateau d'en face | `HdtEntityAdapter.OpponentFacts` |
| 7 | **Référence de l'adversaire** : la courbe de **son** héros (Firestone, mêmes règles « too early », « few games », « curve falls ») ; en taverne, à la moyenne du tour où son plateau a été vu ; sans courbe, gris « no curve for Rakanishu » — jamais la courbe du joueur à la place | la ligne doit dire ce qu'elle compare ; un plateau d'il y a cinq tours contre la moyenne d'aujourd'hui lirait toujours « behind » | `OpponentPower.Compare` |
| 8 | **Une rangée par jauge, dans le même encart**, chacune sous son garde-fou (`warband-curve`, `opponent-power`) | « à côté du premier », lisible d'un coup d'œil ; deux rangées tiennent dans la largeur du panneau sans couper les chiffres ; côte à côte, il aurait fallu des chiffres cryptiques | `CompsPanel.BuildInset` |
| 9 | **Le titre perd − et +**, garde « n targets » / « k chosen » | − / + sont dans l'encart ; le compte reste lisible en haut | `CompsPanel.TitleBar` |
| 10 | **Encart compté dans la hauteur gardée** de `layout.json` (même entrée, même schéma) | un seul panneau à déplacer et à redimensionner ; un fichier ancien se lit sans erreur, mais une boîte choisie avant laisse ≈ 42 px de conception de moins à la liste | — |

## Ce qui a été mesuré (simulation, 1920 × 1080, place par défaut)

| N | Panneau | Lignes |
|---|---|---|
| 1 | (1181, 691) 488 × 177 | 1, la cible 1 |
| 2 | (1181, 691) 488 × 247 | 2 |
| 3 (deux tiers) | (1181, 682) 488 × 339, bas sur l'or | 3 — à 3 px de la place totale entre les plateaux (678) et l'or (1020,6) |
| 4 | (1181, 682) 488 × 339 | 3 sur 4 : la 4ᵉ (son tier en plus) ne tient pas |

- Le budget est serré : la première version (encart de 45 px, bordure de 3 px réservée même hors mode déplacement) laissait
  la 3ᵉ cible dehors (344 px pour 342). Encart ramené à 42 px de conception, bordure réelle comptée hors mode déplacement.
- Trois cibles dans trois tiers ne tiennent pas à la place par défaut (2 lignes) : limite déjà écrite le 2026-10-06 matin.
- Contre le bas de l'écran (`--layout` en bas) : N = 1 → 177 px, bas à 1080 ; N = 3 → 339 px, toujours le bas à 1080.
- En mode déplacement, la boîte par défaut montre 2 cibles sur 3 (l'encart prend la place de l'ancienne ligne de pied, et
  une cible laissée dehors n'est plus remplacée) ; la poignée ◢ du coin couvre le + de l'encart (sans gêne : en mode
  déplacement les clics vont au déplacement).
- Pendant un choix, l'encart disparaît avec le panneau, et revient tel quel.

## Ce qui n'a pas été vu

- **Rien sous HDT réel** : le rendu des feux et du halo sur le vrai jeu, les clics de − / + au-dessus du jeu (couche
  transparente d'HDT), le redimensionnement en partie, la ligne `targets n=…`.
- **Le plateau adverse réel** : que le héros contrôlé par `game.Opponent` soit bien l'adversaire du combat au moment où le
  plugin le lit, que `GetBattlegroundsBoardStateFor` ait déjà le plateau de ce tour (sinon la rangée dit « board not read
  yet »), que `NEXT_OPPONENT_PLAYER_ID` désigne bien le joueur affronté au combat suivant, les noms de héros de HearthDb.
  La ligne `opponent power …` du journal d'HDT dira tout cela à la première partie.
- Les duos (jamais pensés pour la rangée de l'adversaire).

## À vérifier en partie (Ali)

1. L'encart sous le panneau : les feux, le halo, le badge, les chiffres ; − à gauche, + à droite.
2. Un appui sur + ou − : le panneau prend N lignes ; une ligne `targets n=… panel resized to …` par appui.
3. En combat, la seconde rangée « Opp. … » ; en taverne « Next opp. … » ou « not fought yet » ; la ligne `opponent power`.
4. Les trois arbitrages : plateau + main (4), une case cochée garde N lignes (3), la taille de + / − oubliée à la partie
   suivante (1).

## Preuves

Commandes et sorties dans le rapport de la séance ; résumé : `dotnet test` 684 tests verts (640 avant : Stats 558 → 602) ;
simulation `--selftest` 78 contrôles « ALL PASSED » (61 avant) ; build Release du plugin contre HDT 1.58.9, 0 avertissement.
Chaque test xUnit nouveau a été vu rouge avant le code, en quatre passes : 40 tests rouges contre des squelettes qui levaient
`NotImplementedException`, puis 3 (case cochée : N lignes ; boîte qui atteint l'or), 2 (encart de 42 px ; cible jamais
remplacée), 1 (cibles par rang), chacune pour une règle trouvée en regardant la simulation. Les contrôles de la simulation ont été écrits avec le rendu : leur capacité à tomber est montrée par les
mutations ci-dessous.

Mutations, sur une copie du dépôt (`git archive`, jamais l'arbre de travail), `bin/obj` purgés avant chaque essai, la suite
entière (`dotnet test`, 684 tests) et `--selftest` (78 contrôles) à chaque mutation, chaque commande bornée par `timeout` ;
copie verte avant la campagne et après la restauration. **25 mutations, 25 détectées, aucune survivante.**

| # | Mutation | Tests xUnit tombés | Contrôles de la simulation tombés |
|---|---|---|---|
| M01 | hauteur de N lignes : une ligne de trop | 7 (`HeightFor_…EachHeaderOnce`, `HeightFor_IsTheRoom…AtEveryWindowHeight`, `Fit_TargetsThatDoNotAllFit_AreTakenByRank…`) | 2 (+ / −) |
| M02 | hauteur de N lignes : barres de tier non comptées | 8 (mêmes) | 4 (+ / −, lobby inconnu, ligne de contexte) |
| M03 | cibles prises dans l'ordre de la liste, pas par rang | 1 (`Fit_TargetsThatDoNotAllFit_AreTakenByRank…`) | 1 (+ / − : N = 1 à 4) |
| M04 | une cible laissée dehors remplacée par un guide non ciblé | 1 (`Fit_ATargetLeftOut_IsNeverReplacedByAGuideThatIsNotOne`) | 0 — la simulation n'a pas ce cas, le test le tient |
| M05 | ancrage : ne garde jamais son bas | 1 (`ABoxAgainstTheBottomOfTheScreen…`) | 1 (« a panel against the bottom of the screen keeps its bottom ») |
| M06 | ancrage : une boîte qui atteint l'or garde son bas | 1 (`ABoxEndingOnTheGoldLine_KeepsItsTop…`) | 1 (+ / −) |
| M07 | borne : monte à travers les plateaux | 3 (`OutOfRoomBelow…`, `ABoxAgainstTheBottom…`, `AnotherPanelAboveIt…`) | 2 (+ / −, lobby inconnu) |
| M08 | borne : descend sur une zone en dessous | 2 (`ABoxAboveTheBoards…`, `AMinimumLargerThanTheRoom…`) | 0 — pas de panneau au-dessus des plateaux dans la simulation |
| M09 | borne : pas de bornage à l'écran | 1 (`AMinimumLargerThanTheRoom…`, cas ajouté avant la campagne) | 0 |
| M10 | halo : shiny pâle au lieu d'or | 1 (`Halo_TheLevelsOwnColour…`) | 0 — la simulation compare le halo dessiné à `Halo` lui-même : elle tient le câblage, le test la valeur |
| M11 | halo : le gris brille | 1 (`Halo_…`) | 0 — aucun feu allumé sans palier, donc aucun halo à dessiner (seconde garde) |
| M12 | feu : « even » allume le vert | 1 (`Lamps_OneLitPerLevel…`) | 2 (board power « even », opponent power « even ») |
| M13 | rendu : tout feu allumé brille en jaune | 0 | 7 (les paliers behind, ahead, shiny des deux rangées, next) |
| M14 | − / + actifs avec une case cochée | 3 (`CountAdjustable_…`, `FitRows_…`) | 1 (« − and + are dim and do nothing ») |
| M15 | rendu : − jamais grisé | 0 | 3 (− et + grisés, retour aux cibles automatiques, lobby inconnu) |
| M16 | case cochée : panneau dimensionné sur les seules cibles | 2 (`FitRows_…`) | 0 — la simulation coche dans une grande boîte |
| M17 | adversaire en combat : la courbe du joueur | 4 (`InCombat_…NotYours`, `TheirHeroWithoutCurve…`, `TheRulesOfYourOwnGauge…`, `LogLine_…`) | 6 (opponent power behind, even, ahead, shiny, none, ligne de journal) |
| M18 | adversaire en taverne : la courbe du joueur | 1 (`InTheShop_TheNextOpponent…`) | 1 (opponent power « next ») |
| M19 | adversaire sans courbe : repli sur celle du joueur | 1 (`TheirHeroWithoutCurve_GreyAndNamed_NeverYourCurveInstead`) | 1 (opponent power « none ») |
| M20 | adversaire en taverne : la moyenne du tour courant | 1 (`InTheShop_…AtTheTurnItWasSeen`) | 1 (opponent power « next ») |
| M21 | adversaire en combat : plateau d'un tour antérieur accepté | 1 (`WithoutABoard_GreyWithTheReason`) | 0 |
| M22 | un appui ne dimensionne pas un panneau redimensionné | 0 | 1 (« a box given by the handle stays until + or − is pressed… ») |
| M23 | la partie suivante garde la taille de + / − | 0 | 1 (même contrôle) |
| M24 | boîte périmée : placé depuis le dernier redessin | 0 | 1 (« a panel against the bottom of the screen keeps its bottom ») |
| M25 | les deux rangées sous un même garde-fou | 0 | 1 (guard) |

Non éprouvé par mutation, faute de pouvoir l'exécuter hors d'HDT : `HdtEntityAdapter.OpponentFacts` (le choix du héros en jeu
contrôlé par `game.Opponent`, la lecture de `NEXT_OPPONENT_PLAYER_ID`). La ligne `opponent power …` du journal d'HDT est la
mesure qui manque.
