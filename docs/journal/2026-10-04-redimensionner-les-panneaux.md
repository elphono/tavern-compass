# Journal — 2026-10-04 : redimensionner les panneaux en mode déplacement

Demande d'Ali : « il faut rajouter quelque chose pour pouvoir redimensionner nos panneaux quand on est en mode
Move panel ». Trois lectures de « redimensionner » étaient possibles ; Ali a tranché la troisième :

| Lecture | Décision |
|---|---|
| zoom proportionnel (1× à 2×, ou 0,6× à 2×) | écarté |
| **largeur et hauteur libres, le contenu ne grandit pas : il a plus ou moins de place** | **retenue** (« s'il n'y a pas assez de place pour 3 lignes, on n'en affiche que 2 ») |

## Ce qui a été posé

| Panneau | La hauteur règle | La largeur règle | Minimum |
|---|---|---|---|
| `target-compositions` | lignes affichées, pivots du détail | rien (sa largeur par défaut est son minimum : sept ovales) | titre + 1 ligne |
| `lineups` | compositions affichées | ovales par ligne | sept ovales par ligne, un plateau complet |
| `skip-combat` | — | — | pas de poignée |

`layout.json` garde le schéma 1 : `width` et `height` facultatifs, ensemble, en fractions de l'overlay. Les calculs
vivent dans `PanelLayout` (`StoreRect`, `Resize`, `Resolve` avec minimum) et `PanelFit` (paramètre `bottom` de
`Rows` et `DetailPivots`), testés ; la poignée et le cadre dans `PanelMover`, non testables sous WSL.

## Ce que la mesure a trouvé en route

- Une boîte exactement de la hauteur de n lignes en montrait n − 1 à 72 % des hauteurs de fenêtre (arrondi de la
  division par l'échelle). `PanelFit.Tolerance`, fixée par un balayage de 600 à 2200 px.
- Éprouvé par mutation : 30 mutations, 4 non tuées à la première passe — deux variantes (largeur, hauteur) d'un même
  trou, un `Resize` sans minimum masqué par `Resolve` (une largeur négative aurait rendu le panneau à sa taille par
  défaut) ; une constante testée par elle-même (`DetailMinHeight`) ; une mutation mal écrite qui ne compilait pas.
  Les deux trous ont leur test et la mutation mal écrite a été refaite : 0 survivant à la seconde passe.

## Non vérifié (à voir sous Windows)

Pointage de la poignée sous HDT, cadre pointillé, retour au défaut par « Reset », et rendu d'un panneau lineups
étroit (l'en-tête est estimé à deux lignes : un titre ou un bandeau long en prendrait trois et la dernière ligne serait coupée).
