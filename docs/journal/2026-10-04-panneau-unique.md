<!-- visuel : dérogation — journal de séance, au format Markdown des autres journaux du dépôt -->
# Journal — 2026-10-04 : un seul panneau « Compositions », sur les guides de HDT

## La demande d'Ali, en sept points

1. Les ovales sont trop petits.
2. Le panneau « HDT comp guides » est illisible : des noms de cartes en texte, un « ★1/3 +1 » incompréhensible, et une
   compo « de plus de 7 cartes ».
3. « Target compositions » et « HDT comp guides » disent la même chose deux fois ; l'ergonomie de HDT est meilleure.
4. Le cadre « how top boards field it » (le « ? » au-dessus des sbires de Bob) est inutile.
5. Les cadres en taverne doivent suivre automatiquement la compo qu'il semble construire, et changer avec elle.
6. Plusieurs compos à la fois, chacune avec sa couleur.
7. Les aides de découverte, de trinket et de Dark Gift doivent s'appuyer sur les mêmes cibles.

## La compo « de plus de 7 cartes » : cause mesurée

La ligne d'une compo mise en valeur listait **toutes** les cartes tenues — cartes clés, enablers et add-ons concaténés —
puis « need » et les cartes clés manquantes. Une compo à 3 cartes clés, 2 enablers et 2 add-ons tenus en affichait 7,
plus les manquantes. HDT ne mélange jamais les listes ; le panneau ne le fait plus nulle part (`CompGuideProgress` n'a
plus de liste « tout ce qui est tenu »).

## Décisions

