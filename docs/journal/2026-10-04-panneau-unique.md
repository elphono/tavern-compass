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
| Source | les guides de HDT ; les cibles = cochés puis plus probables, 1 à 4 (− n +), couleur stable pendant la partie |
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
