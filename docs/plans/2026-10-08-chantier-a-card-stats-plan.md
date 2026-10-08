# Chantier a — card-stats au format commun : plan d'implémentation

> **Pour l'exécutant :** exécuté en natif dans la session (Ali : « tu peux lancer »), tâche par tâche, en TDD
> (superpowers:test-driven-development). Cases `- [ ]` pour le suivi.

**But :** charger card-stats de Firestone comme première source au format commun (provenance + effectif), et
s'en servir pour deux aides — la valeur d'un sbire de Bob à ce tour (composant 1) et l'étiquette d'un choix
qui n'avait que « — » (composant 2) — avec une tranche de MMR changeable à chaud dans l'overlay (composant 10).
Précédé du chantier e : `RepositoryHygieneTests` refuse les données réelles.

**Architecture :** une seule pièce nouvelle par responsabilité (note § 9). Bibliothèque `BronzebeardHud.Stats` :
`StatProvenance` (format commun, réutilisé par les sources suivantes), `CardStatsFile` + chargeur/import
(la source Firestone pour les cartes), `CardTurnValue` (fonction pure : une carte, un tour → un verdict),
`StatsCache.GetCardStatsAsync` (le cache existant). Plugin : un `CardStatsService` qui câble cache, tranche et
garde-fou ; `ChoiceAdvisor` et `TavernHighlights` reçoivent une fonction `cardId → note`, sans savoir d'où elle vient.
Ni registre, ni consolidation (chantier b) : une seule source de cartes.

**Stack :** C# (`netstandard2.0` pour Stats, `net48` pour le plugin), xUnit sur `net8.0`, Newtonsoft.Json 13.0.3.

**Spec :** `docs/plans/2026-10-08-stats-multi-sources.html` (§ 9, § 11 composants 1, 2, 10 ; § 12 chantiers a, e ;
§ 14 décisions 1, 2, 8, 11).

## Contraintes globales

- Langue : code, commentaires, tests et textes d'interface en anglais ; docs en français.
- Aucune donnée réelle de Firestone dans le dépôt : tests sur données synthétiques (le chantier e le vérifie).
- Aucun texte du plugin sous 12 px en 1080p ; aucun `Viewbox` ; aucun `FontSize = <nombre>` dans le plugin.
- Avant de pousser : `dotnet format whitespace --folder --verify-no-changes .`, `dotnet test -warnaserror`, builds
  Release du plugin et de la simulation en `-warnaserror`.
- Une étiquette ne passe jamais devant une étiquette de cible ; deux lignes au plus par étiquette.
- Corrélation, pas cause : l'étiquette donne le chiffre et sa comparaison, jamais l'ordre « achète ».
- Une ligne `Bronzebeard HUD: …` par chargement et par changement de tranche ; un garde-fou `card-stats` qui coupe la
  fonctionnalité seule.

## Ce que la mesure du 2026-10-08 a changé à la spec

Mesuré sur les vrais fichiers (scratchpad, hors dépôt) : `averagePlacement` (jouée à ce tour) contre
`averagePlacementOther` est **toujours** meilleur — 320 cartes sur 320 au tour 6 (écart de −0,19 à −3,1). Jouer une
carte va avec survivre ; la comparaison du § 11 (« 3.9 vs 4.4 ») marquerait ▲ partout. On compare donc la carte à
**la moyenne des cartes jouées au même tour** (pondérée par `totalPlayed`) : écart de −0,29 à +0,30 (p10–p90) pour
une erreur type médiane de 0,04 — la carte se distingue. `averagePlacementOther` est souvent `null` (4 103 cellules),
`turn` aussi (429) : ignorés. Effectifs par cellule (tours 3–10) : médiane 191 (mmr-100), 143 (50), 118 (25), 92 (10).

## Points d'attention (non couverts par un test de tâche, à surveiller)

1. Tour au-delà des données (tour 20+) : aucune étiquette, jamais une valeur d'un autre tour — testé (tâche 4).
2. Carte dorée (`_G`) : ramenée à sa base avant la recherche — testé (tâche 4).
3. Tranche absente chez Firestone (fichier en échec) : on garde ce qu'on avait, la ligne de journal dit `FAILED` — testé
   par le cache existant (tâche 3, même `GetAsync`).
