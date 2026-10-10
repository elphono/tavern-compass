#!/usr/bin/env python3
"""Read-only: what was happening around the times a minion "dance" was seen, from Hearthstone's own logs.

    python3 tools/nodance-observe.py --at 21:14:05 [--at 21:20:31 ...] [--window 5] [--zone Zone.log] \\
        Power_old.log [Power.log]

For each time given (the player's local clock, as in the logs), it lists the player's actions within +/- window
seconds and says whether, when a placement or move was sent, one of the two conditions of the anti-dance design was
open (docs/journal/2026-10-10-anti-danse.md):

  D-a  a board entry or exit of the player's row was received from the server but its task list had not started
       playing yet (the card still shown, or not yet shown) when the option was sent;
  D-b  a block received before the option, carrying a position change on the player's row, was played after it.

Otherwise it says "neither D-a nor D-b: cause not established". Pass the session's Power logs in order (Power_old.log,
then Power.log). Reception is read from GameState.DebugPrintPower lines, playing from PowerTaskList.DebugPrintPower
lines, matched by identical block text and order of occurrence. The player is the one whose minions are moved
(MOVE_MINION), unless --player is given. Options sent in combat (BACON_IN_COMBAT_PHASE=1) or outside the main step
are left out; a log that starts in the middle of a game (a rotated Power_old.log) is read as shop until its first
STEP tag. Nothing is written; player names are never printed.
"""
import argparse
import collections
import re
import sys

RECEIVED = 'GameState.DebugPrintPower() - '
PLAYED = 'PowerTaskList.DebugPrintPower() - '
SENT = 'GameState.SendOption()'
ENTITY_TAG = re.compile(r'Entity=\[(?:entityName=(?P<name>.*?) )?id=(?P<id>\d+).*?\] tag=(?P<tag>\S+) value=(?P<value>\S+)')
SOURCE = re.compile(r'BlockType=(?P<type>\S+) Entity=\[(?:entityName=(?P<name>.*?) )?id=(?P<id>\d+).*?player=(?P<player>\d+)\]')
MAX_PLAY_DELAY = 10.0


def clock(text):
    h, m, s = text.split(':')
    return int(h) * 3600 + int(m) * 60 + float(s)


def block_key(body):
    """A block's text without what changes between reception and playing (names, zones, positions)."""
    body = re.sub(r'entityName=.*? id=', 'id=', body)
    return re.sub(r' zone=\S+ zonePos=\d+', '', body).strip()


def read_lines(paths):
    """(seconds, text) of every timestamped line, the clock made monotonic across midnight."""
    out, day, last = [], 0, None
    for path in paths:
        with open(path, encoding='utf-8', errors='replace') as f:
            for raw in f:
                parts = raw.split(' ', 2)
                if len(parts) < 3 or not re.match(r'\d\d:\d\d:\d\d', parts[1]):
                    continue
                t = clock(parts[1])
                if last is not None and t + day < last - 12 * 3600:
                    day += 24 * 3600
                last = t + day
                out.append((t + day, parts[2].rstrip('\n')))
    return out


def split_games(lines):
    """One list of lines per game: a log holds several games, and the player's id changes from one to the next."""
    games = [[]]
    for line in lines:
        if line[1].startswith(RECEIVED) and line[1][len(RECEIVED):].strip().startswith('CREATE_GAME') and games[-1]:
            games.append([])
        games[-1].append(line)
    return [g for g in games if g]


