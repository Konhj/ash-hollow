import pygame

from common import make_game, step, arena, FK
from settings import *
from boss import DURATION

game = make_game()
game.state = "play"
game.room = "arena"
p, b = game.player, game.boss


def setup(fn):
    arena(game)
    game.player.facing = pygame.Vector2(1, 0)
    fn()
    game.draw()


print("== attack damage (broadsword 35) ==")
setup(lambda: (setattr(p, "x", b.rect.centerx - 60), setattr(p, "y", b.rect.centery), p.sync_rect()))
step(game, 12, (pygame.K_j,))
assert b.hp == BOSS_MAX_HP - 35, b.hp

print("== parry via guard start ==")
arena(game)
p.equipment["offhand"] = "small_round_shield"
p.x, p.y = b.rect.centerx - 70, b.rect.centery
p.sync_rect()
b.dir = pygame.Vector2(-1, 0)
p.start_guard()
b.state = "attack"
b.attack_kind = "slash"
b.state_timer = DURATION["slash"]
b.hit_landed = False
step(game, 4, (pygame.K_l,))
assert b.state == "staggered", b.state
assert p.hp == p.max_hp()

print("== riposte ==")
arena(game)
p.x, p.y = b.rect.centerx - 60, b.rect.centery
p.sync_rect()
b.stagger(2.5)
step(game, 40, (pygame.K_j,))
assert b.hp == BOSS_MAX_HP - 175, b.hp

print("== slam damage ==")
arena(game)
p.x, p.y = b.rect.centerx, b.rect.centery
p.sync_rect()
b.state = "windup"
b.attack_kind = "slam"
b.state_timer = 0.01
step(game, 3)
assert p.hp == PLAYER_MAX_HP - SLAM_DAMAGE, p.hp

print("== roll iframes ==")
arena(game)
p.x, p.y = b.rect.centerx, b.rect.centery
p.sync_rect()
p.state = "roll"
p.state_timer = ROLL_TIME
p.invuln = ROLL_TIME
b.state = "windup"
b.attack_kind = "slam"
b.state_timer = 0.01
step(game, 3)
assert p.hp == PLAYER_MAX_HP

print("== estus ==")
arena(game)
p.hp = 50
p.state = "idle"
p.estus = 1
step(game, 50, (pygame.K_f,))
assert p.hp == 90 and p.estus == 0, (p.hp, p.estus)

print("== sprint ==")
arena(game)
p.stamina = PLAYER_MAX_STAMINA
x0 = p.x
step(game, 30, (pygame.K_d,))
walk = p.x - x0
p.x = x0
p.stamina = PLAYER_MAX_STAMINA
step(game, 30, (pygame.K_d, pygame.K_LSHIFT))
sprint = p.x - x0
assert sprint > walk * 1.5, (walk, sprint)
assert p.stamina < PLAYER_MAX_STAMINA - 1

print("== shield block reduces slam ==")
arena(game)
p.equipment["offhand"] = "small_round_shield"
p.x, p.y = b.rect.centerx, b.rect.centery
p.sync_rect()
step(game, 2, (pygame.K_l,))
step(game, 30, (pygame.K_l,))
b.state = "windup"
b.attack_kind = "slam"
b.state_timer = 0.01
hp0 = p.hp
step(game, 3, (pygame.K_l,))
assert hp0 - p.hp == max(1, int(SLAM_DAMAGE * 0.5)), hp0 - p.hp

print("== hitstun + roll cancel + hitstop + input buffer ==")
arena(game)
p.equipment["offhand"] = None
p.x, p.y = b.rect.centerx - 70, b.rect.centery
p.sync_rect()
b.dir = pygame.Vector2(-1, 0)
b.state = "attack"
b.attack_kind = "slash"
b.state_timer = DURATION["slash"]
b.hit_landed = False
step(game, 3)
assert p.state == "hitstun", p.state
step(game, 20)
assert p.state == "idle"

p.state = "attack"
p.attack_duration = 0.36
p.state_timer = 0.20
p.attack_is_offhand = False
p.roll_cooldown = 0.0
p.stamina = 100
step(game, 1, (pygame.K_k,))
assert p.state == "roll"

arena(game)
p.x, p.y = b.rect.centerx + 100, b.rect.centery
p.sync_rect()
b.state = "windup"
b.attack_kind = "blast"
b.state_timer = 0.01
step(game, 3)
assert game.hitstop > 0
x0 = p.x
step(game, 3, (pygame.K_d,))
assert p.x == x0
step(game, 40, (pygame.K_d,))
assert p.x > x0

arena(game)
p.state = "attack"
p.attack_duration = 0.36
p.state_timer = 0.06
p.attack_is_offhand = False
step(game, 1, (pygame.K_j,))
assert game.input_buffer["action"] == "attack"
step(game, 6)
assert p.state == "attack"
step(game, 25)
assert p.state == "idle"

print("== phase 2 ==")
arena(game)
b.hp = int(b.max_hp * PHASE2_HP_FRACTION) + 30
p.x, p.y = b.rect.centerx - 60, b.rect.centery
p.sync_rect()
step(game, 12, (pygame.K_j,))
assert b.phase == 2
step(game, 60)
assert b.blast_landed is True

print("== victory ==")
arena(game)
b.hp = 10
p.x, p.y = b.rect.centerx - 60, b.rect.centery
p.sync_rect()
for _ in range(150):
    game.update(1 / 60, FK((pygame.K_j,)))
    game.draw()
    if game.state == "victory":
        break
assert game.state == "victory"

print("ALL COMBAT TESTS PASSED")
