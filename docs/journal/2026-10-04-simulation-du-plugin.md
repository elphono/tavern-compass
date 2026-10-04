# Journal — 2026-10-04 : simulation du plugin

Demande d'Ali : « lance une simulation du plugin pour que je puisse débug sans lancer une partie ».
Le plugin ne se vérifiait que sous HDT avec une partie en cours ; la poignée de redimensionnement, écrite le matin
même, n'avait jamais été vue tourner.

| Approche | Décision |
|---|---|
| **A. Les vrais `.cs` compilés dans un hôte, avec une cale pour les 4 points d'accroche à HDT** | **retenue** |
| B. Charger les vraies assemblies d'HDT | écartée : l'initialisation d'HDT (`Core`, `Config`, `Log`) plante probablement hors HDT, et casse à chaque version |
| C. Le vrai plugin dans HDT avec une fausse partie | impossible : l'overlay n'existe que si le jeu tourne |

Les quatre points d'accroche (`OverlayExtensions`, `Log`, `Database.GetCardFromId`, les téléchargeurs d'images)
étaient la question de faisabilité ; la réponse est qu'**une cale d'environ 150 lignes suffit** : `PanelMover`, les
quatre panneaux, `CardImages`, `PreviewPlacer` et `OverlayLayer` compilent sans une ligne changée.

Vérification : `--selftest` était rouge devant une fenêtre vide (0 panneau, 0 poignée, pas de fichier de disposition),
vert devant la scène ; `--screenshot` rend le canvas avec les vraies images de cartes. Mode « scénario » (souris
injectée pour rejouer un geste) : **reporté**, à faire si le besoin se confirme.

Limite : la couche d'HDT autour des panneaux (clics transparents, survol à 60 Hz) n'est pas simulée.