class Session:
    def __init__(self, lines, player=None):
        self.sends = []          # (time, position)
        self.answers = []        # (time, block type, source id, source name) of the player's own blocks
        self.membership = []     # (received, played, entity id): the player's row gains or loses a minion
        self.positions = []      # (received, played): a block moving places on the player's row
        self.games = []          # (first time, last time, player id or None)
        for game in split_games(lines):
            played = collections.defaultdict(list)
            for t, text in game:
                if text.startswith(PLAYED):
                    body = text[len(PLAYED):].strip()
                    if body.startswith('BLOCK_START'):
                        played[block_key(body)].append(t)
            self.player = player if player is not None else self._guess_player(game)
            self.games.append((game[0][0], game[-1][0], self.player))
            if self.player is not None:
                self._read(game, played)

    @staticmethod
    def _guess_player(lines):
        """The player who moves minions, or whose PLAY block answers a placement sent just before."""
        votes = collections.Counter()
        placed = None
        for t, text in lines:
            if text.startswith(SENT):
                m = re.search(r'selectedPosition=(\d+)', text)
                placed = t if m and int(m.group(1)) > 0 else None
                continue
            if not text.startswith(RECEIVED) or not text[len(RECEIVED):].strip().startswith('BLOCK_START'):
                continue
            m = SOURCE.search(text)
            if m and (m.group('type') == 'MOVE_MINION' or (m.group('type') == 'PLAY' and placed is not None and t - placed <= 2)):
                votes[int(m.group('player'))] += 1
            placed = None
        return votes.most_common(1)[0][0] if votes else None

    def player_at(self, at):
        for first, last, player in self.games:
            if first <= at <= last:
                return player
        return None

    def _read(self, lines, played):
        zone, ctrl, kind = {}, {}, {}
        seen = collections.Counter()
        stack = []               # [received, played, moves places on the player's row]
        current = None
        step = combat = None
        for t, text in lines:
            if text.startswith(SENT):
                m = re.search(r'selectedPosition=(-?\d+)', text)
                if m and step in (None, 'MAIN_ACTION') and combat != '1':
                    self.sends.append((t, int(m.group(1))))
                continue
            if not text.startswith(RECEIVED):
                continue
            body = text[len(RECEIVED):].strip()
            if body.startswith('BLOCK_START'):
                key = block_key(body)
                n = seen[key]
                seen[key] += 1
                p = played[key][n] if n < len(played[key]) else None
                if p is not None and not 0 <= p - t < MAX_PLAY_DELAY:
                    p = None
                block = [t, p, False]
                stack.append(block)
                m = SOURCE.search(body)
                if m and int(m.group('player')) == self.player and m.group('type') in ('PLAY', 'MOVE_MINION'):
                    self.answers.append((t, m.group('type'), int(m.group('id')), m.group('name') or '?'))
                current = None
                continue
            if body.startswith('BLOCK_END'):
                if stack:
                    block = stack.pop()
                    if block[2] and block[1] is not None:
                        self.positions.append((block[0], block[1]))
                current = None
                continue
            m = re.match(r'(FULL_ENTITY|SHOW_ENTITY|CHANGE_ENTITY).*?(?:ID=|id=)(\d+)', body)
            if m:
                current = int(m.group(2))
                continue
            if body.startswith('tag=') and current is not None:
                tag, value = body[4:].split(' value=', 1)
                self._tag(t, current, tag, value.strip(), zone, ctrl, kind, stack, step)
                continue
            current = None
            if 'GameEntity tag=STEP ' in body:
                step = body.split('value=')[1].split()[0]
                continue
            if 'GameEntity tag=BACON_IN_COMBAT_PHASE ' in body:
                combat = body.split('value=')[1].split()[0]
                continue
            m = ENTITY_TAG.search(body)
            if m:
                self._tag(t, int(m.group('id')), m.group('tag'), m.group('value'), zone, ctrl, kind, stack, step)

    def _tag(self, t, entity, tag, value, zone, ctrl, kind, stack, step):
        before = zone.get(entity) == 'PLAY' and ctrl.get(entity) == self.player
        if tag == 'ZONE':
            zone[entity] = value
        elif tag == 'CONTROLLER':
            ctrl[entity] = int(value)
        elif tag == 'CARDTYPE':
            kind[entity] = value
        after = zone.get(entity) == 'PLAY' and ctrl.get(entity) == self.player
        minion = kind.get(entity, 'MINION') == 'MINION'
        if before != after and minion and step in (None, 'MAIN_ACTION') and stack and stack[-1][1] is not None:
            self.membership.append((t, stack[-1][1], entity))
        if tag == 'ZONE_POSITION' and after and minion and stack:
            stack[-1][2] = True

    def report(self, at, window):
        lines = []
        nearby = [s for s in self.sends if abs(s[0] - at) <= window]
        answers = [a for a in self.answers if abs(a[0] - at) <= window]
        events = [(t, f'option sent, position={position}' + ('' if position > 0 else ' (no place: buy, sell, power...)'))
                  for t, position in nearby]
        events += [(t, f'server: {kind} of entity {entity} ({name})') for t, kind, entity, name in answers]
        lines.extend(f'  {hms(t)}  {text}' for t, text in sorted(events, key=lambda e: e[0]))
        placed = [(t, p) for t, p in nearby if p > 0]
        if not placed:
            lines.append(f'  verdict: no placement or move sent within +/-{window:g} s; neither D-a nor D-b: cause not established')
            return lines
        found = []
        for t, _ in placed:
            exits = [m for m in self.membership if m[0] < t < m[1]]
            late = [b for b in self.positions if b[0] < t < b[1]]
            if exits:
                found.append(f'D-a at {hms(t)}: {len(exits)} board entry/exit received, not yet played '
                             f'(entities {", ".join(str(m[2]) for m in exits)}; played {max(m[1] for m in exits) - t:.2f} s after the send)')
            if late:
                found.append(f'D-b at {hms(t)}: {len(late)} block(s) with row places received before the send, '
                             f'played {max(b[1] for b in late) - t:.2f} s after it')
        if found:
            lines.extend('  verdict: ' + f for f in found)
        else:
            lines.append('  verdict: neither D-a nor D-b: cause not established')
        return lines


