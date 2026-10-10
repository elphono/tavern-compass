<!-- visuel : dérogation — note de journal, pas un document de référence -->
# Journal — 2026-10-10 : la couche d'overlay d'HDT simulée (issue #14)

Fait sous WSL (build, autotest de la simulation lancé côté Windows, mutations). **Rien n'est vu en jeu**, et aucun
fichier de `src/` n'a changé : seuls la simulation et la documentation.

## Ce que fait HDT, et d'où on le sait

HDT 1.58.9 installé (`app-1.58.9`) : `HearthstoneDeckTracker.exe` décompilé type par type avec `ilspycmd` 8.2, pris dans
le cache NuGet local (rien téléchargé ; sorties hors du dépôt). Les huit méthodes en cause sont identiques, caractère
pour caractère, dans `app-1.58.10`.

| Lu dans le code d'HDT | Où (`OverlayWindow`, sauf mention) |
|---|---|
| fenêtre créée `WS_EX_NOACTIVATE \| WS_EX_TRANSPARENT` : transparente aux clics | `Window_SourceInitialized_1` |
| boucle lancée par le constructeur : `UpdateHoverable()`, puis `await Task.Delay(16)` | `StartInteractivityUpdates` |
| la fenêtre prend la souris tant que le curseur est sur un élément déclaré `IsOverlayHitTestVisible`, redevient transparente ailleurs ; le style ne bascule qu'au changement | `UpdateHoverable`, `SetClickthrough` |
| `MouseEnter` et `MouseLeave` de sonde (`CustomMouseEventArgs`) sur les éléments `IsOverlayHoverVisible` dont le rectangle contient le curseur, groupés par enfant du canvas ; le dernier enfant l'emporte | `UpdateHoverable` |
| « contient » : visible, chargé, une taille, un parent `FrameworkElement` ; origine par `TransformToAncestor`, taille × échelles ; bornes strictes ; origines gardées 200 ms, échelles 1 s | `ElementContains`, `Helper.GetTotalScaleTransform` |
| l'infobulle d'un élément survolable n'écoute que les événements de la sonde | `OverlayExtensions.ShowTooltip`, `HideTooltip` |
| inscription au changement de la propriété, puis au `Loaded` de l'élément ; retrait à son `Unloaded` ; une `List` pour les cliquables, un `HashSet` pour les survolables | `OverlayExtensions`, constructeur |

**Déduit, pas lu dans HDT** : ce que fait WPF là où la fenêtre prend la souris. Il reçoit les déplacements et lève ses
propres `MouseEnter` et `MouseLeave` (une deuxième entrée sur une ligne quand le curseur arrive sur son nom, déclaré
cliquable). Quand la sonde rend la fenêtre transparente, le déplacement suivant part au jeu : WPF perd la souris et lève un
`MouseLeave` sur tout ce qu'il tenait, la ligne comprise, alors que le curseur y est encore. C'est la déduction du
2026-10-04 (`GuideHover`) ; elle est maintenant produite par un mécanisme au lieu d'être levée à la main.

## Ce que la simulation reproduit

`tools/BronzebeardHud.Harness/HdtOverlay.cs` porte ces méthodes sur un curseur injecté ; `HdtShim.cs` inscrit les
éléments comme HDT. `--mouse 'line:1;wait:300'` pilote une capture par cette couche (README de la simulation). L'autotest
gagne onze contrôles « mouse: » : la sonde tourne ; un clic hors mode déplacement sur le titre du panneau part au jeu, le
même en mode déplacement est au panneau ; un ◇ prend son clic, la carte de Bob dessous non ; popup après 250 ms par la
sonde ; deuxième entrée de WPF sans effet ; sortie de WPF curseur dans la ligne sans effet ; aperçu de carte par la sonde ;
panneau redessiné sous un curseur immobile sans effet ; sortie réelle qui cache. Chaque contrôle exige aussi que
l'événement en cause ait été levé.

Mutations : retirer le filtre de la sortie parasite (`GuideHover.Leave`) fait tomber 5 contrôles, dont 4 « mouse: » ;
déclarer le panneau cliquable hors du mode déplacement (`PanelMover`) fait tomber le clic hors mode déplacement — ce mutant
passait l'autotest d'avant sans un seul rouge. Les deux restaurés, l'autotest repasse au vert (89 contrôles).

## Trouvé en route

1. **Une entrée morte par élément cliquable déclaré avant son `Loaded`.** HDT l'inscrit au réglage, puis à son `Loaded`,
   dans une `List`, et ne l'en retire qu'une fois à son `Unloaded`. Le plugin déclare ses cases, noms, ovales et boutons
   avant de les ajouter à l'arbre, et reconstruit son panneau à chaque redessin. Dans la simulation, la liste passe de
   4 108 à 4 146 entrées en un redessin, pour 29 éléments encore chargés. Établi par le code d'HDT et reproduit ; l'effet
   en jeu (mémoire gardée, liste parcourue toutes les 16 ms) **n'est pas mesuré**. Un remède côté plugin (déclarer après
   l'ajout à l'arbre, ou réutiliser les éléments) est à arbitrer : rien n'est changé.
2. **Le mode déplacement allumé avant le chargement de l'overlay** laisserait le panneau cliquable hors du mode
   déplacement, par le même mécanisme : c'est ce que montrait la simulation, qui l'allumait dans son constructeur ; elle
   l'allume désormais une fois chargée, comme le joueur sur un overlay déjà affiché. En jeu, improbable, non vérifié.
3. **≈ 31 ms, pas 16.** La sonde de la simulation passe une fois toutes les ≈ 31 ms (granularité de 15,6 ms de la minuterie
   de Windows) ; celle d'HDT est le même code, mais sa cadence n'a pas été mesurée dans HDT. « 60 Hz » est l'intention.
4. Une capture de souris prise par `PanelMover` fait traiter à WPF un déplacement du **vrai** curseur au milieu de la
   pression injectée, et le panneau le suivait loin de l'écran : la simulation absorbe l'entrée de la vraie souris tant que
   la souris injectée tourne.

## Ce que la simulation ne reproduit toujours pas

La conversion de la position du curseur à l'écran en pixels de l'overlay (mise à l'échelle de Windows) ; un curseur
immobile sous une fenêtre qui change (seul un déplacement injecté renseigne WPF) ; les éléments propres à HDT et son état
« derrière le jeu » ; WPF lui-même, dont le comportement ici est un modèle déduit. Les contrôles « survol » d'avant gardent
leurs événements levés à la main.
