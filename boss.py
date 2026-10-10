import math
import random

import pygame

from settings import *

WINDUP = {"slash": 0.55, "charge": 0.75, "slam": 0.95}
DURATION = {"slash": 0.18, "charge": 0.5, "slam": 0.12}
RECOVER = {"slash": 0.55, "charge": 0.6, "slam": 1.0}


class Boss:
    def __init__(self, x, y):
        self.spawn = (float(x), float(y))
        self.rect = pygame.Rect(0, 0, 64, 110)
        self.reset()

    def reset(self):
        self.hp = BOSS_MAX_HP
        self.max_hp = BOSS_MAX_HP
        self.state = "idle"
        self.state_timer = 0.0
        self.attack_kind = None
        self.aggro = False
        self.aggro_banner = 0.0
        self.attack_cd = 2.4
        self.dir = pygame.Vector2(-1, 0)
        self.charge_dir = pygame.Vector2(-1, 0)
        self.hit_landed = False
        self.slam_landed = True
        self.flash = 0.0
        self.anim_t = 0.0
        self.dead = False
        self.x, self.y = self.spawn
        self.sync_rect()

    def sync_rect(self):
        self.rect.center = (round(self.x), round(self.y))

    def clamp(self):
        self.x = max(48.0, min(WIDTH - 48.0, self.x))
        self.y = max(71.0, min(HEIGHT - 71.0, self.y))
        self.sync_rect()

    def stagger(self, t):
        self.state = "staggered"
        self.state_timer = t
        self.attack_kind = None

    def take_damage(self, dmg):
        if self.dead:
            return
        self.hp = max(0, self.hp - dmg)
        self.flash = 0.12
        if self.hp <= 0:
            self.dead = True
            self.state = "dying"
            self.state_timer = 1.6

    def current_hit_rect(self):
        if self.state != "attack" or self.attack_kind != "slash":
            return None
        hr = pygame.Rect(0, 0, 130, 100)
        hr.center = (self.rect.centerx + self.dir.x * 75, self.rect.centery + self.dir.y * 25)
        return hr

    def choose_attack(self, dist):
        r = random.random()
        if dist > 280:
            kind = "charge" if r < 0.75 else "slam"
        elif dist > 130:
            if r < 0.4:
                kind = "slash"
            elif r < 0.75:
                kind = "slam"
            else:
                kind = "charge"
        else:
            kind = "slash" if r < 0.7 else "slam"
        self.attack_kind = kind
        self.state = "windup"
        self.state_timer = WINDUP[kind]
        if kind == "charge":
            self.charge_dir = self.dir.copy()

    def update(self, dt, player):
        self.flash = max(0.0, self.flash - dt)
        self.anim_t += dt
        if self.state == "dying":
            self.state_timer -= dt
            return
        if not self.aggro:
            to = pygame.Vector2(player.rect.center) - pygame.Vector2(self.rect.center)
            if to.length() < 430:
                self.aggro = True
                self.aggro_banner = 2.4
        else:
            self.aggro_banner = max(0.0, self.aggro_banner - dt)
        if self.aggro_banner > 0:
            return

        to = pygame.Vector2(player.rect.center) - pygame.Vector2(self.rect.center)
        dist = to.length()
        if dist > 0:
            self.dir = to.normalize()

        if self.state == "idle":
            if dist > 130:
                self.x += self.dir.x * BOSS_SPEED
                self.y += self.dir.y * BOSS_SPEED
                self.clamp()
            self.attack_cd -= dt
            if self.attack_cd <= 0:
                self.choose_attack(dist)
        elif self.state == "windup":
            self.state_timer -= dt
            if self.state_timer <= 0:
                self.state = "attack"
                self.state_timer = DURATION[self.attack_kind]
                self.hit_landed = False
                self.slam_landed = False
        elif self.state == "attack":
            self.state_timer -= dt
            if self.attack_kind == "charge":
                self.x += self.charge_dir.x * CHARGE_SPEED
                self.y += self.charge_dir.y * CHARGE_SPEED
                self.clamp()
            if self.state_timer <= 0:
                self.state = "recover"
                self.state_timer = RECOVER[self.attack_kind]
        elif self.state == "recover":
            self.state_timer -= dt
            if self.state_timer <= 0:
                self.state = "idle"
                self.attack_cd = random.uniform(1.4, 2.6)
        elif self.state == "staggered":
            self.state_timer -= dt
            if self.state_timer <= 0:
                self.state = "recover"
                self.state_timer = 0.8

    def draw(self, surf):
        alpha = 255
        if self.state == "dying":
            alpha = int(255 * max(0.0, min(1.0, self.state_timer / 1.6)))
        if alpha <= 0:
            return
        dx, dy = self.dir

        dy_off = 0
        if self.state == "windup" and self.attack_kind == "slam":
            p = 1.0 - self.state_timer / WINDUP["slam"]
            dy_off = -int(150 * p)
        elif self.state == "attack" and self.attack_kind == "slam":
            p = 1.0 - max(0.0, self.state_timer) / DURATION["slam"]
            dy_off = -int(150 * (1.0 - p))

        if dy_off < -20:
            sh = pygame.Surface((90, 20), pygame.SRCALPHA)
            pygame.draw.ellipse(sh, (0, 0, 0, 120), sh.get_rect())
            surf.blit(sh, (self.rect.centerx - 45, self.rect.bottom - 8))

        if self.state == "attack" and self.attack_kind == "slam":
            p = 1.0 - max(0.0, self.state_timer) / DURATION["slam"]
            ring = pygame.Surface((400, 400), pygame.SRCALPHA)
            r = int(20 + 160 * p)
            pygame.draw.circle(ring, (240, 150, 60, int(150 * (1 - p))), (200, 200), r, 6)
            surf.blit(ring, (self.rect.centerx - 200, self.rect.bottom - 200))

        tmp = pygame.Surface((160, 170), pygame.SRCALPHA)
        bx, by = 80, 100

        if self.state == "staggered":
            pygame.draw.rect(tmp, (150, 100, 40), (44, 62, 72, 48), border_radius=6)
            pygame.draw.circle(tmp, (180, 130, 60), (80, 52), 14)
        else:
            body = (220, 210, 210) if self.flash > 0 else (108, 24, 24)
            pygame.draw.rect(tmp, body, (46, 36, 68, 74), border_radius=8)
            pygame.draw.rect(tmp, (70, 14, 14), (54, 46, 52, 40), border_radius=6)
            pygame.draw.circle(tmp, (46, 46, 56), (80, 28), 15)
            pygame.draw.circle(tmp, (255, 120, 40), (int(80 + dx * 6), int(26 + dy * 6)), 3)
            pygame.draw.circle(tmp, (255, 120, 40), (int(80 + dx * 11), int(26 + dy * 11)), 3)

        if self.state != "staggered":
            sc = (150, 150, 164)
            if self.state == "windup":
                flick = 0.5 + 0.5 * math.sin(self.anim_t * 40)
                sc = (255, int(60 + 100 * flick), int(60 + 100 * flick))
            elif self.state == "attack" and self.attack_kind == "slash":
                sc = (240, 230, 240)
            tip = (bx + dx * 70, by + dy * 70)
            pygame.draw.line(tmp, sc, (bx + dx * 16, by + dy * 16), tip, 9)

        if alpha < 255:
            tmp.set_alpha(alpha)
        surf.blit(tmp, (self.rect.x - 48, self.rect.y - 6 + dy_off))
