<!-- visuel : dérogation — journal de séance court, au format Markdown des autres journaux du dépôt -->
# Journal — 2026-10-10 : mod anti-danse des sbires (issue #1)

Une recherche (client lu en métadonnées et décompilé hors du dépôt, modèle des règles en Python), puis l'implémentation :
une bibliothèque pure testée, un mod BepInEx 5 construit contre le client installé, un script de déploiement. Faits sous
WSL ; **rien n'est installé dans le dossier du jeu, rien n'est vu en jeu**, et HarmonyX n'a jamais tourné sur ce client
(Unity 6000.3.11f1, Mono). Aucune ligne du client n'entre dans le dépôt : seulement des noms de types et de méthodes.

## 1. Ce qui a été établi (code du client lu, mesures)

- **Trois écrivains** de la place d'un sbire, dans trois repères : la prédiction locale au lâcher (rang dans la rangée
  affichée), le chemin **temps réel** à la réception du paquet (`ZoneMgr.OnRealTimeZonePosChange`, carte par carte, sans
  décaler les voisines, rang sur le plateau du serveur) et le chemin **traité** quand la liste de tâches est jouée, parfois
  des secondes plus tard (rang du serveur *d'avant*). La mise en page trie par position, puis par la place traitée.
- En taverne, le drapeau « ignorer les places pures » ne bloque pas les `TAG_CHANGE ZONE_POSITION` du serveur (ils ne
  nomment pas de zone) : une place est écrite deux fois, à la réception puis à la lecture de la liste.
- Deux danses, **démontrées sur le modèle** : D-a (une sortie reçue mais pas encore animée, puis un déplacement :
  `A X B C`, A posé 3e → affiché `X B C A`, puis `X B A C`) et D-b (une liste reçue avant un déplacement, jouée après :
  `D B C` → `B D C` → `D B C`). Recherche exhaustive (plateaux de 4 à 7) : **1 724 cas sur 2 072** montrent un ordre que
  le serveur n'a jamais eu avec les règles du client, **0** avec le correctif.
- Mesuré dans les journaux d'Ali (6 sessions) : les conditions de ces danses sont **rares** chez lui (3 placements sur 405
  pendant une liste en retard portant des places du plateau ; 0 déplacement sur 111 pendant une sortie non animée).
  **Aucune danse réelle n'a été observée ni rattachée à un chemin.**
- Les six cibles (dont H6, facultative) et les 73 membres listés (60 appelés par le mod, 13 sur lesquels raisonne la
  conception) existent dans le client 36.6.3 (signatures lues en métadonnées,
  `Assembly-CSharp.dll` SHA-256 `3f677795…2b69728`).

## 2. La conception retenue

Un invariant sur la rangée du joueur en taverne : les positions valent les rangs affichés (1..n), et **un seul écrivain**
décide de l'ordre, en fin d'image (`LateUpdate`, avant le rendu) : l'ordre affiché tant que l'action du joueur n'a pas de
réponse, sinon l'ordre temps réel du serveur ; une carte que le serveur a déjà retirée mais encore affichée reste derrière
sa voisine de gauche ; les places rejouées par une liste traitée en retard sont neutralisées sur cette rangée.

| # | Cible | Rôle | Requis |
|---|---|---|---|
| H1 | `ZoneMgr.Awake` postfix | attache `NoDanceDriver` (un par partie) | oui |
| H2 | `GameState.SendOption` prefix | lit l'option et sa position avant leur effacement : « en vol » | oui |
| H3 | `PowerTask.DoRealTimeTask` postfix | marque la rangée « sale » ; suit `BACON_IN_COMBAT_PHASE` | oui |
| H4 | `ZoneMgr.AddPredictedLocalZoneChange` prefix + postfix | renumérote 1..n sans réordonner avant la prédiction ; journal | oui |
| H5 | `ZoneMgr.PostProcessServerChangeList` postfix | neutralise les places pures rejouées ; marque « sale » | oui |
| H6 | `ZoneMgr.OnRealTimeZonePosChange` prefix | saute l'écriture carte par carte (réglage, désactivé) | non |