4. Changement de tranche pendant un chargement : le résultat d'une tranche périmée est jeté — testé (tâche 7).
5. Bob's row où plusieurs cartes ont une valeur : chaque étiquette s'aligne sur sa carte (TavernLayout) — vu dans la
   simulation (tâche 9).

---

### Tâche 1 : chantier e — le dépôt refuse les données réelles

**Fichiers :** modifier `tests/BronzebeardHud.Stats.Tests/RepositoryHygieneTests.cs`.

Règle : hors `bin`, `obj`, `lib`, `.git`, `.claude`, `node_modules`, aucun fichier `*.gz` ni `*.gz.json` ; aucun `.json`
de plus de 100 Ko ; aucun `.json` qui porte une signature de fichier serveur (`"lastUpdateDate"` de Firestone,
`"highPlayers"` de nomi.gg). Les fixtures synthétiques vivent dans des `.cs` ou de petits `.json` sans ces clés.

- [ ] Test d'abord : `DataScanner_FlagsRealStatsFiles_AndLetsSyntheticOnesThrough` sur `DataProblems(relativePath, sizeBytes, text)` :
  `card-stats.gz.json` → signalé ; `x.json` de 200 000 octets → signalé ; `{"lastUpdateDate": "…"}` → signalé ;
  `{"overview": {"highPlayers": 30}}` → signalé ; la fixture `comp-cache-schema1-shape…json` (22 Ko, sans signature) → rien.
- [ ] Le faire échouer (méthode absente), puis l'écrire ; `Repository_HoldsNoRealStatsData` parcourt le dépôt.
- [ ] Témoin : copier un vrai fichier card-stats dans `tests/…/Fixtures/`, constater l'échec, le retirer, constater le vert.
- [ ] Commit `test: le dépôt refuse les fichiers de stats réels (chantier e)`.

### Tâche 2 : le format commun et le fichier card-stats local

**Fichiers :** créer `src/BronzebeardHud.Stats/StatProvenance.cs`, `src/BronzebeardHud.Stats/CardStats.cs` ;
tests `tests/BronzebeardHud.Stats.Tests/CardStatsTests.cs`.

**Produit :**
```csharp
public sealed class StatProvenance            // the common format's header, one per loaded file
{
    public StatProvenance(string source, string? sourceUrl, DateTimeOffset? generatedAt, DateTimeOffset? fetchedAt,
        string? timePeriod, int? mmrPercentile, string? patch);
    // Source: open list ("firestone", "nomi.gg", "hsreplay-manual"…); Patch: null until a source gives one.
    public JObject ToJson();  public static StatProvenance FromJson(JObject root, string path);
}
public sealed class CardTurnStat { int Turn; int Played; double AveragePlacement; }
public sealed class CardStat { string CardId; IReadOnlyList<CardTurnStat> Turns; }
public sealed class CardStatsFile
{
    public const int CurrentSchema = 1;
    StatProvenance Provenance; IReadOnlyList<CardStat> Cards;
    CardStat? Find(string cardId);                        // golden "_G" → base id
    double? TurnAverage(int turn);                        // played-weighted mean of every card at that turn
}
public static class CardStatsLoader
{
    CardStatsFile Load(string path); CardStatsFile Parse(string json); string Serialize(CardStatsFile file);
    CardStatsFile ImportFirestone(string firestoneJson, string sourceUrl, DateTimeOffset fetchedAt, int mmrPercentile);
}
```
Format local (schema 1, strict comme les autres : une règle cassée rejette le fichier) :
`{"schema":1,"source":"firestone","sourceUrl":…,"generatedAt":…,"fetchedAt":…,"timePeriod":"last-patch","mmrPercentile":25,"patch":null,
"cards":[{"cardId":"BG31_001","turns":[{"turn":6,"played":5293,"averagePlacement":3.6}]}]}`. Import : `cardStats[]`,
`turnStats[]` sans `turn`, sans `totalPlayed` > 0 ou placement hors [1, 8] ignorés ; `averagePlacementOther` jamais lu
(mesure ci-dessus) ; `lastUpdateDate` → `generatedAt`.

