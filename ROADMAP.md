# Roadmap

State of the project on 2026-10-06. Detailed history, decision by decision, is in [`docs/journal/`](docs/journal/)
(in French); the specification and the plan the plugin follows are in [`docs/plans/`](docs/plans/).

| Horizon | What |
|---|---|
| **Shipped** | Hero-pick stats · MMR of the opponents · Compositions panel (HDT's comp guides as targets) · tavern frames · choice labels · lobby tribes filter · board power gauge · movable and resizable panels · simulation harness |
| **In progress** | Panel ergonomics (size set once, `+` / `−` pick how many comps) · gauge as a lit inset under the panel · same gauge for the enemy's board |
| **To verify in a live game** | See below |
| **Next** | Packaged releases · Firestone card stats for discovers with no comp · simulation of HDT's overlay layer |
| **Not planned** | See the end of this page |

## In progress

- **Panel ergonomics.** *Move panels* sets the panel's default place and size; a press on `+` or `−` resizes the
  panel to show exactly the N best comps. The board-power gauge leaves the main frame for a small inset below it,
  framed by the `−` / `+` buttons, with a glow on the lit level (red, yellow, green, shiny).
- **Enemy composition.** A similar indicator for the opponent's board, when it appears.

## To verify in a live game

These work in the simulation and in the tests; none has been seen in a live game yet. Each line says what to look for.

- **Lobby tribes filter** — no comp of a tribe that is not in the lobby shows up in the list, the frames or the choice
  labels. HDT has never been observed to hand the tribes over at that point: the plugin writes
  `lobby tribes=[…] read at …` in HDT's log when it has them.
- **Ticked boxes** — ticking a comp keeps the comps you are already building ("in progress") next to the ones you
  chose.
- **Power gauge** — grey on turns 1–2, then coloured.
- **Bridge between HDT's guides and Firestone's comps** — how many of the 24 Firestone comps match the ~23 guides
  has never been measured (`bridge:` line in the log).
- **Cover during a choice** — panel and frames hide while a discover, trinket or Dark Gift is open.
- **Resize handle** — pointing under HDT's real overlay, dotted frame, reset.
- **Hero-pick comp line, odds and per-opponent summary** (phases 5 and 6 of the [plan](docs/plans/2026-09-26-parite-tier7-plan.md)).
- **Skip combat** when relaunched by Battle.net.

## Next

- **Packaged releases.** GitHub only hosts HDT up to 1.55.6; newer versions come through HDT's own updater. A release
  must be built against the HDT people run, so it is done by hand for now, then attached to a GitHub release.
- **Firestone card stats for discovers with no matching comp** — a new download that the maintainer still has to approve.
- **Simulation of HDT's overlay layer** (clicks through the transparent overlay, hover probed at 60 Hz) and a scenario
  mode with an injected mouse. Today the simulation cannot show a defect that lives in that layer.
- **HSReplay manual import** is implemented but has never been used: the folder is empty and only Firestone's comps run.

## Open questions

- Keep the "comp ≈" line under each hero in the hero-pick screen? Its samples are thin (17 games at the median).
- Keep the per-opponent summary if it duplicates the game's own interface?

## Not planned

| Idea | Why not |
|---|---|
| Timewarped notification | The mechanic is absent from the games of season 14 (checked on the logs). |
| Quest statistics | Firestone's file is empty. |
| "Next opponent" marker | The game already shows it. |
| Combat history tab | Built, then removed: unused. |
| Reading the game's memory | HDT does it; the plugin only uses what HDT exposes. |
| Getting around HSReplay's anti-bot protection | The plugin makes no HSReplay request. |