Portée : Battlegrounds, phase de taverne animée, étape temps réel `MAIN_ACTION`, pas de combat temps réel, pas de carte
tenue ni de choix en attente. Hors portée : combat, Bob, main, adversaire, Duos, spectateur.

| Chemin | Rôle |
|---|---|
| `src/TavernCompass.NoDance.Core/` | `netstandard2.0`, sans le jeu : `BoardOrder.Target` (l'ordre), `RowReconciler` (l'état du pilote : sale, vol, délai, compteurs), `ReplayedPositions` (H5), `PatchTargets` et `ClientMembers` (les cibles et les membres comme données), `ClientSignature` |
| `tests/TavernCompass.NoDance.Core.Tests/` | modèle des règles du client (`ClientModel`, cadencé par images comme le mod), scénarios, recherche exhaustive, signatures |
| `mods/TavernCompass.NoDance/` | le plugin BepInEx (`net48`, hors solution), qui compile les sources du cœur dans sa propre DLL |
| `tools/nodance-deploy.sh` | installation depuis WSL |

## 3. Écarts avec la conception de la recherche (tranchés seul, chacun testé)

- **Ancrage des cartes sortantes** : la clé « place de la voisine + 0,5 » de la recherche fait sauter la 3e de trois
  cartes sortantes consécutives derrière la carte suivante (1 + 3 × 0,5 = 2,5 > 2). Remplacée par une clé (place de la
  voisine, après elle, ordre affiché). Test `Three_gone_cards_in_a_row_all_stay_behind_the_same_neighbour`.
- **Réconciliation quand une carte entre ou sort de la rangée** (« row changed ») : la recherche l'avait jugée inutile, en
  supposant que H5 marque la rangée à la fin de la liste ; il la marque **au début** (`PostProcessServerChangeList` précède
  `ProcessChanges`, qui attend les animations). Contre-exemple ajouté au modèle : deux jetons invoqués devant la rangée ;
  le client seul a raison (il applique les places des voisines), le correctif sans ce déclencheur affiche `S A T B` au
  lieu de `S T A B`. Test `Tokens_summoned_in_front_enter_at_their_server_place`, mutation détectée.
- **H5 ne neutralise pas** la place d'une entité qui change de zone ou de contrôleur dans la même liste (une carte rendue
  à la main y reçoit sa place dans la main).
- **Rien n'est écrit pendant un choix ouvert** (`MustWaitForChoices`) : la liste du client attendrait, et une liste
  périmée s'appliquerait ensuite ; la rangée reste « sale » jusqu'à la fermeture.
- **Une exception dans une partie requise arrête tout le mod** pour la session (le client garde son comportement) : la
  moitié du correctif pourrait faire de nouvelles danses. H6 seul s'arrête seul.
- **Une seule DLL** : le mod compile les sources du cœur, plutôt que de déposer deux DLL dont BepInEx devrait résoudre la
  dépendance (comportement non vérifié).
- Au chargement, en plus des cibles, les 60 membres que le mod appelle sont cherchés par signature ; un test vérifie que
  **chaque membre du client référencé par la DLL construite** est dans cette liste (témoin : un appel retiré de la liste
  est nommé).
- Un test de plus que la recherche : une **prédiction fausse** (le serveur met D 2e, pas 1er) finit à la place du serveur,
  avec et sans H6. La recherche exhaustive ne peut pas le voir : ses serveurs suivent toujours l'intention du joueur.

## 4. Supposé, non établi, pas vu en jeu

| Supposé | Non établi | Pas vu en jeu |
|---|---|---|
| que la danse vue par Ali soit D-a ou D-b ; que Mono n'inline pas les cibles (IL de 76 à 944 octets) ; que `Application.version` donne la version du jeu | une danse réelle rattachée à un chemin ; l'effet des réponses `MOVE_MINION` vides (37 sur 111) ; les danses en combat ; le comportement de Battle.net et des mises à jour envers BepInEx (« Analyser et réparer ») ; qu'un détour Harmony gêne HDT | **tout** : chargement de BepInEx 5.4.23.5 par ce client Unity 6, HarmonyX sur ce Mono, chaque correctif, chaque ligne de journal, l'absence de saut d'une image avec H6 désactivé |