- [ ] Tests : import synthétique (deux cartes, tours avec `null`) → format local ; aller-retour `Serialize`/`Parse` ;
  schéma ≠ 1, carte dupliquée, `played` < 1, placement 9 → `StatsFormatException` ; `TurnAverage` pondéré
  (carte A 100 × 3,0 + carte B 300 × 5,0 → 4,5 ; pas 4,0) ; `Find("BG31_001_G")` → `BG31_001`.
- [ ] Rouge, implémentation, vert, commit `feat: card-stats au format commun (provenance, effectif)`.

### Tâche 3 : le cache et l'adresse de card-stats

**Fichiers :** modifier `FirestoneEndpoints.cs` (`CardStats(int mmrPercentile, string timePeriod)` →
`…/card-stats/mmr-{p}/{period}/overview-from-hourly.gz.json`), `StatsCache.cs` (`CardStatsPath(p, period)` →
`firestone-card-stats-mmr-{p}-{period}.json` ; `GetCardStatsAsync(p, period, policy, ct)` par `GetAsync`) ; tests dans
`StatsCacheTests.cs` (le `FakeFetcher` existant).

- [ ] Tests : premier appel télécharge et écrit le fichier par tranche ; deux tranches → deux fichiers distincts ; 304
  garde le cache ; l'URL demandée porte `mmr-25`.
- [ ] Rouge, implémentation, vert, commit.

### Tâche 4 : la valeur d'une carte à un tour (fonction pure)

**Fichiers :** créer `src/BronzebeardHud.Stats/CardTurnValue.cs` ; tests `CardTurnValueTests.cs`.

