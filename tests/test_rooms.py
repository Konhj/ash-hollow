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
assert game.room_def["bonfire"] == (640, 430)
game.draw()

print("== hub terrain blocks edges ==")
p.x, p.y = 600, 300
p.sync_rect()
for _ in range(90):
    game.update(1 / 60, FK((pygame.K_w,)))
    game.draw()
assert p.y == 39.0, p.y

print("== door to outpost (big room) ==")
p.x, p.y = 60, 360
p.sync_rect()
step(game, 3, (pygame.K_e,))
assert game.room == "outpost", game.room
assert p.x > 2000
game.draw()

print("== camera follows in big room ==")
p.x, p.y = 2200, 720
p.sync_rect()
game.snap_camera()
cam0 = list(game.cam)
for _ in range(150):
    game.update(1 / 60, FK((pygame.K_a,)))
    game.draw()
assert game.cam[0] < cam0[0], (cam0, game.cam)
print("cam moved:", cam0[0], "->", game.cam[0])

print("== terrain blocks player ==")
p.x, p.y = 540, 720
p.sync_rect()
for _ in range(80):
    game.update(1 / 60, FK((pygame.K_d,)))
    game.draw()
assert p.x == 585.0, p.x
print("blocked at pillar edge:", p.x)

print("== door back to hub ==")
p.x, p.y = 2500, 720
p.sync_rect()
step(game, 3, (pygame.K_e,))
assert game.room == "hub", game.room

print("== fog gate to arena ==")
step(game, 30)  # 等连接冷却
p.x, p.y = 1240, 360
p.sync_rect()
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

print("== sealed fog while boss alive ==")
p.state = "idle"
game.input_buffer = {"action": None, "timer": 0.0}
p.x, p.y = 60, 360
p.sync_rect()
step(game, 3, (pygame.K_e,))
assert game.room == "arena"

print("== fog opens after boss dies ==")
step(game, 30)  # 等连接冷却
b.state = "dying"
b.state_timer = 1.6
b.dead = True
step(game, 3, (pygame.K_e,))
assert game.room == "hub"
assert b.hp == b.max_hp and b.aggro is False

print("== death respawns in hub ==")
game.enter_room("arena", (150, 360))
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
game.enter_room("outpost", (2440, 720))
game.save_current()
import config

data = config.load_save()
assert data["room"] == "outpost"

print("== continue restores room ==")
game.enter_room("hub", (640, 560))
game.load_and_continue()
assert game.room == "outpost"
assert game.room_def["size"] == (2560, 1440)

print("ALL ROOM TESTS PASSED")
