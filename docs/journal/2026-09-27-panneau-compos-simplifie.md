# Journal — 2026-09-27 (2) : panneau des compos simplifié, lineups à part

Seconde ronde du même jour, sur remarques d'Ali (faites sur une maquette de l'ancien panneau). Rien de
tout cela n'est encore vu en jeu.

| Avant (matin) | Maintenant |
|---|---|
| en-tête détaillé, bouton ▸/▾, pivots et lineups sous chaque compo | une ligne par compo : case, nom + place moyenne, sept ovales |
| vignettes carrées arrondies | ovales découpés comme les portraits du jeu (largeur × 1,25), anneau vert + ✓ / rouge |
| détail déplié sous la ligne | clic sur le nom ou un ovale : détail **à la place** de la liste, « ← back » |
| clic sur une vignette : « comment les tops le jouent » | clic sur une vignette : détail de la compo, rien d'autre |
| lineups dans le panneau des compos | panneau à part, déplaçable (`lineups`), ouvert par le ? au-dessus d'un sbire de Bob |

Le détail garde enablers, pièces clés, tour final médian et la mention « derived from », et gagne les
pivots (`CompTransitions`) et l'état du joueur (cochée ou suggestion, pièces clés tenues, effet du héros).
Arbitrage d'Ali : une pièce clé de tier ≤ 3 reste dans les enablers, test `…_ByDesign`.

**Position Tier7 du bouton ? : non déterminable.** HDT 509bb0b n'a pas de bouton Tier7 au-dessus des
cartes de Bob : par carte, seulement l'icône clé sur la carte (`BattlegroundsMinionPinningCard.xaml:73-86`,
Top 99 / Right 0) et un encart au survol 90 unités au-dessus (l.87-100) ; le bouton Inspiration est
en haut à droite de la fenêtre (`OverlayWindow.xaml:488-492`). Le ? reste à droite du ◇.

**Skip combat instantané** (demande d'Ali) : plus d'attente `WaitForExit` ; dès que `Kill()` revient, relance.
`Kill()` ne fait que demander la fin du processus : l'ancien client peut ne pas être sorti au moment du
`Start()`. La ligne de journal le mesure (`old process exited by then: True/False`) ; si Hearthstone refuse
une seconde instance, c'est là que ça se verra.
