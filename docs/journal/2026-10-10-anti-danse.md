<!-- visuel : dérogation — journal de séance court, au format Markdown des autres journaux du dépôt -->
# Journal — 2026-10-10 : mod anti-danse des sbires (issue #1)

Une recherche (client lu en métadonnées et décompilé hors du dépôt, modèle des règles en Python), puis l'implémentation :
une bibliothèque pure testée, un mod BepInEx 5 construit contre le client installé, un script de déploiement. Faits sous
WSL ; **rien n'est installé dans le dossier du jeu, rien n'est vu en jeu**, et HarmonyX n'a jamais tourné sur ce client
(Unity 6000.3.11f1, Mono). Aucune ligne du client n'entre dans le dépôt : seulement des noms de types et de méthodes.

**État : mod écrit, non installé, en attente d'observation.** Ali a décidé d'observer d'abord une danse réelle **sans le
mod** (§ 10 : marche à suivre, outil `tools/nodance-observe.py`) ; l'auteur de Nomi's Kitchen a autorisé l'analyse et la
reprise du code de son propre mod anti-danse (§ 11).

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
  **Aucune danse réelle n'a été observée ni rattachée à un chemin.** Ces fréquences sont à revérifier : les scripts
  retrouvaient le joueur une fois par fichier, qui contient plusieurs parties (§ 10).
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
| H4 | `ZoneMgr.AddPredictedLocalZoneChange` prefix + postfix | renumérote 1..n sans réordonner avant la prédiction ; après, une carte posée depuis la main est montrée à sa place serveur (C2, § 12) ; journal | oui |
| H5 | `ZoneMgr.PostProcessServerChangeList` postfix | neutralise les places rejouées et celles que le client invente en fusionnant (C1, § 12) ; marque « sale » | oui |
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
- **H5 ne neutralise pas** la place d'une entité qu'un changement **envoyé par le serveur** fait changer de zone ou de
  contrôleur dans la même liste (une carte rendue à la main y reçoit sa place dans la main) ; ceux qu'invente le client en
  fusionnant ne comptent pas (C1, § 12).
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

- `dotnet test` : 56 tests du cœur ; sans `HEARTHSTONE_MANAGED`, les 4 tests de signatures sont **sautés avec la raison** ;
  avec, 142 passent (6 cibles et noms de paramètres liés, 78 membres, témoin négatif, références de la DLL du mod).
- Mutations (suite entière) : gel en vol retiré (10 tests tombent), ancrage retiré (5), tri par place serveur retiré (11),
  déclencheur « row changed » retiré (5), neutralisation retirée (10), neutralisation d'une carte qui change de zone (1) ;
  après le § 12 : C1 retiré (8), C1b retiré (1), C1 et C1b ensemble (12), C2 retiré (2), ligne d'écriture tardive muette (1).
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
| place rejouée | `server list 857 (PLAY): 2 replayed position(s) neutralized, 3 merged position(s) neutralized` |
| écriture tardive (tranche C1 en jeu) | `position written by a server list after the row was reconciled: [4912: 2->1, 4911: 1->2]` |
| carte posée depuis la main | `reconcile (drop): shown [4911 4920 4913 4915] -> [4911 4913 4920 4915], 2 position(s) changed, placed at server slot 3 (drawn slot 2)` |
| réconciliation | `reconcile (options): shown [4911 4912 4915] -> [4912 4911 4915], 2 position(s) changed` ; gelée : `…, kept visual order (in flight)` ; pendant une liste serveur : `reconcile (server list active): …` |
| fin de partie | `game summary: flights=… answered=… rejected=… timeouts=… reconciles=… order changes=… kept=… renumbered=… neutralized=… (merged …) drops=… late writes=… (game 3)` |
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

## 9. Décisions d'Ali, et ce qui reste à décider

