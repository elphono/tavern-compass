<!-- visuel : dérogation — journal de séance, au format Markdown des autres journaux du dépôt -->
# Journal — 2026-10-04 : les « Comp Guides » de HDT dans un panneau à part

Demande d'Ali, mot pour mot : « je trouve que nos compos suggérées sont pourries par rapport à celles de HDT. Va
récupérer celles de HDT et il faut les présenter de la même façon (classées) par Tier. Et il faut mettre en valeur
celles qui deviennent le plus probable grâce à notre board et notre main. Pour l'instant ne remplace pas d'IHM on va
rajouter un panneau. »

Livré sous WSL (tests, build, déploiement des DLL) ; **rien n'est encore vu en jeu**.

## 1. D'où viennent les compos de HDT — mesure

Inspection des assemblies **installées** (`app-1.58.6`), décompilées type par type avec `ilspycmd` 8.2 (outil posé
dans le dossier temporaire de la session, pas dans le dépôt), recoupée avec le source public de HDT (`master`).

| Maillon | Constat | Visibilité |
|---|---|---|
| `Hearthstone_Deck_Tracker.API.Core.OverlayWindow` | l'API des plugins rend la fenêtre d'overlay | public |
| `OverlayWindow.BattlegroundsCompsGuidesVM` | le modèle de vue des Comp Guides, créé une fois par HDT | public |
| `BattlegroundsCompsGuidesViewModel.Comps` | liste **gratuite** (`List<BattlegroundsCompGuideViewModel>`) | public |
| `BattlegroundsCompsGuidesViewModel.CompsByTier` | liste **Tier 7** (`Dictionary<int, TieredComps>`) | public |
| `BattlegroundsCompsGuidesViewModel.CurrentState` | `Loading`, `BaseFeature`, `Tier7Feature`, `Empty`, `Error` : ce que HDT affiche | public |
| `BattlegroundsCompGuideViewModel.CompGuide` | l'objet HSReplay derrière chaque ligne | public |
| `HSReplay.Responses.BattlegroundsCompGuide` (HSReplay.dll) | `Name`, `Tier`, `TierRank`, `Difficulty`, `PrimaryTribe`, `RepresentativeCard`, `CoreCards`, `AddonCards` (dbf ids), `HowToPlay`, `WhenToCommit`, `CommonEnablers`, `LastUpdated` | public |
| `ApiWrapper.GetCompsGuides` → `HsReplayClient.GetCompsGuides` | `GET https://hsreplay.net/api/v1/battlegrounds/comp_guides/?game_language=<langue>`, sans clé d'API (seuls `Accept` et l'User-Agent de HDT) | `ApiWrapper` est **internal** |
| `HsReplayClient.GetTier7CompsGuides` | `…/comp_guides/tier7/?game_language=…&minion_types=…`, en-tête `X-Trial-Token` ou OAuth | — |

Ce que HDT en fait :

| Vue de HDT | Ordre | Source |
|---|---|---|
| gratuite (`BaseFeature`) | **une seule liste, par nom** (`OrderBy(comp => comp.Name)`), un badge de tier S…D par compo | `Comps` |
| Tier 7 (`Tier7Feature`) | **groupée par tier** (clé 1 → S … 5 → D, croissante), puis `TierRank` croissant ; filtrée sur les tribus du lobby | `CompsByTier` |

HDT charge ces listes lui-même à chaque début de partie Battlegrounds (`OnMatchStart`, appelé par `ShowBgsTopBar`)
et en pré-lobby. Les mêmes membres publics existent dans la 1.55.6 (release GitHub contre laquelle le dépôt compile par
défaut) : le plugin compile contre les deux, 0 erreur, 0 avertissement.

Commandes et sorties (extraits) :

