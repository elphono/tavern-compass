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