| Arbitrage | Issues | Rendu (2026-10-10) |
|---|---|---|
| observer avant d'installer | (a) installer maintenant ; (b) d'abord un journal `Zone` d'une danse réelle ; (c) les deux | **(b)** : observer d'abord, sans le mod (§ 10) ; le mod reste non installé |
| la vitrine publique | annoncer le mod ou non | **discret** : `README.md` et `ROADMAP.md` n'en parlent pas ; l'issue #1 reste ouverte tant que rien n'est vu en jeu |
| le mod de Nomi | lire seulement ; analyser et reprendre | accord de son auteur (§ 11) |
| les conditions de Blizzard | § 8 | à décider |

## 10. Observer une danse réelle, sans le mod (marche à suivre d'Ali)

Constaté en lecture seule sur la machine de jeu le 2026-10-10 : `log.config` existe, avec sept sections (`Achievements`,
`FullScreenFX`, `Net`, `Power`, `Decks`, `Arena`, `LoadingScreen`), chacune de cinq lignes, fins de ligne Windows ;
**aucune section `[Zone]`**. Les journaux du jeu tombent dans `E:\JEUX\Hearthstone\Logs\`, un dossier par lancement du
jeu, nommé à l'heure du lancement (`Hearthstone_2026_10_10_09_14_34\`) ; dedans `Power.log` pendant la session, et
`Power_old.log` dans les sessions terminées (un fichier tourné peut commencer au milieu d'une partie). Chaque ligne commence
par l'heure locale de la machine, sans la date : `D 09:14:55.6078907 GameState.DebugPrintPower() - …`.

1. **Activer le journal `[Zone]`**, Hearthstone fermé (HDT peut rester ouvert) : ouvrir
   `%LocalAppData%\Blizzard\Hearthstone\log.config` dans le Bloc-notes et ajouter à la fin, sur le modèle des sections
   présentes :

   ```
   [Zone]
   LogLevel=1
   FilePrinting=True
   ConsolePrinting=False
   ScreenPrinting=False
   Verbose=True
   ```

   `Verbose=True` par prudence, comme `[Power]` : non établi qu'il soit nécessaire pour `Zone`. Au lancement suivant, un
   `Zone.log` devrait apparaître à côté de `Power.log` (supposé par analogie avec les autres sections, pas encore vu).
   **HDT ne retire pas cette section** (lu dans HDT 1.55.6, sous licence MIT : il relit toutes les sections, ajoute les cinq
   qu'il exige — `Achievements`, `Arena`, `FullScreenFX`, `LoadingScreen`, `Power` — et, s'il réécrit le fichier, y remet
   `LogLevel=1` et `FilePrinting=True` partout, sections inconnues comprises ; non relu dans la version 1.58 installée).
   **Retour arrière** : supprimer ces six lignes, jeu fermé ; HDT ne remet pas `[Zone]`, qu'il n'exige pas.
2. **Jouer normalement**, en essayant les gestes du § 7 (triple, vente, aimant, suivis aussitôt d'un déplacement), et
   enregistrer l'écran si possible : barre de jeu Windows (`Win+Alt+R` démarre et arrête l'enregistrement, `Win+Alt+G`
   garde les 30 dernières secondes si l'enregistrement en arrière-plan est activé) ou OBS (tampon de relecture). Sinon, la
   note suffit.
3. **À chaque danse vue, noter** :
   - l'**heure à la seconde**, à l'horloge de Windows (c'est celle des journaux) ;
   - le **geste qui précède** : déplacement, achat, vente, triple, aimant, découverte, effet de début ou de fin de tour… ;
   - **ce qui bouge** : quels sbires, de quelle place à quelle place, et s'ils reviennent ou non ;
   - **taverne ou combat**.
4. **Rendre** :
   - le **dossier entier** de la session, `E:\JEUX\Hearthstone\Logs\Hearthstone_<date et heure du lancement>\`
     (`Power.log` et/ou `Power_old.log`, `Zone.log` et/ou `Zone_old.log`) ;
   - les heures notées et ce qui a été vu ;
   - la vidéo s'il y en a une ;
   - `%AppData%\HearthstoneDeckTracker\Logs\hdt_log.txt` si l'overlay d'HDT a bougé lui aussi.

   Ces journaux contiennent les pseudos des adversaires : ils ne vont **ni dans le dépôt, ni dans une issue**.

**Lecture, en une commande** (lecture seule ; les noms de joueurs ne sont jamais écrits) :

```bash
python3 tools/nodance-observe.py --at 21:14:05 --at 21:20:31 [--zone Zone.log] Power_old.log Power.log
```

Pour chaque heure, l'outil liste les options envoyées et les réponses du serveur dans les ± 5 s (`--window`), puis dit si,
au moment d'un placement ou d'un déplacement, la fenêtre **D-a** (une entrée ou sortie de la rangée reçue, pas encore jouée)
ou **D-b** (un bloc portant des places de la rangée reçu avant l'option, joué après) était ouverte ; sinon « neither D-a
nor D-b: cause not established ». Le joueur est retrouvé **partie par partie** (un fichier en contient plusieurs, et son
identifiant change d'une partie à l'autre : 1 puis 2 dans un fichier du 2026-10-10). Avec `--zone`, il ajoute les
déplacements du journal `Zone` dans la fenêtre. Tests : `python3 tools/test_nodance_observe.py`, 11 cas sur des journaux
inventés, mutations détectées (fenêtre D-a sans « pas encore jouée », D-b jamais marqué, minuit, un seul joueur par
fichier). Essayé en lecture seule sur un `Power_old.log` réel de 80 Mo : moins d'une seconde.

**Constaté en écrivant l'outil, à reprendre** : les scripts de la recherche (`window.py`, `lag_at_send.py`,
`moves_exposed.py`) votent le joueur **une fois par fichier** ; or un même fichier contient plusieurs parties où son
identifiant change. Les fréquences du § 1 (« 3 placements sur 405 », « 0 déplacement sur 111 ») peuvent donc mêler le
joueur et un autre : **non revérifiées**.

## 11. Le mod de Nomi's Kitchen : accord de son auteur (2026-10-10)

Accord personnel du développeur de Nomi's Kitchen, obtenu par Ali, malgré la licence « MIT NON-AI » : l'analyse du mod
`com.community.hs.NomiCantDance` par décompilation et désobfuscation, et la reprise de son code, sont autorisées. Tout code
repris est marqué à la source, en anglais (`Taken from NomiCantDance (Nomi's Kitchen), with its author's permission,
2026-10-10`), parce que le dépôt est public et sous MIT seule ; on préfère réécrire quand c'est aussi simple, on reprend
tel quel ce que la décompilation montre plus juste que notre version. L'analyse est faite (§ 12) : **rien de son code n'a
été repris**, bien que l'accord le permette ; le dépôt ne contient aucun code de Nomi.

## 12. Ce que montre le mod de Nomi (lu avec l'accord de son auteur)

Lu par décompilation et désobfuscation, hors du dépôt ; résumé ici en nos mots, sans extrait.

- **Deux versions.** 1.0.0, ressource du plugin HDT de Nomi's Kitchen (commit du 2026-09-24), sept cibles : celles que la
  note du 2026-10-08 nommait. 1.1.2, intégrée au mod Firestone de Nomi's Kitchen (publié le 2026-09-30), dix cibles : les
  sept, plus `ZoneMgr.OnRealTimeZonePosChange`, `PowerProcessor.OnPowerHistory` et `PowerProcessor.FlushDelayedRealTimeTasks`.
- **Quatre corrections, nommées dans ses journaux.** *drop* : une carte posée depuis la main est montrée d'emblée à sa
  place serveur, pas au rang dessiné ; *merge* : une carte en attente n'est plus replacée par l'heuristique du client
  quand il fusionne une liste serveur ; *replay* : une liste jouée en retard ne renvoie plus une carte à une place plus
  ancienne (notre D-b), par un horodatage des tâches au nombre d'options envoyées ; *magnet* (1.1.2) : tant qu'un sbire
  magnétique fusionné reste affiché, les écritures temps réel sont regroupées par paquet et toute la rangée est recalée.
- **Ce qu'il ne fait pas** : rien pour un déplacement sur le plateau pendant une sortie non animée (notre D-a), hors
  pièce magnétique ; rien en combat ; aucun gel pendant le vol ; aucun écrivain à chaque image.
- **Sa méthode contre la nôtre.** Lui corrige **dans** la réconciliation du client, étape par étape (prédiction, fusion,
  post-traitement), avec un écrivain global seulement autour des pièces magnétiques ; nous, un écrivain unique en aval, à
  chaque image, plus un filtre des places rejouées.
- **Sa ligne de chargement** (1.0.0) : « This mod modifies the Hearthstone client; Blizzard's terms do not allow client
  modification. »
- **Ne pas installer les deux mods ensemble** : ils patchent les mêmes méthodes du client, sans coordination
  (`tools/nodance-deploy.sh` signale un autre plugin présent).

Ce que la lecture a changé chez nous, chaque règle réécrite en nos termes, chacune avec un test qui échouait d'abord :

| | Constat | Règle (réécrite) | Test |
|---|---|---|---|
| **C1** (défaut réel) | quand une prédiction du joueur est en attente, le client fusionne la liste serveur et ajoute, pour **chaque** carte de la rangée, un changement qui nomme la zone, **sans tâche** (`ZoneMgr.MergeServerChangeList`) ; H5 prenait ces cartes pour des transferts et **ne neutralisait plus rien**, précisément dans le cas D-b. Le modèle, une fois la fusion modélisée, le montre partout : 1 628 cas faux sur 2 072 avec l'ancienne règle | seul un changement envoyé par le serveur (porteur d'une tâche) fait d'une entité un transfert ; un changement inventé par le client en fusionnant ne déplace jamais une carte déjà dans la rangée : sa position est neutralisée, l'écrivain range | `Positions_of_a_list_the_client_merged_are_still_neutralized`, `Sell_then_move_with_the_sale_list_merged_and_written_a_frame_later_never_shows_BDC` |
| **C1b** (défense en profondeur) | une liste serveur peut écrire des positions plusieurs images après son post-traitement, quand la marque qu'elle a laissée est déjà consommée | tant qu'une liste serveur est en cours (`ZoneMgr.HasActiveServerChange`), la rangée est sale à chaque image | `While_a_server_list_is_active_a_late_write_is_reconciled_at_the_next_frame` |
| **C2** (amélioration, idée *drop*) | au lâcher d'une carte de la main, nous gardions le rang dessiné pendant le vol (`ANTB` ≈ 300 ms, puis `ATNB` quand le jeton reste collé à son créateur) | au lâcher d'une carte venue d'une autre zone, l'ordre gelé est celui que le serveur aura : la carte à la place serveur que le client a prédite, les cartes du serveur dans leur ordre temps réel autour, les sortantes à leur ancre ; pas tant qu'un sbire de la rangée est une cible magnétique (non étudié) | `A_card_played_from_the_hand_is_shown_at_its_server_slot_from_the_drop` |

Mutations : C1 retiré, 8 tests tombent (dont la recherche exhaustive) ; C1b retiré, 1 (son test : C1 couvre seul les
scénarios) ; les deux ensemble, 12 (les scénarios de vente et de sortie) ; C2 retiré, 2. Pour trancher C1 en jeu, l'écrivain
écrit `position written by a server list after the row was reconciled: [...]` quand, pendant une liste serveur et sans
paquet temps réel ni prédiction, il trouve une position de la rangée différente de celle qu'il y a laissée.

**Question ouverte, à trancher après observation (C2')** : la même règle pour un **déplacement** sur le plateau (montrer
d'emblée la place serveur plutôt que le rang dessiné). Nomi ne le fait pas, et rien n'est observé.