```
$ ilspycmd -t Hearthstone_Deck_Tracker.API.Core HearthstoneDeckTracker.exe
public class Core
	public static OverlayWindow OverlayWindow => Hearthstone_Deck_Tracker.Core.Overlay;

$ grep -n 'BattlegroundsCompsGuidesVM' Hearthstone_Deck_Tracker.Windows.OverlayWindow.decompiled.cs
743:	public BattlegroundsCompsGuidesViewModel BattlegroundsCompsGuidesVM { get; } = new BattlegroundsCompsGuidesViewModel();
5242:		BattlegroundsCompsGuidesVM.OnMatchStart();

$ grep -n 'public \|OrderBy\|ApiWrapper' …BattlegroundsCompsGuidesViewModel.decompiled.cs
49:	public List<BattlegroundsCompGuideViewModel>? Comps
62:	public Dictionary<int, TieredComps>? CompsByTier
75:	public CompGuideListState CurrentState
247:			return (from comp in (await ApiWrapper.GetCompsGuides(Helper.GetCardLanguage()))?.OrderBy((BattlegroundsCompGuide comp) => comp.Name)
269:			foreach (KeyValuePair<int, List<BattlegroundsCompGuide>> item in battlegroundsTier7CompsGuidesData2.ByTier.OrderBy(… t.Key))
272:				List<BattlegroundsCompGuideViewModel> comps = (from comp in item.Value.OrderBy((BattlegroundsCompGuide comp) => comp.TierRank).ToList()

$ grep -n 'class ApiWrapper' …ApiWrapper.decompiled.cs
19:internal class ApiWrapper

$ grep -n -A6 'GetCompsGuides' HSReplay/HsReplayClient.cs      (HSReplay.dll décompilé)
256:	public async Task<BattlegroundsCompsGuidesData> GetCompsGuides(string gameLanguage)
258:		using HttpWebResponse httpWebResponse = await _webClient.GetAsync("https://hsreplay.net/api/v1/battlegrounds/comp_guides/?game_language=" + gameLanguage);
```

### Les quatre voies, dans l'ordre de préférence demandé

| Voie | Résultat | Retenue |
|---|---|---|
| (a) API publique de HDT | `Core.OverlayWindow.BattlegroundsCompsGuidesVM` → `CurrentState`, `Comps`, `CompsByTier`, `CompGuide` : tout est public | **oui** |
| (b) la même par réflexion | inutile : rien d'internal sur le chemin | non |
| (c) HSReplay.dll directement | inutile ; `HsReplayClient` est public mais refaire la requête doublerait celle de HDT | non |
| (d) GET direct au point d'accès gratuit | **200** sur un simple `curl` sans aucun en-tête ajouté (voir ci-dessous) ; mesuré pour connaître la forme des données, **pas utilisé** par le plugin | non |

Avantage décisif de (a) : le panneau montre **exactement** la liste que HDT affiche — la gratuite, ou la Tier 7 filtrée
sur le lobby quand Ali a Tier 7 ou un essai —, sans aucune requête de plus.

### Forme réelle des données (une lecture, le 2026-10-04)

```
$ curl -sS -o comp_guides_enUS.json -D headers.txt -w 'http=%{http_code} bytes=%{size_download} type=%{content_type}\n' \
    "https://hsreplay.net/api/v1/battlegrounds/comp_guides/?game_language=enUS"
http=200 bytes=27114 type=application/json
HTTP/2 200 · server: cloudflare · cache-control: public, max-age=60, stale-while-revalidate=600 · cf-cache-status: HIT
```