| Sujet | Décision |
|---|---|
| Panneaux | un seul, `CompsPanel`, clé de layout `target-compositions` (la place sauvegardée d'Ali reste valable) |
| Source | les guides de HDT ; les cibles = les cochés **seuls** s'il y en a, sinon les plus probables, 1 à 4 (− n +, sans effet tant qu'une case est cochée), couleur stable pendant la partie |
| Ligne de guide | case, nom (deux lignes au besoin), cartes clés seules en ovales de 54 ; une cible : liseré, teinte, rang |
| Détail | comme HDT : la liste est remplacée, « ← All comp guides », sections dans l'ordre de HDT |
| Taverne | carte clé d'une cible → cadre plein, enabler ou add-on → pointillés, couleur de la cible (`TavernHighlights`) |
| Choix | étiquette dans la couleur de la première cible servie (`ChoiceAdvisor`) |
| Disparaissent | la liste Firestone, la place moyenne Firestone par compo, le panneau `lineups` et son « ? », « ★k/N +m », « need » |
| Restent dans le code | `CompService`, `TavernAdvisor`, `CompositionRows`, `CompDetail`, `CompTransitions`, `MinionLineups` (stats gardées pour orienter les aides plus tard ; `TavernAdvisor.Aim` n'est plus appelé) |

Arbitrages pris en route, là où la demande laissait le choix :

- **« k of n shown » dans la barre de titre**, rythme resserré (lignes 3 px d'écart, 1 px de marge, barres de tier en
  13 px). Mesuré dans la simulation à 1920 × 1080 : avec l'écart de 6 et une ligne « k of n » sous la liste, trois cibles
  sur deux tiers demandaient 373 px pour 330 à la place par défaut, et la troisième cible disparaissait ; elles tiennent
  maintenant (≈ 322 px). Le liseré n'empiète plus sur la marge : WPF découpait l'élément à sa place et le liseré
  disparaissait (vu sur la capture).
- **Sections du détail** : dans l'ordre, chacune prise si elle tient dans ce qui reste, sinon omise entière ; une
  section plus courte après elle peut encore passer (« 3 of 5 sections »). À la place par défaut en 1080p, deux ou trois
  sections tiennent : le détail complet demande d'agrandir le panneau.
- **Plus de six cartes clés** (aucun guide mesuré n'en a plus de six) : cinq ovales et « +k », six remplissant la ligne.
- **Badge de difficulté** omis quand HDT dirait « Unknown » (échelle lue dans l'IL de HDT 1.58.6 : 1 Hard, 2 Medium, 3 Easy).
- **Tribus** : `((Race)20).ToString()` vaut « BEAST » sous .NET Framework 4.8 (celui d'HDT) mais « PET » sous .NET 8 —
  mesuré sur `HearthDb.dll` 1.58.6. Aujourd'hui juste par chance du runtime : les tribus du lobby et des cartes sont
  désormais nommées par valeur (`GuideTribes.NameOrEnum`).
- **Garde-fous** : « compositions » réunit « comp-guides » et « tavern-advice » ; « tavern-markers » pour les cadres et
  les ◇ ; « comp-pivots » remplace « comp-transitions » ; « minion-lineups » et « hero-affinity » disparaissent.

## Ce qui reste à voir en jeu

Rien de ceci n'a tourné sous HDT : la liste et ses couleurs sur de vrais guides (noms longs, Tier 7), le détail et ses
textes réels, les cadres sur les vraies cartes de Bob, les étiquettes de choix, et le pointage des cases et des noms
au-dessus du jeu. Vu dans la simulation (`tools/BronzebeardHud.Harness`, `--selftest`, captures liste et détail).

## Correction d'Ali : une case cochée restreint

Première livraison : une compo cochée passait en tête des cibles et les plus probables complétaient jusqu'à n. Ali :
« les checkbox devant les compos restreignent les cartes highlightées dans le shop, une compo checkboxée est une compo
vers laquelle on veut se diriger » (c'était d'ailleurs le sens de l'ancien panneau : la case visait la compo seule).
Maintenant `CompTargets.Choose` rend les cochées seules ; sans cochée, les plus probables. Le titre dit « k chosen » et
− / + sont grisés. Vérifié dans la simulation par de vrais clics sur les cases (`--selftest`) et par mutation.

## Survol : le guide complet en popup

Ali : « le HDT Comp guide devrait faire pop le guide complet de la comp (comme dans HDT) en popup quand on survole, ou en
remplaçant le contenu du panel en cliquant ». Le clic existait (détail, « ← All comp guides ») ; le survol manquait, et
le détail à la place par défaut n'affiche que trois sections sur six.

**Mesure, HDT 1.58.6 décompilé** (`Windows/OverlayWindow.cs` 2414-2502 et 2640-2805,
`Utility/Extensions/OverlayExtensions.cs` 173-240) :

| Ce que fait HDT | Conséquence |
|---|---|
| sonde ≈ 60 Hz purement géométrique : tout élément déclaré `IsOverlayHoverVisible` dont le rectangle contient le curseur reçoit `MouseEnter` (`CustomMouseEventArgs`), `MouseLeave` quand il en sort | une ligne et l'ovale qu'elle contient sont « entrés » ensemble ; passer de l'une à l'autre ne lève rien sur la ligne |
| au-dessus d'un élément cliquable (`IsOverlayHitTestVisible`), WPF lève **en plus** ses propres `MouseEnter` / `MouseLeave` ; l'infobulle de HDT ne tient compte, pour un élément survolable, que de ceux de la sonde | nos gestionnaires peuvent être appelés deux fois, et WPF peut lever un `MouseLeave` alors que le curseur est encore sur la ligne (fenêtre repassée en clic-transparent) |
| `SetTooltip` : **un seul** emplacement d'infobulle pour tout l'overlay (`Children.Count > 0 → return`), fermé au `MouseLeave`, sans taille maximale | une infobulle sur la ligne occuperait l'emplacement : les ovales de cette ligne ne montreraient plus leur carte |
| `SetTooltip` place l'infobulle d'après sa taille mesurée (`ActualWidth`, `ActualHeight`) juste après l'avoir ajoutée | une `Image` sans source mesure 0 × 0 : voir plus bas |

**Décision** : pas d'infobulle de HDT. Le popup (`GuidePopup`) est un élément du canvas, comme les panneaux ; le
`MouseEnter` d'une ligne lance une minuterie de 250 ms (parcourir la liste ne fait pas défiler les popups), son
`MouseLeave` l'annule et cache. Rien dans le popup n'est survolable ni cliquable ; les aperçus de cartes gardent
l'infobulle de HDT, au-dessus du popup, et peuvent se montrer en même temps sans le recouvrir. Les sections sont
construites par le même code que le détail (`GuideView`).

Arbitrages pris en route :

- **Événements de la sonde et de WPF traités pareil** (`GuideHover`, testé) : une deuxième entrée sur la ligne ne change
  rien ; une sortie alors que le curseur est **encore dans le rectangle de la ligne** (`GetCursorPos`, la même mesure
  que la sonde) est ignorée. Sans cela, le `MouseLeave` que WPF lève quand HDT repasse sa fenêtre en clic-transparent
  (curseur passé du nom au fond de la ligne) cacherait le popup, et la sonde, qui tient la ligne pour entrée, ne le
  ferait jamais revenir. Déduit du code, **pas vu en jeu**.
- **Place** (`GuidePopupLayout.Place`) : la demande voulait le bord droit du popup sur celui du panneau **et** x ≥ 1442
  en 1080p ; les deux ne tiennent pas ensemble (1669 − 400 = 1269, sur les plateaux). Les zones du jeu l'emportent : bord
  droit sur celui du panneau quand rien ne gêne, poussé de côté sinon. Mesuré dans la simulation, 1920 × 1080 : popup en
  (1452, 135) 400 × 547 en taverne, les six sections d'un guide qui les a ; en combat, au-dessus de Skip combat,
  (1452, 17) 400 × 547. En 4:3 (1440 × 1080) la colonne à droite des plateaux n'a que 217 px : le popup passe au-dessus
  des plateaux, 301 px de haut, sections réduites (calculé par `GuidePopupLayout`, pas regardé dans la simulation). Il
  évite aussi toute la place qu'un aperçu de carte du panneau peut prendre (`PreviewArea`).
- **Caché** au clic qui ouvre le détail, au changement de phase (taverne ↔ combat), en mode déplacement, panneau caché.

**Trouvé en route, l'aperçu de carte au premier survol.** La simulation remplaçait l'infobulle de HDT par une infobulle
WPF, qui se remesure toute seule. Refaite selon le code de HDT (un emplacement sur le canvas, placé d'après la taille
mesurée de l'infobulle), elle a montré l'aperçu d'un ovale **sur le panneau**, décalé vers le bas et coupé par le bord
de la fenêtre : `FullCard` rendait l'`Image` elle-même, dont la source n'est posée qu'à son `Loaded`, après le calcul de
HDT ; une `Image` sans source mesure 0 × 0. Les ovales étant reconstruits à chaque redessin du panneau, c'est le cas de
chaque premier survol. Corrigé : `FullCard` rend une boîte de la taille de la carte. Avant / après vus sur capture ;
**pas vu en jeu**.

**Ce que la simulation ne montre pas** : la sonde de HDT (la simulation lève `MouseEnter` / `MouseLeave` elle-même), le
double `MouseEnter` réel, le `MouseLeave` de WPF quand la fenêtre repasse en clic-transparent (simulé en disant au popup
que le curseur est encore dans la ligne), et l'emplacement unique d'infobulle de HDT ailleurs que dans sa copie, écrite
d'après le code décompilé.

## Stats Firestone → aides

Depuis la fusion, les compositions de Firestone étaient chargées sans servir à rien d'autre que l'encart des héros. Le
pont (`GuideBridge`, écrit et testé dans `Stats` la même journée) est maintenant passé par le plugin.

**Ce qui est branché.**

| Où | Ce qui change à l'écran | Code |
|---|---|---|
| journal | `bridge: Guide → compo (k/N keys, m cards, n games); Autre → no match (k/n guides bridged, against m compositions; Firestone <état>)`, à chaque recalcul | `Plugin.UpdateBridge` : nouvelle liste de guides de HDT, ou chargement des compos (`CompService.Version`) |
| choix | « + T · 4/5 boards » après le rôle d'une cible ; « + T 3/5 boards » (couleur de T) pour une carte qu'aucune liste de T ne nomme mais sur ≥ 2 plateaux finaux de sa compo ; « pivot → G (S) » (neutre) pour une carte clé d'un guide vers lequel une cible peut pivoter | `ChoiceAdvisor.Advise(…, bridge)`, aucun texte nouveau côté plugin |
| taverne | cadre pointillé « + T 3/5 » sur une carte de Bob dans le même cas, après tous les rôles ; la ligne `tavern highlights=[…]` le dit (`carte:boards 3/5:guide`) | `TavernHighlights.For(Bob, cibles, pont)`, `TavernHighlights.Summary` |
| détail et popup d'un guide | une ligne sous l'en-tête : « ≈ 3,5 with your hero (23) · final turn ≈ 13 · 5 top boards » (12 px, gris), omise sans pont | `TargetContext.For`, `GuideView.Context` |

Le morceau « with your hero » vient de `HeroCompAffinity.Effects`, qui n'était plus calculé depuis la fusion : il est
rebranché à moindre coût (recalculé quand le héros joué ou les compos changent, comme avant la fusion). Un garde-fou à
part, `guide-bridge` : s'il tombe, plus de pont, et les textes d'avant mot pour mot.

**Les critères d'honnêteté du pont**, inchangés (tâche précédente) : un guide n'est relié à une compo que si elle partage
**au moins deux** de ses cartes (clés et add-ons ; ses enablers ne comptent pas, presque toutes les compos les jouent)
**et au moins la moitié de ses cartes clés** ; un guide sans carte clé n'est jamais relié ; entre plusieurs compos
honnêtes, le plus de cartes clés, puis de cartes, puis de parties, puis l'id ; sinon **aucun pont plutôt qu'un pont
douteux**. Les « + T k/n boards » et les cadres « plateaux » exigent en plus ≥ 2 des plateaux finaux. Jamais la place
moyenne de la compo (Ali l'avait écartée).

**Masquage pendant un choix — décision du pilote, à confirmer en jeu par Ali.** Vu dans la simulation : pendant une
découverte ou un Dark Gift, les cadres, étiquettes et ◇ de Bob restaient dessinés sur les options, et en Dark Gift les ◇
cliquables tombaient **dans** les cartes (un clic épinglait au lieu de choisir) ; le panneau, à sa place par défaut en
1080p, couvre le bas de la 3e option (mesuré par le self-test sur `ChoiceLayout`, constantes de HDT : 302 × 43 px en
découverte, 359 × 162 px en Dark Gift). Maintenant (`ChoiceCover`) : tant qu'un choix est ouvert **en taverne**, ces
marqueurs, le panneau et son popup sont retirés de l'écran, puis remis tels quels à la fermeture, sans recalcul (le
panneau garde les mêmes éléments si rien n'a changé ; la capture refermée est identique au pixel près à une taverne
sans choix). Les étiquettes du choix portent les cibles et leurs couleurs pendant ce temps. Une ligne par transition.
Arbitrages pris en route : un choix **sans disposition connue** (héros, pouvoirs, quêtes…) masque aussi, ses options
couvrant la rangée de Bob de la même façon ; seulement en taverne (en sélection des héros rien n'est affiché, et le combat
rétablit tout même si un choix reste listé). Réversible : `ChoiceCover.IsOpen`, ou le garde-fou `choice-cover`.

**Ce que coûte la ligne de contexte**, mesuré dans la simulation : à la place par défaut en 1080p, le détail de Pirate
Discover passe de 3 à 2 sections sur 5 ; le popup en combat (au-dessus de Skip combat) de 6 à 5 sections sur 6. En
taverne, le popup tient toujours ses six sections.

**Ce qui n'est pas mesuré.**

- **Le taux de pontage réel** : 24 compos Firestone contre ≈ 23 guides de HDT, deux nomenclatures écrites par des gens
  différents ; personne n'a compté combien se recoupent au seuil choisi. Dans la simulation, 2 guides sur 15 sont pontés,
  sur des compos **inventées pour l'être** : ce chiffre ne dit rien du jeu. La ligne `bridge:` du journal d'HDT, à la
  première partie, est la mesure qui manque ; si presque tout est « no match », les nouvelles aides ne s'afficheront
  presque jamais, et c'est le seuil qu'il faudra rediscuter, pas le code.
- **Un choix qui resterait « ouvert »** : le masquage suit la même lecture que les étiquettes des choix (options en zone
  SETASIDE, `ChoiceClassifier`). Si HDT gardait une liste d'options périmée dans cet état, le panneau disparaîtrait pour le
  reste du tour ; jamais vu, mais jamais vu en jeu non plus.
- Rien de tout ceci n'a tourné sous HDT : vu dans la simulation (`--selftest`, captures `--choice discover`,
  `--choice discover --close-choice`, `--hover 1`), sur des compos Firestone synthétiques (`HarnessData.FirestoneComps`).

