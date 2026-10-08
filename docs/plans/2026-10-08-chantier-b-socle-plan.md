# Chantier b — le socle des sources : plan

Issue #9. Spec : `docs/plans/2026-10-08-stats-multi-sources.html`, § 6, § 9 et § 12. Consigne d'Ali : modulaire, une
responsabilité par pièce, pas d'over-engineering — ni registre, ni adaptateur par genre de stat.

**Critère de sortie** (§ 12) : mêmes entrées → mêmes aides qu'avant (tests existants verts) ; deux sources synthétiques
en désaccord → « contested » ; une source qui lève n'éteint pas l'autre ; mutations de la règle de recouvrement et de la
décote détectées ; la CLI sort le tableau des désaccords d'un instantané réel, hors dépôt.

## Un écart assumé avec l'esquisse du § 9

L'esquisse donnait à chaque source un `LoadAsync(cache, bracket)`. Le chargement existe déjà et marche, par tranche et
avec ses règles d'âge (`StatsService`, `CompService`, `CardStatsRefresh`, `ChoiceAdvicePanel`) : le refaire dans des
sources serait réécrire ce qui est vu en jeu, sans source réelle qui en ait besoin. **Une source ne fait donc que
traduire** des fichiers déjà chargés en chiffres avec leur provenance ; les services restent ceux qui chargent. Si
nomi.gg (chantier c) demande autre chose, on le verra sur pièce.

## Étapes

| # | Étape | Fichiers | Vérification |
|---|---|---|---|
| 1 | **`source` en liste ouverte** : un fichier d'une source inconnue se charge ; une étiquette courte pour les connues (`FS`, `HSR`), le nom sinon | `HeroStatsLoader`, `CompositionLoader`, `StatsSources`, `HeroPickPanel` | test : `source: "nomi.gg"` se charge ; l'étiquette d'une inconnue est son nom |
| 2 | **La provenance sur chaque fichier** : `HeroStatsFile`, `CompositionFile`, `TrinketStatsFile` exposent `Provenance` (`StatProvenance`) | ces trois fichiers | test : un fichier relu rend la même provenance |
| 3 | **Les chiffres au format commun** : `StatRecord` (genre, sujet, mesure, valeur, effectif, unité) et `SourceSnapshot` (provenance, chiffres) ; `FirestoneSource` (héros, trinkets, cartes) et `HsReplayManualSource` (héros) | nouveaux | tests de traduction sur données synthétiques |
| 4 | **`StatsConsolidation.Consolidate`**, fonction pure (§ 6.2) : aligner (même genre, sujet, mesure, unité ; patch le plus récent quand il est connu), décote d = 0,5 hors tranche du joueur, rappel k = 30 vers μ0, verdict `Single` / `Consensus` / `Contested` / `Apart` (sous 10 parties), contributions | nouveau | tableaux d'entrées → sorties ; mutations : recouvrement, décote, seuil de 10 |
| 5 | **Un garde-fou par source** : `data-firestone` et `data-manual` à la place de `data-refresh` ; une source qui lève est coupée seule | `Plugin`, `StatsService`, `CompService` | lecture et build (le plugin n'a pas de tests unitaires) ; en jeu, ligne du garde-fou |
| 6 | **Un seul fetcher et un seul cache**, créés par le plugin et passés aux services | `Plugin`, services | build ; même journal `data …` qu'avant |
| 7 | **La vue consolidée** calculée quand une source change, et une ligne de journal (`stats view: …`) ; aucune aide ne la lit encore (composant 3, chantier d) | `Plugin` | même affichage qu'avant (simulation, tests) |
| 8 | **CLI d'inspection** `tools/BronzebeardHud.Inspect` : lit le cache local sans réseau, construit les instantanés, sort les désaccords et l'effectif par tranche ; refuse d'écrire dans le dépôt | nouveau projet | lancée sur le cache d'Ali ; sortie hors dépôt |
| 9 | Documentation : `CLAUDE.md`, issue #9 | — | — |

## Ce que la CLI peut mesurer aujourd'hui, franchement

Le dossier `manual\` d'Ali est vide : il n'existe **aucun** désaccord entre deux sources à mesurer. Ce qui se mesure :
l'écart entre les tranches de Firestone (top 25 % contre tous les joueurs), qui éclaire la décote d, et l'effectif par
héros et par tranche, qui éclaire le seuil de 10 parties. Les seuils restent des points de départ jusqu'à nomi.gg.
