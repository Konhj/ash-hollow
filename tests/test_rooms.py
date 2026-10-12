import pygame

from common import make_game, step, FK
from settings import *

game = make_game()
game.fade = 0.0
game.state = "play"
p, b = game.player, game.boss

print("== new game starts in hub ==")
game.start_new_game()
p, b = game.player, game.boss
assert game.room == "hub"
assert game.hub_banner > 0
assert game.bonfire.pos == (640, 430)
game.draw()

print("== fog gate: blocked, interact key enters arena ==")
p.x, p.y = 1170, 360
p.sync_rect()
step(game, 2, (pygame.K_d,))
assert game.room == "hub"
step(game, 3, (pygame.K_e,))
assert game.room == "arena"
assert p.x < 300

print("== boss fight works in arena ==")
b.aggro = True
b.aggro_banner = 0.0
p.x, p.y = b.rect.centerx - 60, b.rect.centery
p.sync_rect()
p.facing = pygame.Vector2(1, 0)
hp0 = b.hp
step(game, 12, (pygame.K_j,))
assert b.hp < hp0

print("== sealed gate while boss alive ==")
p.state = "idle"
game.input_buffer = {"action": None, "timer": 0.0}
p.x, p.y = 110, 360
p.sync_rect()
step(game, 2, (pygame.K_a,))
step(game, 3, (pygame.K_e,))
assert game.room == "arena"

print("== gate opens after boss dies ==")
p.state = "idle"
game.input_buffer = {"action": None, "timer": 0.0}
b.state = "dying"
b.state_timer = 1.6
b.dead = True
step(game, 3, (pygame.K_e,))
assert game.room == "hub"
assert b.hp == b.max_hp and b.aggro is False

print("== boss not hittable from hub ==")
p.state = "idle"
b.hp = 500
p.x, p.y = b.rect.centerx - 60, b.rect.centery
p.sync_rect()
p.facing = pygame.Vector2(1, 0)
step(game, 15, (pygame.K_j,))
assert b.hp == 500

print("== death in arena respawns in hub ==")
game.room = "arena"
b.reset()
b.aggro = True
b.aggro_banner = 0.0
p.respawn()
p.hp = 1
p.x, p.y = b.rect.centerx, b.rect.centery
p.sync_rect()
b.state = "windup"
b.attack_kind = "slam"
b.state_timer = 0.01
step(game, 5)
assert game.state == "died"
for _ in range(200):
    game.update(1 / 60, FK())
    game.draw()
assert game.state == "play" and game.room == "hub"

print("== save stores room ==")
game.enter_arena()
game.save_current()
import config

data = config.load_save()
assert data["room"] == "arena"

print("== continue restores room ==")
game.exit_to_hub()
game.load_and_continue()
assert game.room == "arena"

print("ALL ROOM TESTS PASSED")