## 5. Vérifié sans le jeu

- `dotnet test` : 48 tests du cœur ; sans `HEARTHSTONE_MANAGED`, les 4 tests de signatures sont **sautés avec la raison** ;
  avec, 129 passent (6 cibles et noms de paramètres liés, 73 membres, témoin négatif, références de la DLL du mod).
- Mutations (suite entière) : gel en vol retiré (10 tests tombent), ancrage retiré (5), tri par place serveur retiré (11),
  déclencheur « row changed » retiré (5), neutralisation retirée (10), neutralisation d'une carte qui change de zone (1).
- Témoin des signatures : une cible inventée `H7` fait échouer le test en la nommant.
- Build du mod contre le client installé, `-warnaserror` ; sans `HearthstoneManagedDir`, erreur explicite. L'archive BepInEx
  est téléchargée (vérifié dans un dossier vide) et refusée si son SHA-256 diffère (vérifié sur une archive altérée).
- `tools/nodance-deploy.sh` : `--dry-run` sur le vrai dossier (rien d'écrit) ; installation, réinstallation, plugin
  étranger, installation à moitié, désinstallation et `--purge` exercés sur un **faux** dossier de jeu.

## 6. Installation (Ali)

1. Construire : `dotnet build mods/TavernCompass.NoDance -c Release -warnaserror -p:HearthstoneManagedDir=/mnt/e/JEUX/Hearthstone/Hearthstone_Data/Managed/`
   (télécharge BepInEx dans `lib/bepinex/` et vérifie son empreinte).
2. Hearthstone fermé : `tools/nodance-deploy.sh` (ou `--game-dir …`) ; il montre la cible, installe BepInEx 5.4.23.5 si
   `winhttp.dll` et `BepInEx/core` sont absents (jamais par-dessus une autre installation), copie
   `BepInEx/plugins/TavernCompass.NoDance.dll`, compare les SHA-1 et imprime le retour arrière. Un autre plugin dans
   `BepInEx/plugins` (celui de Nomi, par exemple) est signalé : deux correctifs de l'ordre se battraient.
3. Lancer le jeu : `BepInEx/LogOutput.log` doit dire `5/5 required patches applied`.
4. Retour arrière : `tools/nodance-deploy.sh --uninstall` (le mod), `--uninstall --purge` (le mod et BepInEx) ; sans
   désinstaller : `Enabled = false` dans `BepInEx/config/com.tavern-compass.nodance.cfg`, ou `enabled = false` dans
   `doorstop_config.ini` (BepInEx entier).

Réglages (`BepInEx/config/com.tavern-compass.nodance.cfg`, lus au lancement) : `General.Enabled` (vrai),
`Fixes.SkipPerCardRealTimeWrites` (H6, faux), `Fixes.FlightTimeoutSeconds` (3, de 0,5 à 10 ; réponse la plus lente
mesurée : 2,3 s).

## 7. Le scénario de vérification d'Ali

1. **Avant le mod** : ajouter à `%LocalAppData%\Blizzard\Hearthstone\log.config` une section `[Zone]` (`LogLevel=1`,
   `FilePrinting=true`), filmer l'écran, et jouer : (a) deux exemplaires d'un sbire sur le plateau, acheter le troisième
   et, **pendant l'animation du triple**, glisser aussitôt le sbire de gauche à droite de son voisin ; (b) vendre un sbire
   puis déplacer aussitôt un autre ; (c) poser un sbire magnétique puis déplacer aussitôt un voisin. Noter l'heure de
   chaque danse vue.
2. **Avec le mod** : mêmes gestes ; attendre des lignes `reconcile` / `neutralized` aux mêmes moments, et l'absence de saut.
3. Garder `Zone.log`, `Power.log` et `BepInEx/LogOutput.log` : ils rattachent une danse réelle à D-a, D-b ou à autre chose.
   `order changes` et `neutralized` à zéro sur plusieurs parties : le correctif n'a rien fait.

| Moment | Ligne de `LogOutput.log` |
|---|---|
| chargement, par cible | `patch ZoneMgr.PostProcessServerChangeList postfix: ok` |
| chargement, bilan | `5/5 required patches applied, 0/1 optional (game …, Unity …, BepInEx 5.4.23.5, …)` ou `Tavern Compass — No Dance 0.4.0 disabled (missing: …)` |
| début de partie | `attached to ZoneMgr (game 3)` |
| action envoyée | `option sent: entity=4911 position=1 (in flight)` |
| réponse | `option answered after 287 ms` / `option rejected after … ms` / `flight timeout after 3000 ms` |
| prédiction | `prediction: entity=4911 slot=1 predicted=1 list=842 (renumbered 2)` |
| place rejouée | `server list 857 (PLAY): 2 replayed position(s) neutralized` |
| réconciliation | `reconcile (options): shown [4911 4912 4915] -> [4912 4911 4915], 2 position(s) changed` ; gelée : `…, kept visual order (in flight)` |
| fin de partie | `game summary: flights=… answered=… rejected=… timeouts=… reconciles=… order changes=… kept=… renumbered=… neutralized=… (game 3)` |
| faute | `H5 ZoneMgr.PostProcessServerChangeList postfix failed: the mod stops for the rest of the session, …` |

## 8. Les conditions de Blizzard (faits, sans avis)

EULA Blizzard (Battle.net), « LAST REVISED March 21, 2024 » :
<https://www.blizzard.com/en-us/legal/fba4d00f-c7e4-4883-b8b9-1b4500a402ea/blizzard-end-user-license-agreement>

- § 1.C « License Limitations » : « Blizzard may suspend or revoke your license to use the Platform […] if you violate
  […] the license limitations set forth below. »
- § 1.C, Derivative Works : « Copy or reproduce […], translate, reverse engineer, derive source code from, modify,
  disassemble, decompile, or create derivative works based on or related to the Platform. »
- § 1.C, Cheating, c) : « hacks; i.e. accessing or modifying the software of the Platform in any manner not expressly
  authorized by Blizzard ».
