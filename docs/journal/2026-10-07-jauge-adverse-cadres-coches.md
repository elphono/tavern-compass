<!-- visuel : dérogation — journal de séance court, au format Markdown des autres journaux du dépôt -->
# Journal — 2026-10-07 : jauge de l'adversaire, cadres des compos cochées

Deux correctifs demandés par Ali après une partie réelle. Faits sous WSL (tests, mutations, build contre HDT 1.58.9) ;
**rien n'est encore vu en jeu**.

## 1. La rangée de l'adversaire de l'encart de puissance était toujours vide

- **Constat** : dans le journal d'HDT, 89 lignes `opponent power …`, toutes `seen=none`, alors qu'HDT écrit 30 fois
  `Snapshotting board state for … with player id N (k entities` sur les mêmes parties, **avant** notre lecture (ex. 18:48:02 :
  snapshot du joueur 3, 7 entités, puis notre ligne `id=3 … seen=none`).
- **Cause** (HDT 1.58.9 décompilé, `BattlegroundsBoardState`) : le plateau est rangé sous le `PLAYER_ID` et ne contient que
  les sbires (`x.IsMinion && x.IsInZone(Zone.PLAY) && x.IsControlledBy(_game.Opponent.Id)`) ; `GetSnapshot(entityId)` le
  retrouve par le `PLAYER_ID` de l'entité donnée. Le plugin cherchait le héros **dans** le plateau : jamais trouvé, aucun
  plateau gardé. Écartées par le code et le journal : la mauvaise entité (toute entité de ce `PLAYER_ID` rend le même
  plateau), le mauvais moment (le snapshot précède la lecture), une exception (le garde-fou `opponent-power` n'a jamais
  sauté).
- **Décision** : le héros de référence vient des entités, celui du classement (`OpponentBoards.Pick` : contre un fantôme,
  le héros en jeu est Kel'Thuzad avec le `PLAYER_ID` du mort) ; la ligne de journal dit en plus `read=[heroes … asked … →
  turn t, n entities, m minions | no snapshot]`.
- **Fin de combat** : plus aucun héros en jeu contrôlé par l'adversaire n'est trouvé alors que la phase est encore
  « combat » (28 lignes `scope=combat id=0` sur 28 combats, chacune après une ligne du même combat avec un id, au même
  tour, et 2 à 5 s avant la ligne `scope=next` : 4 × 2 s, 17 × 3 s, 6 × 4 s, 1 × 5 s) ; la rangée passait « Opp. – not
  known yet ». **Arbitrage d'Ali** : garder l'adversaire du combat et sa jauge jusqu'au retour en taverne
  (`CombatOpponentKeeper` : les faits du même combat gardés tels quels, oubliés en taverne, à un autre tour ou quand un
  autre adversaire est trouvé ; journal `id=3 (kept)`, une seule ligne tant que rien ne change).

## 2. Compos cochées : plus aucun cadre pour les autres compos en taverne

- **Constat** (Ali) : « quand on sélectionne des compos vers lesquelles on veut tendre, on ne devrait plus surligner aucun
  autre sbire dans le shop ». Une cible « in progress » gardée à côté d'une cochée encadrait encore ses cartes.
- **Cause** : `TavernHighlights.For` encadrait pour toutes les cibles, cochées ou non (décision du 2026-10-06, faite pour
  la liste du panneau).
- **Décision** : dès qu'un guide est coché, seuls les guides cochés encadrent (`TavernHighlights.Framing`) : rôles,
  plateaux de la compo pontée et noms sous l'étiquette. La liste du panneau, les étiquettes des choix et les ◇ ne changent
  pas ; sans case cochée, rien ne change. Le journal dit `frames from ticked=[…]`.
