import os
import sys
import tempfile

os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
os.environ.setdefault("PYGAME_HIDE_SUPPORT_PROMPT", "1")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, ROOT)
os.environ["ASH_HOLLOW_DIR"] = tempfile.mkdtemp(prefix="ash_test_")

import pygame  # noqa: E402

from main import Game  # noqa: E402


def make_game():
    game = Game()
    game.fade = 0.0
    return game


class FK:
    def __init__(self, keys=()):
        self.keys = set(keys)

    def __getitem__(self, k):
        return k in self.keys


def step(game, frames, keys=()):
    fk = FK(keys)
    for _ in range(frames):
        game.update(1 / 60, fk)
        game.draw()


def arena(game):
    game.state = "play"
    game.room = "arena"
    game.hitstop = 0.0
    game.input_buffer = {"action": None, "timer": 0.0}
    game.player.respawn()
    game.boss.reset()
    game.boss.aggro = True
    game.boss.aggro_banner = 0.0
