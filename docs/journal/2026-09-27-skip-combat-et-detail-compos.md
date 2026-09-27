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