- § 1.C, Cheating, d) : « any code and/or software, not expressly authorized by Blizzard, that can be used in connection
  with the Platform and/or any component or feature thereof which changes and/or facilitates the gameplay or other
  functionality ».
- § 4, Consent to Monitor : « WHILE RUNNING, THE PLATFORM (INCLUDING A GAME) MAY MONITOR YOUR COMPUTER […] FOR
  UNAUTHORIZED THIRD PARTY PROGRAMS […] ».

Déclaration de Blizzard à PCGamesN (HearthArena, 2 novembre 2015) : « Like any third party programs for Hearthstone, we
can't condone or approve of their use. » ; « Our Terms of Use don't allow changing game files or automating game play. »
<https://pcgamesn.com/hearthstone-heroes-of-warcraft/hearthstone-arena-overlay-blizzard-say-we-can-t-condone-or-approve-of-their-use>

Le README de Nomi's Kitchen décrit son « Fix Minion Dance » (« early alpha and experimental ») et ne dit rien des
conditions de Blizzard. Fait propre à cette recherche : le client a été décompilé (dans un dossier de travail hors du
dépôt), ce que la clause « Derivative Works » nomme.

## 9. Ce qui reste à décider (Ali)

| Arbitrage | Issues |
|---|---|
| observer avant d'installer | (a) installer maintenant ; (b) d'abord un `Zone.log` d'une danse réelle ; (c) les deux, le mod journalisant ce qu'il fait |
| les conditions de Blizzard | § 8 |
| la vitrine publique | `README.md` et `ROADMAP.md` ne parlent pas du mod (décision d'Ali) ; l'issue #1 reste ouverte tant que rien n'est vu en jeu |
