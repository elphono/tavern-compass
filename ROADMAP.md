# Roadmap

State of the project on 2026-10-06. Detailed history, decision by decision, is in [`docs/journal/`](docs/journal/)
(in French); the specification and the plan the plugin follows are in [`docs/plans/`](docs/plans/).

| Horizon | What |
|---|---|
| **Shipped** | Hero-pick stats · MMR of the opponents · Compositions panel (HDT's comp guides as targets) · tavern frames · choice labels · lobby tribes filter · `−` / `+` that resize the panel for N comps · power inset (you and your opponent) · movable and resizable panels · simulation harness |
| **To verify in a live game** | Almost everything of the last two weeks: see below |
| **Next** | Packaged releases · Firestone card stats for discovers with no comp · simulation of HDT's overlay layer |
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
- **Cover during a choice** — panel, inset and frames hide while a discover, trinket or Dark Gift is open.
- **Resize handle** — pointing under HDT's real overlay, dotted frame, reset.
- **Hero-pick comp line, odds and per-opponent summary** (phases 5 and 6 of the [plan](docs/plans/2026-09-26-parite-tier7-plan.md)).
- **Skip combat** when relaunched by Battle.net.

## Open questions

- **Board only, or board and hand?** The N comps are ranked on what you hold on the board **and** in your hand: the hand
  is what you will play next turn. Ranking on the board alone is a one-line change.
- Should a panel resized by `−` / `+` keep its size from one game to the next? Today it goes back to its default.
- Keep the "comp ≈" line under each hero in the hero-pick screen? Its samples are thin (17 games at the median).
- Keep the per-opponent summary if it duplicates the game's own interface?
- Duos have not been thought through for the opponent's gauge.

## Next

- **Packaged releases.** GitHub only hosts HDT up to 1.55.6; newer versions come through HDT's own updater. A release
  must be built against the HDT people run, so it is done by hand for now, then attached to a GitHub release.
- **Firestone card stats for discovers with no matching comp** — a new download that the maintainer still has to approve.
- **Simulation of HDT's overlay layer** (clicks through the transparent overlay, hover probed at 60 Hz) and a scenario
  mode with an injected mouse. Today the simulation cannot show a defect that lives in that layer.
- **HSReplay manual import** is implemented but has never been used: the folder is empty and only Firestone's comps run.

## Not planned

| Idea | Why not |
|---|---|
| Timewarped notification | The mechanic is absent from the games of season 14 (checked on the logs). |
| Quest statistics | Firestone's file is empty. |
| "Next opponent" marker on the leaderboard | The game already shows it. |
| Combat history tab | Built, then removed: unused. |
| Reading the game's memory | HDT does it; the plugin only uses what HDT exposes. |
| Getting around HSReplay's anti-bot protection | The plugin makes no HSReplay request. |
