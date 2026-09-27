# Journal — 2026-09-27 : vignettes en couleur, détail des compos, bouton Skip combat

Deux demandes d'Ali, livrées sous WSL (tests et build) ; rien n'est encore vu en jeu.

## A. Panneau « Target compositions »

| Quoi | Décision | Motif |
|---|---|---|
| vignettes | toutes en couleur, opacité 1 ; tenue = cadre vert + ✓, manquante = cadre rouge vif | Ali ne veut plus de gris ; le ✓ distingue aussi sans la couleur |
| cible / suggestion | `IsTarget` retiré (toujours vrai depuis `fb61851`) ; cochée = gras dans sa couleur, sinon gris clair « · suggestion » | les suggestions s'affichaient comme des cibles |
| détail d'une compo | bouton ▸ / ▾ à gauche du nom, un seul ouvert à la fois ; la case garde son rôle | geste distinct, petite zone cliquable au-dessus du jeu |

Le JSON Firestone (schema 4) et le format HSReplay manuel n'ont **aucune** donnée early game, enablers ou
« when to commit » : `CompDetail` les **dérive**, et le bloc le dit en toutes lettres.

- Early enablers : cartes des add-ons et des plateaux finaux de tier ≤ 3, les plus présentes sur les plateaux
  d'abord, six au plus. Une pièce clé de tier ≤ 3 y figure aussi (lecture littérale de la demande).
- When to commit : les pièces clés, avec leur tier. Typical final turn : médiane des `turn` des plateaux.
- Tier : HearthDb via HDT (`Card.TechLevel`, 0 = inconnu, écarté des enablers).

## B. Bouton « Skip combat »

En combat seulement, un bouton jaune tue Hearthstone et le relance ; la reconnexion saute l'animation.
Panneau déplaçable à part (`skip-combat`), par défaut au bout droit de la rangée du joueur, à droite de sept
sbires. Exécutable retenu **avant** le kill (`MainModule`, sinon `HearthstoneDirectory` d'HDT) ; rien n'est
tué s'il n'existe pas. Un clic par combat. Une ligne `Bronzebeard HUD: skip combat …` cite pid, exécutable,
temps de sortie, nouveau pid ou l'exception.

| Mesuré le 2026-09-27 | Valeur |
|---|---|
| `config.xml` d'HDT | `CloseWithHearthstone=false`, `HearthstoneDirectory=E:\JEUX\Hearthstone` |
| bitness (`file`) | Hearthstone.exe et HDT 1.58.3 : x86-64 tous deux, donc pas de refus 32/64 bits attendu sur `MainModule` |
| HDT à la mort du client (lu dans `Core.cs`, master) | `Reset()` puis `IsInMenu = true` : les panneaux du plugin devraient se vider jusqu'à la reconnexion |
| HDT lançant lui-même le jeu (`HearthstoneRunner.cs`) | via Battle.net `--exec="launch WTCG"`, pas Hearthstone.exe |
