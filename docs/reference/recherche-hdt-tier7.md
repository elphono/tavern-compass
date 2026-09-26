<!-- visuel : dérogation — inventaire de référence, tableaux de données brutes -->

> Recherche du 2026-09-26 (session de reprise, agent de recherche). Source de la spec
> `docs/plans/2026-09-26-parite-tier7-spec.md`. Conservée telle quelle, non mise à jour depuis.

## Inventaire des fonctionnalités d'overlay Battlegrounds — HDT / Tier7

Légende colonne **Source** : `LOG` = dérivable de Power.log seul · `MEM` = lecture mémoire (HearthMirror/Reflection) · `EXT` = base externe (stats agrégées HSReplay) · `RED` = contenu rédactionnel (guides Jeef).

### A. Fonctionnalités gratuites (HDT seul)

| Fonctionnalité | Affiche | Phase / déclencheur | Source | Preuve |
|---|---|---|---|---|
| **Bob's Buddy** (combat odds) | % victoire / égalité / défaite, % létal, % mort | Combat (option « show results during combat phase ») + option phase de boutique | `LOG` — `BobsBuddyInvoker.cs` lit `_game.Player/_game.Opponent`, `gamePlayer.Board`, `.Secrets`, `.Trinkets`, `.QuestRewards` (modèle issu des logs, **pas** Reflection) | changelog + source |
| **Minion Browser** | Pool de sbires/sorts réellement présents dans la partie, filtres tier / type / mécanique (Activate, Lockbox, Rally, Avenge…) | Permanent, bouton ; depuis v1.56.3 aussi **entre** les parties | `LOG` + DB de cartes locale. v1.58.3 : « Reworked the minion browser to be driven off the game's library » | v1.42.0, v1.46.0, v1.55.11, v1.58.2/3 |
| **Counters** (compteurs) | Compteurs actifs : Eternal Knight, Ancestral Automaton, Blood Gem Barrage, Free Refreshes, Max Gold, Deity size, Incindius… | Permanent, mode « Auto » par compteur | `LOG` (tags de compteur sur les entités) | v1.48.23, v1.50.1, v1.55.13, v1.58.0 |
| **Session widget / panneau de session** | MMR de départ + MMR courant + delta, historique des 8 dernières parties avec placement et **plateau final**, **types de sbires disponibles / bannis** | Permanent, hors et pendant partie | MMR = `MEM` ; tribus = `MEM` (voir point 3) ; placements = `LOG` | HSTracker 2.1.5, 2.7.3, 2.7.6, 3.6.11 ; HDT v1.25.3, v1.31.7 |
| **Last known board** (survol d'un joueur) | Dernier plateau vu de ce joueur, son tier de taverne, sa **deity size** (S14+) | Boutique, **au survol** du portrait sur le leaderboard | Plateau = `LOG` (plateaux révélés au combat) ; **la cible du survol = `MEM`** (« hovered leaderboard entity ID » exposé par HearthMirror) | HDT v1.28.1, v1.56.1, v1.58.0 ; HSTracker 3.6.12, issue #1423 |
| **Hero Guides** | Guide rédigé (Jeef) pour chaque héros proposé | Sélection de héros, onglet | `RED` | « Added **free** Hero Guides… written by Jeef » v1.38.6 |
| **Comp Guides** | Guide rédigé de composition | Boutique, onglet | `RED` | « Added **free** Comp Guides » v1.38.0 |
| **Quest Guides** | Guide rédigé quêtes/récompenses | Choix de quête | `RED` | « Added **free** Quest Guides » v1.46.16 |
| **Anomaly Guides** | Guide rédigé d'anomalie | Sélection de héros | `RED` | « Added **free** Anomaly Guides » v1.43.0 |
| **Buddies Guides** | Guide rédigé des buddies | Sélection de héros | `RED` | v1.47.12 |
| **Tavern Tier 7 auto** | Affiche les sbires de tier 7 quand Norgannon's Reward / Thorim est présent | Boutique, automatique | `LOG` | v1.23.11, v1.32.1, v1.46.10 |
| **Dark Paradox indicator** | Pastille orange sur le Dark Paradox de la partie | Boutique | `LOG` | v1.58.3 (2026-09-24) |
| **Extension Twitch** | Les spectateurs voient les Dark Gifts au survol | Diffusion | `LOG` | v1.56.5 |

### B. Fonctionnalités Tier7 (payant — gratuites **2 parties/semaine**)

| Fonctionnalité | Affiche | Phase / déclencheur | Source | Preuve |
|---|---|---|---|---|
| **Hero Picking / Hero Picker** | Par héros proposé : tier **S/A/B/C/D/F**, placement moyen, pick rate — **ajusté à ta tranche de MMR** | Sélection de héros, permanent | `EXT` (stats agrégées, MAJ horaire) + `LOG` (quels héros sont proposés : `BACON_HERO_CAN_BE_DRAFTED`) | v1.27.3, v1.29.0 (Duos), tweet « Hero Pick Overlay » |
| **Composition Stats / Comp Picker** | Meilleures comps pour ce héros et **ce lobby**, classement par tier, pièces clés, chemins de transition, **Hero Affinity** | Boutique, onglet | `EXT` | v1.29.7, v1.48.0 |
| **Trinket picking stats** | Placement moyen par trinket proposé | Choix de trinket | `EXT` | v1.34.0 |
| **Quest / reward picking stats** | Stats par quête et par récompense, ajustées au MMR | Choix de quête | `EXT` | HSTracker 2.3.8 « Tier 7 overlays for Hero **and Quest** picking » |
| **Anomaly overlay masking** | Masque/adapte l'overlay de draft selon l'anomalie du lobby | Sélection de héros | `EXT` + `LOG` (`BACON_GLOBAL_ANOMALY_DBID`) | v1.43.0 |
| **Tavern Pinning** | Épingler une carte de la taverne pour la garder visible toute la partie | Boutique, clic | `LOG` | v1.48.6/7 « New Tier7 feature » |
| **Comp Key Pieces Pinning** | Signale quand une pièce clé d'une comp du lobby est proposée (« When to Commit », « Common Enablers » de Jeef) | Boutique, automatique | `EXT` + `LOG` | v1.48.6/7 |
| **Board Inspiration Tool** | Plateaux réels de parties haut-MMR récentes ressemblant à ce que tu construis | Boutique, onglet | `EXT` | i18n « INSPIRATION TOOL » |
| **Minion lineup click-through** | Clic sur un sbire du browser → comment les top players l'intègrent | Boutique, clic | `EXT` | v1.48.0 « New Tier7 feature » |
| **Timewarped Taverns notification** | Compare les cartes Timewarped HSReplay filtrées sur les choix présentés | Début de Timewarped Tavern | `EXT` | v1.48.23 |
| **Meta Snapshot button** | Accès au snapshot méta (bouton configurable) | Permanent | `EXT` | v1.58.0 |

### C. Éléments de ta liste **non confirmés** dans HDT/Tier7

| Élément demandé | Verdict |
|---|---|
| **Prochain adversaire** (affichage dédié) | **Non trouvé** comme fonctionnalité HDT nommée. Le tag existe pourtant : `NEXT_OPPONENT_PLAYER_ID = 1360`, présent dans les données de test réelles → dérivable du log. |
| **Chronomètre / timer de tour** | **Non trouvé**. Aucune entrée de changelog. |
| **Compteur de tour** | **Non trouvé** comme widget dédié ; `TURN = 20` existe. |
| **Triples** | Contradiction non tranchée : `PLAYER_TRIPLES = 1447` existe, Tavern-Lens (log seul) prétend afficher les triples des adversaires, mais l'issue HDT #4039 affirme « the only info that gets passed through the Powers log is the current Tavern Tier » pour les autres joueurs. **À mesurer sur un log réel.** |
| **Or / coût de montée de niveau** | Compteur **Max Gold** existe (v1.58.0). Pas de tag `TECH_LEVEL_MANA_GEM_COST` : **non trouvé**. Ce qui existe : `TECH_LEVEL_MANA_GEM = 1442`. |
| **MMR / rang leaderboard des adversaires** | **Non trouvé** dans HDT/Tier7 en standard. Fait uniquement par des plugins tiers (MMRadar, hsbg.cards, HDT_BGrank) qui combinent noms de lobby via HearthMirror + API externe (wallii.gg). |
| **Statistiques post-partie** | Session recap (8 dernières parties, placement, plateau final) dans HDT ; stats détaillées sur hsreplay.net « My Stats » après upload. |
| **Plateau des adversaires en temps réel** | **Impossible sans mémoire.** Le log ne donne que le *dernier plateau vu*. |

---

## Point 1 — Logs seuls, ou mémoire ?

**HDT lit les deux. Confirmé.** HearthMirror est bien une bibliothèque de lecture de la mémoire du processus Hearthstone (runtime Mono/Unity, localisation du root AppDomain par offsets).

| Donnée | Source réelle |
|---|---|
| Plateaux pour Bob's Buddy, entités, tags, phases | **Log** (`BobsBuddyInvoker.cs` → `Core.Game`, `gamePlayer.Board`) |
| **Lobby info BG** : `Name`, `AccountId`, `HeroCardId` de chaque joueur | **Mémoire** — `Reflection.Client.GetBattlegroundsLobbyInfo()` via `HearthMirrorBattlegroundsLobbyInfoProvider` |
| Mode BG sélectionné (solo/duos) | **Mémoire** |
| **Entité du leaderboard survolée** (déclencheur du « last known board ») | **Mémoire** |
| Plateau du coéquipier (Duos) | **Mémoire** |
| **Liste des tribus disponibles/bannies au tour 0** | **Mémoire** (voir point 3) |
| MMR courant | **Mémoire** |

La FAQ HDT n'aborde pas la technique et se contente de citer Ben Brode (« any app that duplicates what you can do with a pencil and paper already is fine »). Les affirmations « HDT ne lit que les logs » qu'on trouve sur des sites de téléchargement sont **fausses** pour Battlegrounds.

## Point 2 — Mécaniques BG actives (saison 14, au 2026-09-26)

| Mécanique | Statut | Source |
|---|---|---|
| **Trinkets** | ✓ **Actif** — « Trinkets are sticking around for another season and will be available alongside Dark Gifts! » | 36.2 |
| **Dark Gifts** | ✓ **Actif** (nouveau S14) — 3 or, à partir du tour 3, 3 usages/partie, 43 Dark Gifts | 36.2 / annonce S14 |
| **Activate** (mot-clé) | ✓ **Actif** (nouveau S14) | 36.2 |
| **Lockbox** (pirates) / **Fishbait** (bêtes) | ✓ **Actifs** (nouveaux S14) | 36.2 |
| **Aberrations + Deity** (C'Thun / Y'Shaarj) | ✓ **Actif** depuis 36.6.1 (2026-09-22), nouveau type de sbire | 36.6 |
| **Naga** | ✗ **Rotationné hors du pool** temporairement par 36.6 | 36.6 |
| **Anomalies** | ✗ **Retirées** à l'entrée en S14 — revenues en 35.6 (mi-S13, juin 2026, 7 nouvelles + 17 de retour), puis « Anomalies from the previous season were also removed » | 35.6, Game8 |
| **Quests / Rewards** | **Non trouvé** dans les notes S14 → présumé inactif, **non confirmé** |
| **Buddies** | **Non trouvé** dans les notes S14 ; revenus en 33.6 (sept. 2025) → **non confirmé** |
| **Duos** | Mode de jeu distinct, présumé actif (HDT le supporte toujours), **pas de confirmation explicite trouvée pour S14** |
| **Timewarped Taverns** | Actif au moins jusqu'à v1.58.0 (2026-09-22 : « Fixed the overlay not showing fully during a Timewarp tavern ») |

Note utile : ces mécaniques sont **détectables dynamiquement** dans le log via des tags de bascule — `BACON_QUESTS_ACTIVE`, `BACON_TRINKETS_ACTIVE`, `BACON_BUDDY_ENABLED`, `BACON_GLOBAL_ANOMALY_DBID`, `BACON_DARK_GIFTS_ACTIVE`, `BACON_ALT_TAVERN_SYSTEM_ACTIVE`. Coder contre ces tags plutôt que contre une saison.

## Point 3 — Ce que Power.log contient sur les adversaires

**Confirmé par données réelles** (`hsreplaynet-tests/replays/battlegrounds_*.annotated.xml`) :

| Information adversaire | Dans le log ? |
|---|---|
| Entité héros (cardID + nom : `TB_BaconShop_HERO_14` « Queen Wagtoggle ») | ✓ `FullEntity` en zone `SETASIDE` |
| `PLAYER_ID` de chaque héros adverse | ✓ tag 30 |
| Place au classement | ✓ `PLAYER_LEADERBOARD_PLACE` (1373) |
| Tier de taverne | ✓ `PLAYER_TECH_LEVEL` (1377), mis à jour en cours de partie |
| PV / dégâts | ✓ `HEALTH` (45) + `DAMAGE` (44) sur l'entité héros |
| Pouvoir héroïque | ✓ `HERO_POWER` (380) → dbfId du pouvoir |
| Armure | `ARMOR` (292) **absent** de ce replay (2020) mais utilisé par `BobsBuddyInvoker` → présent sur les versions modernes |
| Prochain adversaire | ✓ `NEXT_OPPONENT_PLAYER_ID` (1360) |
| Joueur factice (Bob) | ✓ `BACON_DUMMY_PLAYER` (1349) |
| Héros draftables | ✓ `BACON_HERO_CAN_BE_DRAFTED` (1491) |
| **Plateau adverse** | ✓ mais **uniquement au combat** — d'où le « last known board ». Pas d'accès au plateau courant d'un autre joueur pendant ta boutique. |
| **Tribus bannies / disponibles** | ✗ — « The tribe list is the one thing Hearthstone never writes to any log » (bgtracker). Inférable seulement en voyant apparaître des sbires. C'est pourquoi le panneau de session HDT/HSTracker l'affiche dès le tour 0 : il vient de la mémoire. |

⚠ Le fichier `hslog-tests/36393_battlegrounds.power.log` existe bien mais dépasse 10 Mo — **non récupérable** par WebFetch. Son voisin `139963_battlegrounds_perfect_game.power.log` existe aussi. Les XML annotés ci-dessus portent la même information sous forme exploitable (attribut `GameTagName` en clair).

## Point 4 — Listes publiques de GameTag

| Source | URL | Couverture BG |
|---|---|---|
| **python-hearthstone** (la plus complète, à jour) | `https://github.com/HearthSim/python-hearthstone/blob/master/hearthstone/enums.py` — brut : `https://raw.githubusercontent.com/HearthSim/python-hearthstone/master/hearthstone/enums.py` | ✓ ~140 tags `BACON_*`, enum `Race`, `BnetGameType`, constante `BATTLEGROUNDS_RACES` |
| **HearthDb** (.NET, `Enums.cs` généré) | `https://github.com/HearthSim/HearthDb` | ✓ mêmes tags, consommé par HDT |
| **HearthstoneJSON** (doc `mechanics`) | `https://hearthstonejson.com/docs/cards.html` | partiel (tags booléens de carte seulement) |
| Wiki Fandom « GameTag enumeration » | `https://hearthstone.fandom.com/wiki/GameTag_enumeration` | **inaccessible** (HTTP 402) — non vérifié |

---

## Bloc — tags Power.log identifiés

Tous vérifiés dans `python-hearthstone/hearthstone/enums.py` (deux lectures concordantes), sauf mention contraire. `[R]` = vu en plus dans des données de replay BG réelles.

```
── Identité / joueurs ──────────────────────────────────────────
BACON_DUMMY_PLAYER                  = 1349   [R]  Bob (contrôleur des héros adverses)
NEXT_OPPONENT_PLAYER_ID             = 1360   [R]
PLAYER_LEADERBOARD_PLACE            = 1373   [R]
PLAYER_TECH_LEVEL                   = 1377   [R]  tier de taverne du joueur
PLAYER_TRIPLES                      = 1447
BACON_ODD_PLAYER_OUT                = 1415
BACON_MAX_PLAYER_TECH_LEVEL         = 1494
BACON_HERO_CAN_BE_DRAFTED           = 1491   [R]  héros proposé au draft
BACON_HERO_EARLY_ACCESS             = 1554
BACON_PLAYER_RESULTS_HERO_OVERRIDE  = 1649

── Combat / tours ──────────────────────────────────────────────
BACON_IN_COMBAT_PHASE               = 1522
BACON_CURRENT_COMBAT_PLAYER_ID      = 2989
BACON_COMBAT_PHASE_HERO             = 3048
BACON_WON_LAST_COMBAT               = 1422
BACON_DIED_LAST_COMBAT              = 2483
BACON_DIED_LAST_COMBAT_HINT         = 2780
BACON_COMBAT_DAMAGE_CAP             = 2089

── Cartes / taverne ────────────────────────────────────────────
TECH_LEVEL                          = 1440   tier de la carte
TECH_LEVEL_MANA_GEM                 = 1442
IS_BACON_POOL_MINION                = 1456
IS_BACON_POOL_SPELL                 = 3081
BACON_ACTION_CARD                   = 1437
BACON_SELL_VALUE                    = 1587
BACON_TRIPLE_UPGRADE_MINION_ID      = 1429
BACON_TRIPLED_BASE_MINION_ID        = 1471
BACON_OVERRIDE_BG_COST              = 2437
BACON_REDUCE_BUY_COST               = 2438
BACON_FREE_REFRESH_COUNT            = 4536
BACON_NUMBER_HERO_REFRESH_AVAILABLE = 2452

── Tribus (sous-ensembles, PAS la liste des bannies) ───────────
CARDRACE                            = 200    (enum Race)
BACON_SUBSET_DRAGON 1591 · MURLOC 1592 · DEMON 1593 · BEAST 1594
BACON_SUBSET_MECH 1595 · PIRATE 1596 · NAGA 2272 · TOTEM 2630
BACON_SUBSET_ABERRATION             = 4757
BATTLEGROUNDS_RACES = [MURLOC, DEMON, MECHANICAL, BEAST, DRAGON,
                       PIRATE, ELEMENTAL, QUILBOAR, NAGA, UNDEAD, ABERRATION]
→ AUCUN tag BANNED_RACE / EXCLUDED_RACE : non trouvé

── Bascules de systèmes saisonniers ────────────────────────────
BACON_QUESTS_ACTIVE                 = 2468
BACON_TRINKETS_ACTIVE               = 3740
BACON_BUDDY_ENABLED                 = 2518
BACON_GLOBAL_ANOMALY_DBID           = 2897
BACON_DARK_GIFTS_ACTIVE             = 4845
BACON_ALT_TAVERN_SYSTEM_ACTIVE      = 4519   (Timewarped Taverns)
BACON_GLOBAL_OLD_GOD_DBID           = 4902   (Deity, S14 36.6)

── Quêtes / récompenses ────────────────────────────────────────
BACON_QUEST_COMPLETED 2633 · BACON_QUEST_TOOLTIP 2705
BACON_IS_BOB_QUEST 2732 · BACON_IS_HEROPOWER_QUESTREWARD 2706
BACON_HERO_QUEST_REWARD_DATABASE_ID 2713 · ..._COMPLETED 2715
BACON_HERO_REWARD_CARD_DBID 2748 · BACON_HERO_REWARD_MINION_TYPE 2750
BACON_CARD_DBID_REWARD 2673

── Trinkets ────────────────────────────────────────────────────
BACON_TRINKET                       = 3407
BACON_IS_POTENTIAL_TRINKET          = 3705
BACON_TURNS_LEFT_TO_DISCOVER_TRINKET= 3738
BACON_FIRST_TRINKET_DATABASE_ID     = 3741
BACON_SECOND_TRINKET_DATABASE_ID    = 3742
BACON_HEROPOWER_TRINKET_DATABASE_ID = 3743

── Buddies / Duos ──────────────────────────────────────────────
BACON_BUDDY 2154 · BACON_COMPANION_ID 2130 · BACON_HERO_BUDDY_PROGRESS 2364
BACON_BUY_BUDDY 2937 · BACON_BUY_BUDDY_2 2938
BACON_DUO_TEAM_ID 3095 · BACON_DUO_TEAMMATE_PLAYER_ID 2939
BACON_DUO_PASSABLE 3178 · BACON_DUO_PLAYER_FIGHTS_FIRST_NEXT_COMBAT 2975

── S14 : Dark Gifts / Aberrations / Timewarp ───────────────────
BACON_DEITY_SIGIL 4922 · BACON_OLD_GOD 4744 · BACON_OLD_GOD_ATTACK 4914
BACON_OLD_GOD_HEALTH 4915 · BACON_DARK_GIFT_PRESSABLE_VFX 4891
BACON_TIMEWARPED 4503 · BACON_TIMES_VISITED_ALT_TAVERN 4514
BACON_TURNS_UNTIL_ALT_TAVERN 4529 · BACON_TOTAL_TIMEWARPS_FOR_GAME 4637
BACON_ALT_TAVERN_COIN 4443 · BACON_ALT_TAVERN_IN_PROGRESS 4451

── Tags généraux utiles ────────────────────────────────────────
PLAYSTATE 17 · STEP 19 · TURN 20 · RESOURCES_USED 25 · RESOURCES 26
HERO_ENTITY 27 · PLAYER_ID 30 · DAMAGE 44 · HEALTH 45 · ZONE 49
CONTROLLER 50 · CARDRACE 200 · ZONE_POSITION 263 · NUM_TURNS_IN_PLAY 271
ARMOR 292 · TEMP_RESOURCES 295 · HERO_POWER 380
Step : BEGIN_MULLIGAN 4 · MAIN_READY 6 · MAIN_START_TRIGGERS 17 · FINAL_GAMEOVER 15
BnetGameType : BGT_BATTLEGROUNDS 50 · BGT_BATTLEGROUNDS_DUO 65

── Explicitement NON TROUVÉS ───────────────────────────────────
TECH_LEVEL_MANA_GEM_COST · MINION_TYPE · BANNED_RACE
EXCLUDED_RACE · BACON_EXCLUDED_RACE · TAVERN_UPGRADE_COST
```

---

## URLs consultées

**HSReplay / Tier7** — `hsreplay.net/battlegrounds/tier7/` (403 sur WebFetch, contenu récupéré via l'i18n) · `https://github.com/HearthSim/hsreplaynet-i18n/blob/master/hsreplaynet/frontend/en/frontend.json` · `https://articles.hsreplay.net/2020/04/24/introducing-bobs-buddy/` · `https://help.hearthsim.net/en/collections/2207447-battlegrounds` · `https://help.hearthsim.net/en/collections/2287426-bob-s-buddy` · `https://help.hearthsim.net/en/articles/4106635-how-do-i-get-bob-s-buddy-to-show`

**HearthSim GitHub** — `https://raw.githubusercontent.com/HearthSim/Hearthstone-Deck-Tracker/master/CHANGELOG.md` · `.../BobsBuddy/BobsBuddyInvoker.cs` · `.../Live/BoardStateWatcher.cs` · `https://github.com/HearthSim/Hearthstone-Deck-Tracker/wiki/FAQ` · issues `#4039`, `#4450`, `#4570` · `https://raw.githubusercontent.com/HearthSim/HSTracker/master/CHANGELOG.md` · `https://github.com/HearthSim/HSTracker/issues/1423` · `https://github.com/HearthSim/HDT-Localization` (pas de chaînes BG) · `https://raw.githubusercontent.com/HearthSim/python-hearthstone/master/hearthstone/enums.py` · `https://github.com/HearthSim/HearthDb` · `https://github.com/HearthSim/hsreplay-test-data/tree/master/hslog-tests` · `.../hsreplaynet-tests/replays/battlegrounds_combat.annotated.xml` · `.../battlegrounds_no_combat_minions_both_players.annotated.xml` · `https://hearthsim.info/docs/gamestate-protocol/`

**Blizzard** — `https://hearthstone.blizzard.com/en-us/news/24290432` (36.2 / S14) · `https://news.blizzard.com/en-us/article/24290433/announcing-battlegrounds-season-14-dark-gifts-of-dalaran` · `https://hearthstone.blizzard.com/en-us/news/24294373/366-patch-notes` · `https://news.blizzard.com/en-us/article/24276665/35-6-patch-notes`

**Tiers** — `https://github.com/BattlegroundsHelp/bgtracker` · `https://github.com/PeytonNowlin/Tavern-Lens` · `https://blizzardwatch.com/2026/07/28/hearthstone-battlegrounds-season-14/` · `https://game8.co/articles/latest/hearthstone-battlegrounds-new-season-breaks-the-rules-with-dark-gifts-and-new-heroes` · `https://github.com/lowerman/MMRadar_HDT_BG`

**Inaccessibles** — `hsreplay.net/*` (403) · `hearthstone.fandom.com/wiki/GameTag_enumeration` (402) · `hslog-tests/36393_battlegrounds.power.log` (>10 Mo)

---

Deux conséquences directes pour `bg_treehudder`, dont l'architecture « le log est la seule source de vérité » est **confirmée viable** : les tribus bannies et le nom/MMR des adversaires sont les deux seules choses que HDT obtient par la mémoire et que ton approche ne pourra pas rendre à l'identique ; tout le reste de l'overlay gratuit de HDT est dérivable de Power.log.

```
┌─ À FAIRE ─────────────────────────────────────────────────────────────┐
│ 1. Mesurer sur un Power.log réel si PLAYER_TRIPLES (1447) est écrit   │
│    pour les ADVERSAIRES ou seulement pour soi — deux sources se       │
│    contredisent, la mesure tranche en une passe de grep.              │
│ 2. Vérifier la présence d'ARMOR (292) sur les héros BG modernes :     │
│    absent du replay de 2020, utilisé par BobsBuddyInvoker aujourd'hui.│
│ 3. Confirmer quêtes / buddies / duos en saison 14 — non trouvé dans   │
│    les notes 36.2 et 36.6 ; se coder contre BACON_QUESTS_ACTIVE       │
│    (2468) et BACON_BUDDY_ENABLED (2518) plutôt que contre la saison.  │
│ 4. Décider si les tribus bannies restent hors périmètre (le log ne    │
│    les porte pas) ou s'inférent depuis les sbires vus en boutique.    │
└───────────────────────────────────────────────────────────────────────┘
```
