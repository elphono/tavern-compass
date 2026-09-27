# Journal — 2026-09-27 (3) : relance de Skip combat, ergonomie

## Relance de Skip combat : toujours par Battle.net

Bug d'Ali : « il kill, il relance, mais la fenêtre relancée disparaît ». Essais sur sa machine, jeu au menu
(journal d'HDT), avec une sonde Windows qui relance par `Process.Start` comme le plugin :

| Heure | Relance | Résultat | Mesure |
|---|---|---|---|
| 09:47:57 | directe, `Hearthstone.exe -launch -uid hs_beta` | client en 12 ms, écran « Fermé » | Login.log : « A repeated token was retrieved when disallowed » |
| 09:56:10 | Battle.net `--exec="launch WTCG"`, 112 ms après le kill | rien pendant 50 s | aucun processus Hearthstone |
| 09:57:45 | même commande, Battle.net au repos | client en 582 ms, connecté | « We are now logged in » 09:58:01 |
| 10:02:04 | Battle.net redemandé toutes les 2 s | ignoré à 0,1 / 2,2 / 4,2 s, client à 6,7 s, connecté | « We are now logged in » 10:02:25 |

Une relance directe réutilise le jeton de connexion du client tué : éliminée. Le plugin demande à Battle.net
(le parent du client, sinon un Battle.net en cours), puis redemande chaque seconde, 12 fois au plus ; sans
Battle.net, rien n'est tué. Le journal d'HDT dit les demandes, le nouveau pid et s'il vit 3 s après.