```csharp
public enum CardTurnVerdict { Better, Worse }
public sealed class CardTurnNote { int Turn; double Placement; double TurnAverage; int Played; CardTurnVerdict Verdict; }
public static class CardTurnValue
{
    public const int MinimumPlayed = 200;          // starting points (decision 8), recalibrated by the CLI
    public const double MinimumDelta = 0.1;        // places
    public const double PlacementSpread = 2.3;     // one game's placement varies by about 2.3 places (HeroCompAffinity)
    public static CardTurnNote? For(CardStatsFile? file, string cardId, int turn);   // null: nothing worth saying
    public static string? Label(CardTurnNote? note, int maxChars);                    // "t6 ▲ 3.6 vs 3.9" / "t6 ▼ 4.3 vs 3.9"
}
```
Règle : `null` si pas de fichier, carte ou tour inconnus, `Played < MinimumPlayed`, ou
`|placement − moyenne| < max(MinimumDelta, 2 × 2,3 / √Played)` (sous le bruit : rien n'est dit). Mieux placée
(placement plus petit) → `Better` (▲), sinon `Worse` (▼). `Label` : forme courte « t6 ▲ 3.6 » si la longue ne tient pas.

- [ ] Tests, chacun avec des valeurs distinctes : au-dessus du bruit → ▲ ; en dessous → ▼ ; écart 0,05 → `null` ;
  `Played` 199 → `null` ; tour sans donnée (tour 20) → `null` ; carte dorée trouvée ; label long puis court.
- [ ] Mutation : `<` → `<=` sur le seuil, `Better`/`Worse` inversés : un test doit tomber.
- [ ] Commit.

### Tâche 5 : composant 2 — l'étiquette d'un choix sans compo

**Fichiers :** modifier `ChoiceAdvice.cs` ; tests dans `ChoiceAdviceTests.cs`.

- `OptionAdvice` reçoit `CardTurnNote? cardValue = null` ; `ChoiceReason.CardValue` s'insère **juste avant** `None` :
  `… : Guides.Count > 0 ? Guide : CardValue != null ? CardValue : None`.
- `ChoiceAdvisor.Advise(…, Func<string, CardTurnNote?>? cardValue = null)` ne l'appelle que pour une découverte ou un
  Dark Gift (jamais un trinket).
- `Lines` : `ChoiceReason.CardValue` → `CardTurnValue.Label(...)`. Couleur : neutre (`Colour` reste `null`).
- [ ] Tests : option sans rien → « t6 ▲ 3.6 vs 3.9 » au lieu de « — » ; option carte clé d'une cible → l'étiquette de cible,
  la valeur ignorée ; trinket → inchangé ; sans `cardValue` → « — » mot pour mot (rien ne change pour un appelant ancien).
- [ ] Commit.

### Tâche 6 : composant 1 — la valeur sur les cartes de Bob

**Fichiers :** modifier `TavernHighlights.cs` (`MarkerLines(…, string? value = null)` : la valeur en dernière ligne
**s'il reste de la place** sous `maxLines`) ; `TavernMarkers.cs` (une carte qui n'a que la valeur : étiquette sans cadre,
fond neutre `#E63A3A44`, texte blanc ; une carte encadrée garde sa couleur) ; tests dans `TavernHighlightsTests.cs`.

- [ ] Tests : carte sans rôle + valeur → `["t6 ▲ 3.6 vs 3.9"]` ; carte clé + valeur → deux lignes, la clé d'abord ;
  carte clé + deux autres cibles (deux lignes pleines) → la valeur s'efface ; épinglée + clé → la valeur s'efface.
- [ ] Commit.

### Tâche 7 : câblage — `CardStatsService` et le garde-fou

**Fichiers :** créer `src/BronzebeardHud.Stats/StatsFileRefresh.cs` (la mécanique de `TrinketStatsRefresh` rendue
générique : `StatsFileRefresh<T>` avec `Func<T, DateTimeOffset?> fetchedAt` ; `TrinketStatsRefresh` en hérite sans
changer ses tests) ; créer `src/BronzebeardHud.HdtPlugin/CardStatsService.cs` ; modifier `Plugin.cs`.

- `CardStatsService(statsDirectory)` : un `StatsCache`, une tranche courante, `SetBracket(int)` qui relance un
  chargement si elle change ; `Poll()` ; `Note(cardId, turn)` ; `Version` ; `PendingLogLine`
  (`data card-stats mmr-25 last-patch: …`). Le résultat d'un chargement lancé pour une autre tranche que la courante est
  jeté (point d'attention 4).
- `Plugin` : garde-fou `card-stats` ; tranche = `_stats.Bracket` (ou la tranche choisie, tâche 8) ; `UpdateTavern` passe
  `CardTurnValue.Label(_cards.Note(id, turn), maxChars)` par carte de Bob (la clé de redessin inclut `_cards.Version` et le
  tour) ; `UpdateChoice` passe `id => _cards.Note(id, turn)` (clé : `_cards.Version`). Journal : `tavern values=[id:t6 ▲ …]`
  quand elles changent.
- [ ] Tests (Stats) : `StatsFileRefresh` — trois chargements de suite gardent le fichier quand le suivant échoue ;
  tranche changée pendant un chargement → résultat jeté (via un double de chargement contrôlé).
- [ ] Build du plugin en `-warnaserror` ; commit.

### Tâche 8 : composant 10 — la tranche changeable à chaud

**Fichiers :** créer `src/BronzebeardHud.Stats/BracketChoice.cs` ; tests `BracketChoiceTests.cs` ; modifier
`StatsService.cs` (`SetBracketOverride(int?)` : recharge les stats de héros dans cette tranche), `CompsPanel.cs` (un
bouton dans la barre de titre, avant « Meta ↗ »), `Plugin.cs`.

```csharp
public static class BracketChoice
{
    public static int Next(int current);          // 100 → 50 → 25 → 10 → 1 → 100
    public static string Label(int percentile);   // "all" / "top 25%"
}
```
- Un clic : tranche suivante pour les stats de héros (jauge comprise), les trinkets et card-stats ; oubliée à la partie
  suivante (comme + / −) ; ligne `bracket top 10% chosen (yours: top 25%)`.
- [ ] Tests : cycle complet sur trois tours ; libellés ; une tranche inconnue repart à 100.
- [ ] Build ; commit.

### Tâche 9 : simulation, vérification, documentation, déploiement

- [ ] Harness : un fichier card-stats synthétique, les valeurs sur Bob et sur un choix « — » ; captures en 1080p.
- [ ] `dotnet format …`, `dotnet test -warnaserror`, builds Release plugin et simulation (`-warnaserror`).
- [ ] Docs : `CLAUDE.md` (card-stats, tranche à chaud, lignes de journal), note HTML § 11 (comparaison à la moyenne du
  tour, mesure), `ROADMAP.md`.
- [ ] Déploiement : build Release contre l'HDT installé, copie des deux DLL, SHA-1 comparés.
- [ ] Commit, push.