Extrait **anonymisé** (aucune donnée réelle de HSReplay n'entre dans le dépôt : valeurs remplacées par leur nature) :

```json
[{"id": <entier>, "name": "<nom de la compo>", "tier": 2, "tier_rank": 1, "difficulty": 1, "primary_tribe": <Race>,
  "core_cards": [<dbf>, <dbf>, <dbf>, <dbf>], "addon_cards": [<dbf>, <dbf>, <dbf>], "representative_card": "<card id>",
  "how_to_play": "<texte> [[<nom de carte>||<dbf>]] <texte>",
  "when_to_commit": "[[<nom>||<dbf>]] + [[<nom>||<dbf>]] + [[<nom>||<dbf>]]",
  "common_enablers": "[[<nom>||<dbf>]]\n[[<nom>||<dbf>]]",
  "summary": "<texte>", "previous_tier": null, "last_updated": "<date ISO>", "tier_last_updated": "<date ISO>",
  "created_at": "<date ISO>", "hidden": false}, …]
```

Mesures sur cette lecture (script Python local, non versionné) :

| Mesure | Valeur |
|---|---|
| compos | 23, aucune `hidden` |
| tiers | 1 (S) : 4 · 2 (A) : 13 · 3 (B) : 6 |
| `tier_rank` | des égalités dans chaque tier (S : 0, 0, 0, 2) → départage nécessaire |
| cartes clés / d'appoint par compo | 2 à 6 / 1 à 7, jamais les mêmes |
| cartes citées dans `when_to_commit` | 51 : 43 clés, 7 d'appoint |
| cartes citées dans `common_enablers` | 30 : 21 clés, 7 d'appoint, 2 seulement hors des deux listes |
| références sans dbf id | 0 |

## 2. Ce qui a été construit

| Pièce | Rôle |
|---|---|
| `Stats/CompGuides.cs` | modèle `CompGuide`, `CompGuideSet` groupé par tier ; `CompGuideParser` lit le format ci-dessus (liste gratuite, ou `{"by_tier": …}`), et les objets de HDT à travers leurs attributs JSON ; `CompGuideText` lit `[[Nom||dbf]]` comme HDT (`ReferencedCardRun.ParseCardsFromText`) |
| `Stats/CompGuideMatch.cs` | score de chaque compo d'après le plateau **et** la main ; les 3 meilleures (score non nul) mises en valeur |
| `Stats/CompGuideLayout.cs` | place par défaut du panneau, et ce qui tient (`Fit`) |
| `HdtPlugin/HdtCompGuides.cs` | lecture de la liste de HDT par l'API publique ; relue seulement quand HDT change de liste ou d'état |
| `HdtPlugin/CompGuidesPanel.cs` | le panneau, clé de layout `comp-guides` |

**Ordre** : tiers dans l'ordre de HDT (S, A, B, C, D) ; dans un tier, `tier_rank` croissant puis le nom (l'ordre de la
liste gratuite de HDT). C'est la présentation de la vue Tier 7, appliquée à la liste que HDT affiche ; la vue gratuite
de HDT n'est pas groupée, mais chaque compo y porte le même tier.

**Score** = 3 × cartes clés tenues + 2 × enablers tenus + 1 × cartes d'appoint tenues ; chaque carte compte une fois,
dans son rôle le plus fort (un enabler qui est aussi une carte clé compte comme carte clé). Égalité : la plus grande
part de cartes clés tenues, puis l'ordre de HDT. Les compos mises en valeur remontent en tête de **leur** tier ; l'ordre
des tiers ne bouge pas.

**Place par défaut** : le bas gauche du cadre 4:3, symétrique du panneau des compos visées de l'autre côté du héros
(à droite des MMR du classement, à gauche du héros, sous le plateau, jusqu'à l'or). La marge gauche hors du cadre a été
écartée : c'est la place par défaut du widget de session de HDT (`Config.SessionRecapLeft = 0`, `SessionRecapTop = 15`).
Elle ne tient pas les 23 compos : le panneau garde d'abord les compos mises en valeur, puis les autres dans l'ordre, et
dit « k of 23 shown ». Déplacé plus haut par « Move panels », il en montre davantage.

**Journal HDT** : `Bronzebeard HUD: comp guides loaded from HDT (hdt-free, state BaseFeature): 23 comps, tiers [S=4 A=13 B=6], unknown cards 0`
à chaque nouvelle liste ; `Bronzebeard HUD: comp guides round=5 source=hdt-free comps=23 board=4 hand=2 highlighted=[1. … 6 ★2/4; …]`
à la fin de chaque tour de taverne ; `Bronzebeard HUD: comp guides: none from HDT (state Loading)` tant que HDT n'a rien.

## 3. Reste à vérifier en jeu

1. La ligne `comp guides loaded from HDT` apparaît au premier tour, avec 20 à 30 compos.
2. Le panneau ne masque rien du jeu au bas gauche (main pleine comprise) et ne double pas le widget de session de HDT.
3. Les compos mises en valeur suivent bien ce qu'Ali achète et garde en main.
4. En partie Tier 7 (essai), la source affichée passe à « Tier 7 » et la liste suit celle de HDT.
