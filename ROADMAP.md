# Roadmap

State of the project on 2026-10-10. Detailed history, decision by decision, is in [`docs/journal/`](docs/journal/)
(in French); the specification and the plan the plugin follows are in [`docs/plans/`](docs/plans/).

| Horizon | What |
|---|---|
| **Shipped** | Hero-pick stats · MMR of the opponents · Compositions panel (HDT's comp guides as targets) · tavern frames · choice labels · lobby tribes filter · `−` / `+` that resize the panel for N comps · power inset (you and your opponent) · movable and resizable panels · simulation harness, with HDT's overlay layer on an injected mouse · packaged releases |
| **To verify in a live game** | Almost everything of the last two weeks: see below |
| **Next** | Statistics from several sources (card stats and nomi.gg done, see below) |
| **Not planned** | See the end of this page |

## To verify in a live game

These work in the simulation and in the tests; none has been seen in a live game yet. Each line says what to look for,
and, when there is one, the line of HDT's log that tells.

- **Power inset** — the lamps, the glow, the badge and the figures; `−` left of the inset, `+` right. Log: `power inset at …`.
- **`−` and `+`** — a press makes the panel take exactly N lines. Log: `targets n=… panel resized to …`.
- **The opponent's gauge** — in combat, the second row ("Opp. …") compares the board you are facing; in the tavern
  ("Next opp. …") your next opponent's last seen board. Whether HDT already holds that board at the moment the plugin
  reads it, and whether `NEXT_OPPONENT_PLAYER_ID` names the right player, have only been *deduced* from HDT's code.
  Log: `opponent power …`.
- **Lobby tribes filter** — no comp of a tribe that is not in the lobby shows up in the list, the frames or the choice
  labels. HDT has never been observed to hand the tribes over at that point. Log: `lobby tribes=[…] read at …`.
- **Ticked boxes** — ticking a comp keeps the comps you are already building ("in progress") next to the ones you
  chose, and the panel keeps room for them.
- **Bridge between HDT's guides and Firestone's comps** — how many of the 24 Firestone comps match the ~23 guides
  has never been measured. Log: `bridge: …`.
- **During a choice** — Bob's frames and labels hide while a discover, trinket or Dark Gift is open; the panel stays.
- **Resize handle** — pointing under HDT's real overlay, dotted frame, reset.
- **Guide popup on hover** — moving from a guide's name to the rest of its line keeps the popup; leaving the line hides
  it. Works in the simulation's copy of HDT's overlay; WPF's part of it is deduced. Log: `guide popup … shown at …`.
- **Statistics from several sources** (2026-10-10) — the hero badges show one line per hero, its sample size and its
  sources ("3.41 · 4,102 games", "FS 25% + nomi.gg"), or both figures in amber when two sources disagree; a trinket that
  nomi.gg also rates names it, or shows both figures; a banner under the heroes for the first three games of a patch; a
  guide's tribe since the patch in its detail and popup. Log: `hero pick why=[…]`, `patch banner …`, `freshness …`,
  `tier pace …`.
- **Hero-pick odds** (phases 5 and 6 of the [plan](docs/plans/2026-09-26-parite-tier7-plan.md)).
- **Skip combat** when relaunched by Battle.net.

## Open questions

- Duos have not been thought through for the opponent's gauge.

## Next

- **Packaged releases** — done on 2026-10-08: GitHub only hosts HDT up to 1.55.6, so a release is built by
  `tools/release.sh` against the HDT installed on the maintainer's machine, and its zip names that version.
- **Firestone card stats** — done on 2026-10-08, not yet seen in a live game: on Bob's cards and in place of a choice's "—", how a card did when played at this turn against every card played then ("t6 ▲ 3.6 vs 3.9"), nothing under the noise; the MMR bracket can be switched from the panel's title bar.
- **Simulation of HDT's overlay layer** — done on 2026-10-10: with `--mouse`, the harness drives its scene through a copy
  of HDT's click-through overlay window and of its hover probe (ported from HDT 1.58.9's code), on an injected mouse, and
  its self-test checks clicks, hover and the guide popup through it. The events WPF adds there (a second "enter" on a
  guide's name, a "leave" when the window turns click-through again) follow a model deduced from WPF, not seen in a live
  game. The cursor's screen-to-overlay conversion (display scaling) is still not simulated.
- **HSReplay manual import** is implemented but has never been used: the folder is empty and only Firestone's comps run.
- **Statistics from several sources**: one local format that keeps where each figure comes from (source, patch, MMR bracket, sample size), consolidation that shows "contested" rather than silently picking a source, Firestone's card stats first, then nomi.gg's patch analysis (its owner has agreed), with the MMR bracket switchable in game. Done on 2026-10-10 (each source recentred on its own mean, so that a population gap is not read as a disagreement); not yet seen in a live game. Design note (in French): [docs/plans/2026-10-08-stats-multi-sources.html](docs/plans/2026-10-08-stats-multi-sources.html).

## Not planned

| Idea | Why not |
|---|---|
| Timewarped notification | The mechanic is absent from the games of season 14 (checked on the logs). |
| Quest statistics | Firestone's file is empty. |
| "Next opponent" marker on the leaderboard | The game already shows it. |
| Combat history tab | Built, then removed: unused. |
| Reading the game's memory | HDT does it; the plugin only uses what HDT exposes. |
| Getting around HSReplay's anti-bot protection | The plugin makes no HSReplay request. |
