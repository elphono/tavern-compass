#!/usr/bin/env python3
"""Tests of tools/nodance-observe.py on SYNTHETIC Power logs (invented entities and names, no real game data).

    python3 tools/test_nodance_observe.py
"""
import contextlib
import importlib.util
import io
import os
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True   # importing the tool must not leave a __pycache__ in tools/
HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location('nodance_observe', os.path.join(HERE, 'nodance-observe.py'))
observe = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(observe)

R = 'GameState.DebugPrintPower() - '
P = 'PowerTaskList.DebugPrintPower() - '


def ent(name, eid, pos, card):
    return f'[entityName={name} id={eid} zone=PLAY zonePos={pos} cardId={card} player=5]'


SETUP = [
    ('10:00:00.0', R + 'GameState.DebugPrintGame() - PlayerID=5, PlayerName=Testplayer'),
    ('10:00:00.0', R + 'FULL_ENTITY - Creating ID=51 CardID=Y'),
    ('10:00:00.0', R + '    tag=CONTROLLER value=5'),
    ('10:00:00.0', R + '    tag=ZONE value=PLAY'),
    ('10:00:00.0', R + 'FULL_ENTITY - Creating ID=53 CardID=W'),
    ('10:00:00.0', R + '    tag=CONTROLLER value=5'),
    ('10:00:00.0', R + '    tag=ZONE value=PLAY'),
    ('10:00:00.0', R + 'TAG_CHANGE Entity=GameEntity tag=STEP value=MAIN_ACTION'),
    ('10:00:00.0', R + 'TAG_CHANGE Entity=GameEntity tag=BACON_IN_COMBAT_PHASE value=0'),
]
TRIGGER = 'BLOCK_START BlockType=TRIGGER Entity=' + ent('Zap', 50, 2, 'X') + ' EffectCardId=x EffectIndex=0 Target=0 SubOption=-1'
MOVE = 'BLOCK_START BlockType=MOVE_MINION Entity=' + ent('Mover', 52, 1, 'Z') + ' EffectCardId=x EffectIndex=0 Target=0 SubOption=-1'
EXIT = '    TAG_CHANGE Entity=' + ent('Victim', 51, 3, 'Y') + ' tag=ZONE value=GRAVEYARD'
SHIFT = '    TAG_CHANGE Entity=' + ent('Other', 53, 4, 'W') + ' tag=ZONE_POSITION value=3'
SEND = 'GameState.SendOption() - selectedOption=3 selectedSubOption=-1 selectedTarget=0 selectedPosition=2'


def scenario(effects, trigger_played_at):
    """A shop effect received at 10:00:01 (its tag changes: effects), a move sent at 10:00:02 and answered."""
    lines = list(SETUP)
    lines.append(('10:00:01.0', R + TRIGGER))
    lines += [('10:00:01.0', R + e) for e in effects]
    lines.append(('10:00:01.0', R + 'BLOCK_END'))
    lines.append(('10:00:02.0', SEND))
    lines.append(('10:00:02.3', R + MOVE))
    lines.append(('10:00:02.3', R + 'BLOCK_END'))
    lines.append((trigger_played_at, P + TRIGGER))
    lines.append(('10:00:03.1', P + MOVE))
    lines.sort(key=lambda x: x[0])
    return '\n'.join(f'D {t}000000 {text}' for t, text in lines) + '\n'


