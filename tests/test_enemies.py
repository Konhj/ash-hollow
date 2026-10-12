import math

import pygame

from common import make_game, step, FK

game = make_game()
game.fade = 0.0
game.state = "play"
p = game.player


def outpost():
    game.enter_room("outpost", (2440, 720))
    game.state = "play"
    game.conn_cooldown = 0.0


print("== enemies spawn in outpost ==")
outpost()
assert len(game.enemies) == 3, len(game.enemies)
game.draw()
print("enemies:", [e.eid for e in game.enemies])

print("== patrol, no aggro far away ==")
for _ in range(30):
    game.update(1 / 60, FK())
    game.draw()
assert all(not e.aggro for e in game.enemies)
print("no aggro")

print("== approach triggers aggro + chase ==")
soldier = game.enemies[0]
p.x, p.y = soldier.x - 100, soldier.y
p.sync_rect()
for _ in range(60):
    game.update(1 / 60, FK())
    game.draw()
assert soldier.aggro
print("soldier aggro:", soldier.aggro)

print("== melee enemy damages player ==")
p.hp = p.max_hp()
soldier.state = "windup"
soldier.attack_kind = "slash"
soldier.state_timer = 0.01
soldier.hit_landed = False
p.x, p.y = soldier.x - 40, soldier.y
p.sync_rect()
for _ in range(8):
    game.update(1 / 60, FK())
    game.draw()
assert p.hp < p.max_hp(), p.hp
print("player hp:", p.hp)

print("== player kills enemy, souls reward ==")
soldier.take_damage(1000)
for _ in range(40):
    game.update(1 / 60, FK())
    game.draw()
assert soldier.dead
assert soldier not in game.enemies
assert game.player.souls == 50, game.player.souls
assert ("outpost", 0) in game.killed_enemies
print("souls:", game.player.souls)

print("== archer projectile damages player ==")
archer = [e for e in game.enemies if e.eid == "hollow_archer"][0]
p.respawn()
p.x, p.y = archer.x + 60, archer.y
p.sync_rect()
archer.aggro = True
archer.state = "windup"
archer.attack_kind = "shot"
archer.state_timer = 0.01
archer.shot_fired = False
hp0 = p.hp
for _ in range(40):
    game.update(1 / 60, FK())
    game.draw()
assert p.hp < hp0, (hp0, p.hp)
print("projectile hit:", hp0 - p.hp)

print("== leash: disengage and heal ==")
raider = [e for e in game.enemies if e.eid == "hollow_raider"][0]
raider.hp = 10
raider.aggro = True
p.x, p.y = raider.x - 700, raider.y
p.sync_rect()
for _ in range(330):
    game.update(1 / 60, FK())
    game.draw()
assert not raider.aggro
assert raider.hp == raider.data["max_hp"], raider.hp
print("disengaged and healed")

print("== rest respawns killed enemies ==")
game.enter_room("hub", (640, 560))
game.conn_cooldown = 0.0
p.x, p.y = game.room_def["bonfire"]
p.sync_rect()
p.hp = p.max_hp()
step(game, 3, (pygame.K_e,))
assert ("outpost", 0) not in game.killed_enemies
outpost()
assert len(game.enemies) == 3
print("enemies respawned")

print("== save/load persists kills ==")
e0 = game.enemies[0]
e0.take_damage(1000)
for _ in range(40):
    game.update(1 / 60, FK())
    game.draw()
assert ("outpost", 0) in game.killed_enemies
game.save_current()
game.load_and_continue()
assert ("outpost", 0) in game.killed_enemies
assert len(game.enemies) == 2
print("kills persisted")

print("== player attack hits enemy ==")
p = game.player
e = game.enemies[0]
p.respawn()
p.x, p.y = e.x - 50, e.y
p.sync_rect()
p.facing = pygame.Vector2(1, 0)
hp0 = e.hp
step(game, 12, (pygame.K_j,))
assert e.hp < hp0, (hp0, e.hp)
print("enemy hp:", hp0, "->", e.hp)

print("ALL ENEMY TESTS PASSED")