def hms(seconds):
    seconds %= 24 * 3600
    h, rest = divmod(seconds, 3600)
    m, s = divmod(rest, 60)
    return f'{int(h):02d}:{int(m):02d}:{s:06.3f}'


def zone_lines(path, at, window):
    """The Zone log's position and zone moves near the time, as they are (they name cards, never players)."""
    out = []
    for t, text in read_lines([path]):
        if abs(t - at) <= window and (' pos from ' in text or ' zone from ' in text):
            out.append(f'  {hms(t)}  {text.strip()}')
    return out


def nearest(at, lines):
    """The noted clock time on the session's monotonic clock: the occurrence closest to the logs."""
    if not lines:
        return at
    first, last = lines[0][0], lines[-1][0]
    candidates = [at + day * 24 * 3600 for day in range(int(last // (24 * 3600)) + 2)]
    return min(candidates, key=lambda c: 0 if first <= c <= last else min(abs(c - first), abs(c - last)))


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    parser.add_argument('power', nargs='+', help="the session's Power logs, in order (Power_old.log, then Power.log)")
    parser.add_argument('--at', action='append', required=True, metavar='HH:MM:SS', help='a time a dance was seen (repeatable)')
    parser.add_argument('--window', type=float, default=5.0, help='seconds before and after (default 5)')
    parser.add_argument('--zone', help='the Zone.log of the same session, if [Zone] logging was on')
    parser.add_argument('--player', type=int, help="the player's id, if it cannot be guessed from the moves")
    args = parser.parse_args(argv)
    try:
        times = [clock(a) for a in args.at]
    except ValueError:
        parser.error('--at takes HH:MM:SS')
    lines = read_lines(args.power)
    session = Session(lines, args.player)
    if all(player is None for _, _, player in session.games):
        print('no move or placement of the player found in these logs: pass --player', file=sys.stderr)
        return 2
    print(f'{len(lines)} log lines, {len(session.games)} game(s), {len(session.sends)} options sent in the shop')
    for at in times:
        t = nearest(at, lines)
        player = session.player_at(t)
        print(f'\n{hms(t)} (+/-{args.window:g} s), player {player if player is not None else "unknown"}')
        for line in session.report(t, args.window):
            print(line)
        if args.zone:
            for line in zone_lines(args.zone, t, args.window) or ['  Zone.log: no position change in the window']:
                print(line)
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