class ObserveTests(unittest.TestCase):
    def run_tool(self, text, *times):
        with tempfile.TemporaryDirectory() as d:
            path = os.path.join(d, 'Power.log')
            with open(path, 'w', encoding='utf-8') as f:
                f.write(text)
            args = [path]
            for t in times:
                args += ['--at', t]
            out = io.StringIO()
            with contextlib.redirect_stdout(out):
                code = observe.main(args)
        self.assertEqual(code, 0)
        return out.getvalue()

    def test_exit_received_not_played_when_the_move_is_sent_is_d_a(self):
        out = self.run_tool(scenario([EXIT], trigger_played_at='10:00:03.0'), '10:00:02')
        self.assertIn('verdict: D-a at 10:00:02.000: 1 board entry/exit received, not yet played (entities 51', out)
        self.assertNotIn('D-b', out.split('verdict', 1)[1])
        self.assertIn('option sent, position=2', out)
        self.assertIn('server: MOVE_MINION of entity 52 (Mover)', out)

    def test_row_places_received_before_the_move_and_played_after_is_d_b(self):
        out = self.run_tool(scenario([SHIFT], trigger_played_at='10:00:03.0'), '10:00:03')
        self.assertIn('verdict: D-b at 10:00:02.000: 1 block(s) with row places received before the send, played 1.00 s after it', out)
        self.assertNotIn('D-a at', out)

    def test_the_research_witness_has_both(self):
        out = self.run_tool(scenario([EXIT, SHIFT], trigger_played_at='10:00:03.0'), '10:00:02')
        self.assertIn('D-a at 10:00:02.000', out)
        self.assertIn('D-b at 10:00:02.000', out)

    def test_an_effect_played_before_the_move_opens_no_window(self):
        out = self.run_tool(scenario([EXIT, SHIFT], trigger_played_at='10:00:01.5'), '10:00:02')
        self.assertIn('verdict: neither D-a nor D-b: cause not established', out)

    def test_no_placement_near_the_time_says_so(self):
        out = self.run_tool(scenario([EXIT], trigger_played_at='10:00:03.0'), '10:00:30')
        self.assertIn('no placement or move sent within +/-5 s; neither D-a nor D-b: cause not established', out)

    def test_options_sent_in_combat_are_left_out(self):
        text = scenario([EXIT], trigger_played_at='10:00:03.0').replace(
            'tag=BACON_IN_COMBAT_PHASE value=0', 'tag=BACON_IN_COMBAT_PHASE value=1')
        out = self.run_tool(text, '10:00:02')
        self.assertIn('0 options sent in the shop', out)
        self.assertIn('no placement or move sent', out)

    def test_a_log_that_starts_in_the_middle_of_a_game_is_read_as_shop(self):
        text = '\n'.join(line for line in scenario([EXIT], trigger_played_at='10:00:03.0').split('\n')
                         if 'GameEntity tag=' not in line)
        out = self.run_tool(text, '10:00:02')
        self.assertIn('D-a at 10:00:02.000', out)

    def test_a_send_in_another_step_is_left_out(self):
        text = scenario([EXIT], trigger_played_at='10:00:03.0').replace('value=MAIN_ACTION', 'value=MAIN_END')
        out = self.run_tool(text, '10:00:02')
        self.assertIn('no placement or move sent', out)

    def test_each_game_of_a_log_has_its_own_player(self):
        # Game 2: the player is 7; the minion that leaves belongs to player 5, the player of game 1. A single vote over
        # the whole log would take 5 for both games and report a D-a that is not the player's.
        game1 = scenario([EXIT], trigger_played_at='10:00:03.0')
        game2 = scenario([EXIT], trigger_played_at='10:00:03.0').replace('D 10:00:', 'D 10:01:')
        game2 = game2.replace(MOVE, MOVE.replace('player=5', 'player=7'))
        out = self.run_tool(game1 + f'D 10:00:59.0000000 {R}CREATE_GAME\n' + game2, '10:00:02', '10:01:02')
        first, second = out.split('\n10:01:02.000')
        self.assertIn('2 game(s)', first)
        self.assertIn('player 5', first)
        self.assertIn('D-a at 10:00:02.000', first)
        self.assertIn('player 7', second)
        self.assertIn('neither D-a nor D-b', second)

    def test_several_times_and_never_a_player_name(self):
        out = self.run_tool(scenario([EXIT], trigger_played_at='10:00:03.0'), '10:00:02', '10:00:30')
        self.assertEqual(2, out.count('verdict'))
        self.assertNotIn('Testplayer', out)

    def test_the_clock_goes_on_past_midnight(self):
        # The exit is received at 23:59:58, the move sent at 23:59:59, the exit played at 00:00:00.5.
        text = scenario([EXIT], trigger_played_at='10:00:03.0').replace('D 10:00:0', 'D 23:59:5')
        for old, new in (('D 23:59:51', 'D 23:59:58'), ('D 23:59:52.0', 'D 23:59:59.0'), ('D 23:59:52.3', 'D 23:59:59.3'),
                         ('D 23:59:53.0', 'D 00:00:00.5'), ('D 23:59:53.1', 'D 00:00:00.6')):
            text = text.replace(old, new)
        out = self.run_tool(text, '23:59:59')
        self.assertIn('D-a at 23:59:59.000', out)


if __name__ == '__main__':
    unittest.main()
