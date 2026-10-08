<!-- visuel : dérogation — journal de séance court, au format Markdown des autres journaux du dépôt -->
# Journal — 2026-10-08 : stats multi-sources, card-stats, organisation en issues

Séance sur la machine de travail : tests, mutations, simulation et builds faits ; **HDT n'y est pas installé, rien n'est
déployé ni vu en jeu**. La suite se reprend sur la machine de jeu, depuis les issues GitHub (#1 à #14).

## 1. La note de conception, annotée par Ali

`docs/plans/2026-10-08-stats-multi-sources.html`, révisée sur ses 12 annotations : accord de nomi.gg obtenu (tel quel, sur
les termes de la demande), card-stats autorisé, architecture simplifiée (une classe par source, une fonction pure de
consolidation, ni registre ni adaptateur par genre : « séparation des responsabilités avant tout, pas d'over-engineering »),
schémas d'architecture et de séquence au § 9, tranche du joueur et non celle d'Ali, collecte de nos propres données plus tard.
Décisions rendues au § 14.

## 2. L'anti-danse du board (Nomi's Kitchen)

Ali garde l'idée et voulait comprendre la méthode. Lu : le plugin HDT de Nomi copie une DLL BepInEx dans le dossier du jeu ;
elle pose des correctifs Harmony sur `ZoneMgr` (réconciliation des déplacements prédits par le client avec ceux du
serveur). Le corps est obfusqué : on s'est arrêté là. Ali n'était pas d'accord qu'il s'agisse d'un mod du client
(« nomi.gg dit que son plugin le fait ») ; le code montre que le plugin le fait **en installant** ce mod. Suite : issue #1.

## 3. Chantier a — card-stats (livré, simulation seulement)

Plan : `docs/plans/2026-10-08-chantier-a-card-stats-plan.md`. Format commun (`StatProvenance`), `CardStatsFile` par tranche,
`CardTurnValue`, valeur sur les cartes de Bob et à la place du « — » d'un choix, tranche changeable à chaud (bouton de la
barre de titre), `RepositoryHygieneTests` qui refuse les fichiers de stats réels (chantier e).

- **Mesure qui a changé la spec** : comparer une carte jouée aux parties où elle ne l'est pas donnait ▲ à 320 cartes sur 320
  au tour 6. Comparée à la moyenne des cartes jouées au même tour : écart de −0,29 à +0,30 (p10–p90), erreur type médiane
  0,04. Fichiers réels gardés hors du dépôt.
- **Supposé, pas vérifié** : que le tour de `GetTurnNumber()` d'HDT soit celui de Firestone (issue #2).
- Vérifié : 652 tests, mutations (seuils, verdict, ordre des raisons, isolement des tranches) toutes détectées, self-test de
  la simulation, captures (`--card-values`).

## 4. Organisation

Le travail ouvert passe dans les issues GitHub, en trois milestones (Vérifier en jeu, Stats multi-sources, Idées à
explorer) et deux labels (`vérif en jeu`, `arbitrage`). `CLAUDE.md` n'en garde qu'un renvoi.
