# Simulation du plugin (hôte Windows)

Les panneaux du plugin dans une fenêtre Windows ordinaire, avec des données synthétiques, pour les déplacer, les
redimensionner et les regarder **sans lancer une partie** ni HDT.

```bash
./tools/BronzebeardHud.Harness/launch.sh                 # la fenêtre, pour déboguer à la main
./tools/BronzebeardHud.Harness/launch.sh --selftest      # vérifie la scène sans personne au clavier (code de sortie 0 = tout passe)
./tools/BronzebeardHud.Harness/launch.sh --screenshot    # écrit C:\temp\BronzebeardHarness-ci\out\shot.png (le canvas à sa taille réelle)
./tools/BronzebeardHud.Harness/launch.sh --screenshot --detail 2   # la même, détail de la 2e cible ouvert (rang, ou nom d'un guide)
```

Arguments en plus : `--size 1600x900`, `--layout <fichier>`, `--wait <ms>` (attente des noms et images avant la capture),
`--scenario 0|1|2` (plateau tenu : rien, deux cartes d'une compo, un plateau fort ; 2 par défaut), `--detail <rang ou nom>`
(ouvre le détail avant la capture ; un rang qui n'existe pas fait échouer la capture plutôt que de capturer la liste).
Le script compile sous WSL, copie dans `C:\temp\BronzebeardHarness` et lance l'exécutable côté Windows (par `Start-Process` : lancée par `cmd.exe /c start`, la fenêtre garde la console de WSL attachée et le script ne rend jamais la main). **Fermer la fenêtre avant de la relancer** : un exécutable en cours ne se remplace pas, et le script le dit.
`--selftest` et `--screenshot` tournent depuis leur propre copie, `C:\temp\BronzebeardHarness-ci` (sortie dans son `out\`) : ils passent même fenêtre ouverte, sans toucher à son dossier.
Ils lisent aussi leur propre disposition, `C:\temp\BronzebeardHarness-ci\layout.json` (jamais écrite : la disposition par défaut), sauf `--layout` explicite : la capture ne dépend pas de la fenêtre. Le journal et le cache d'images restent partagés.

## Ce qui est réel, ce qui est simulé

| Réel : les sources du plugin, compilées telles quelles | Simulé : `HdtShim.cs` |
|---|---|
| `PanelMover` (déplacer, poignée, cadre), `CompsPanel` (le panneau « Compositions »), `TavernMarkers` (cadres et étiquettes sur les cartes de Bob, boutons ◇), `SkipCombatPanel`, `CardImages`, `PreviewPlacer`, `OverlayLayer` | `OverlayExtensions` (sans effet : la fenêtre normale reçoit la souris ; l'infobulle devient une infobulle WPF), `Log` (le volet de droite), `Database` (noms et paliers de HearthstoneJSON), les téléchargeurs d'images (art.hearthstonejson.com, cache dans `%TEMP%\BronzebeardHarness`) |

La logique de `Plugin.cs` (lecture d'HDT, `CompTargetTracker`, `TavernHighlights`) est rejouée par `HarnessWindow` avec les
mêmes appels de `BronzebeardHud.Stats` : cibles et couleurs sont celles que le plugin calculerait sur ces cartes.

**Pas simulé, et un défaut qui y vivrait ne se reproduit pas ici :** la couche d'HDT autour des panneaux, c'est-à-dire
la fenêtre d'overlay transparente aux clics au-dessus du jeu et le survol sondé à 60 Hz ; la vraie rangée de Bob (ses
sept cartes sont des boîtes grises placées où `TavernLayout.CardSlots` met les cartes du jeu). Ce que l'hôte reproduit :
la logique des panneaux, leurs événements WPF, leur mise en page et leur remplissage.

## Ce qui est à l'écran

- Le canvas à la taille d'une fenêtre Hearthstone (liste déroulante : 1920 × 1080, 1600 × 900, 4:3, 21:9, 2291 × 1360).
- Les deux panneaux déplaçables (Compositions, Skip combat), mode déplacement activé au départ, et les zones à ne pas
  masquer en rouge dessous (définies par `NoGoZones` des tests : une seule définition).
- La rangée de Bob : deux cartes clés d'une cible (cadre plein), un add-on et un enabler (pointillés), une carte épinglée
  (◆, blanc) et deux qui ne servent à rien.
- Trois plateaux (liste déroulante) : 0, 1 et 3 cibles ; le bouton « Detail of target 1 / list » ouvre et ferme le détail.
- Un volet de journal : les lignes du plugin (`resize start`, `resize end`, `panel moved`, `ticked guides`…).
- La disposition est enregistrée dans `%TEMP%\BronzebeardHarness\layout.json`, **jamais** dans le `layout.json` du plugin.
- Les données sont inventées (quinze guides, leurs textes et leurs listes de cartes) sur de vrais identifiants de cartes ;
  rien ne vient de Firestone ni de HSReplay. Les noms et images viennent de HearthstoneJSON, téléchargés dans le cache temporaire.

## Ce que vérifie `--selftest`

Deux panneaux visibles et dans l'overlay ; une poignée et un cadre (seul le panneau des compositions se redimensionne) ;
le mode déplacement ; 0, 1 et 3 cibles selon le plateau, de couleurs distinctes ; les cadres sur les cartes de Bob, pleins
et pointillés, tels que `TavernHighlights` les demande ; un clic sur le nom d'une cible ouvre son détail, « ← All comp
guides » rend la liste ; les sections du détail dans l'ordre de HDT et « k of n sections » quand il en manque ; **aucun
texte du panneau sous 12 px × échelle, aucun texte coupé** (liste et détail : l'encre de chaque texte, pas sa boîte, doit
tenir dans chaque découpe de ses parents) ; aucune ligne Warning/Error ; le fichier de disposition est celui de la simulation.
Le contrôle des textes a été éprouvé par mutation le 2026-10-04 : un nom sans retour à la ligne et un texte à 11 px le font
échouer.
