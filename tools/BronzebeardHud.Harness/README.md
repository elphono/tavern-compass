# Simulation du plugin (hôte Windows)

Les panneaux du plugin dans une fenêtre Windows ordinaire, avec des données synthétiques, pour les déplacer, les
redimensionner et les regarder **sans lancer une partie** ni HDT.

```bash
./tools/BronzebeardHud.Harness/launch.sh                 # la fenêtre, pour déboguer à la main
./tools/BronzebeardHud.Harness/launch.sh --selftest      # vérifie la scène sans personne au clavier (code de sortie 0 = tout passe)
./tools/BronzebeardHud.Harness/launch.sh --screenshot    # écrit C:\temp\BronzebeardHarness\out\shot.png (le canvas à sa taille réelle)
```

Arguments en plus : `--size 1600x900`, `--layout <fichier>`, `--wait <ms>` (attente des noms et images avant la capture).
Le script compile sous WSL, copie dans `C:\temp\BronzebeardHarness` et lance l'exécutable côté Windows.

## Ce qui est réel, ce qui est simulé

| Réel : les sources du plugin, compilées telles quelles | Simulé : `HdtShim.cs` |
|---|---|
| `PanelMover` (déplacer, poignée, cadre), `TavernAdvicePanel`, `LineupsPanel`, `CompGuidesPanel`, `SkipCombatPanel`, `CardImages`, `PreviewPlacer`, `OverlayLayer` | `OverlayExtensions` (sans effet : la fenêtre normale reçoit la souris ; l'infobulle devient une infobulle WPF), `Log` (le volet de droite), `Database` (noms et paliers de HearthstoneJSON), les téléchargeurs d'images (art.hearthstonejson.com, cache dans `%TEMP%\BronzebeardHarness`) |

**Pas simulé, et un défaut qui y vivrait ne se reproduit pas ici :** la couche d'HDT autour des panneaux, c'est-à-dire
la fenêtre d'overlay transparente aux clics au-dessus du jeu et le survol sondé à 60 Hz. Ce que l'hôte reproduit : la
logique des panneaux, leurs événements WPF, leur mise en page et leur remplissage.

## Ce qui est à l'écran

- Le canvas à la taille d'une fenêtre Hearthstone (liste déroulante : 1920 × 1080, 1600 × 900, 4:3, 21:9, 2291 × 1360).
- Les quatre panneaux déplaçables, mode déplacement activé au départ, et les zones à ne pas masquer en rouge dessous
  (définies par `NoGoZones` des tests : une seule définition).
- Un volet de journal : les lignes du plugin (`resize start`, `resize end`, `panel moved`…).
- La disposition est enregistrée dans `%TEMP%\BronzebeardHarness\layout.json`, **jamais** dans le `layout.json` du plugin.
- Les données sont inventées (compositions, guides) sur de vrais identifiants de cartes ; rien ne vient de Firestone
  ni de HSReplay. Les noms et images viennent de HearthstoneJSON, téléchargés dans le cache temporaire.
