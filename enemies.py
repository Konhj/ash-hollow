# 敌人数据:个体数值全部在此(框架:后续 Boss 也可迁移至此)
# attacks: 招式数据,melee 用 range/hitbox;远程用 projectile{speed,radius,range,color}
# ai: aggro_radius 仇恨半径 / leash_radius 追击上限 / patrol{type, points} / disengage_heal 脱战回血

import math
import random

import pygame

ENEMIES = {
    "hollow_soldier": {
        "name": {"en": "HOLLOW SOLDIER", "zh": "活尸士兵"},
        "max_hp": 120,
        "speed": 1.6,
        "souls": 50,
        "size": (30, 46),
        "color": (95, 62, 40),
        "attacks": {
            "slash": {
                "windup": 0.55,
                "duration": 0.18,
                "recover": 0.6,
                "damage": 18,
                "range": 55,
                "hitbox": (90, 70),
            },
        },
        "ai": {
            "aggro_radius": 320,
            "leash_radius": 600,
            "patrol": {"type": "pingpong", "points": [(1250, 520), (1550, 520)]},
            "patrol_speed": 0.8,
            "disengage_heal": True,
            "attack_cd": (1.2, 2.0),
        },
    },
    "hollow_archer": {
        "name": {"en": "HOLLOW ARCHER", "zh": "活尸弓手"},
        "max_hp": 80,
        "speed": 1.2,
        "souls": 40,
        "size": (30, 46),
        "color": (80, 66, 58),
        "attacks": {
            "shot": {
                "windup": 0.8,
                "duration": 0.1,
                "recover": 0.8,
                "damage": 12,
                "range": 340,
                "projectile": {"speed": 6.0, "radius": 5, "range": 500, "color": (205, 185, 130)},
            },
        },
        "ai": {
            "aggro_radius": 380,
            "leash_radius": 700,
            "patrol": {"type": "pingpong", "points": [(1850, 780), (2050, 780)]},
            "patrol_speed": 0.7,
            "disengage_heal": True,
            "attack_cd": (1.6, 2.6),
        },
    },
    "hollow_raider": {
        "name": {"en": "HOLLOW RAIDER", "zh": "活尸掠袭者"},
        "max_hp": 180,
        "speed": 2.2,
        "souls": 70,
        "size": (32, 48),
        "color": (120, 70, 45),
        "attacks": {
            "slash": {
                "windup": 0.45,
                "duration": 0.16,
                "recover": 0.5,
                "damage": 20,
                "range": 55,
                "hitbox": (90, 70),
            },
        },
        "ai": {
            "aggro_radius": 300,
            "leash_radius": 600,
            "patrol": {"type": "route", "points": [(1400, 1080), (1700, 1080), (1700, 1220)]},
            "patrol_speed": 1.0,
            "disengage_heal": True,
            "attack_cd": (0.9, 1.6),
        },
    },
}


class Enemy:
    def __init__(self, eid, x, y, index=0):
        self.eid = eid
        self.index = index
        self.data = ENEMIES[eid]
        w, h = self.data["size"]
        self.rect = pygame.Rect(0, 0, w, h)
        self.x = float(x)
        self.y = float(y)
        self.home = (float(x), float(y))
        self.sync_rect()
        self.hp = self.data["max_hp"]
        self.state = "patrol"
        self.state_timer = 0.0
        self.attack_kind = None
        self.dir = pygame.Vector2(-1, 0)
        self.aggro = False
        self.attack_cd = random.uniform(0.6, 1.4)
        self.hit_landed = False
        self.shot_fired = False
        self.flash = 0.0
        self.anim_t = 0.0
        self.dead = False
        self.patrol_idx = 0

    def sync_rect(self):
        self.rect.center = (round(self.x), round(self.y))

    def attacks(self):
        return self.data["attacks"]

    def primary_attack(self):
        return next(iter(self.data["attacks"]))

    def patrol_points(self):
        return self.data["ai"].get("patrol", {}).get("points", [])

    def move_toward(self, tx, ty, speed):
        dx, dy = tx - self.x, ty - self.y
        d = math.hypot(dx, dy)
        if d < 3:
            return True
        self.x += dx / d * speed
        self.y += dy / d * speed
        return False

    def take_damage(self, dmg):
        if self.dead:
            return
        self.hp -= dmg
        self.flash = 0.15
        if self.hp <= 0:
            self.dead = True
            self.state = "dying"
            self.state_timer = 0.5
            self.hit_landed = True
        else:
            self.state = "recover"
            self.state_timer = 0.3
            self.hit_landed = True

    def current_hit_rect(self):
        if self.state != "attack":
            return None
        atk = self.attacks().get(self.attack_kind)
        if not atk or "projectile" in atk:
            return None
        rng = atk["range"]
        w, h = atk.get("hitbox", (90, 70))
        r = pygame.Rect(0, 0, w, h)
        r.center = (self.x + self.dir.x * rng * 0.7, self.y + self.dir.y * 20)
        return r

    def update(self, dt, player, room_def):
        if self.dead:
            if self.state == "dying":
                self.state_timer -= dt
            return
        self.flash = max(0.0, self.flash - dt)
        self.anim_t += dt
        if self.state == "dying":
            self.state_timer -= dt
            return
        ai = self.data["ai"]
        dist_p = math.hypot(player.x - self.x, player.y - self.y)
        if not self.aggro:
            if dist_p <= ai["aggro_radius"]:
                self.aggro = True
        else:
            dist_home = math.hypot(self.home[0] - self.x, self.home[1] - self.y)
            if dist_home > ai["leash_radius"]:
                self.aggro = False
                if ai.get("disengage_heal"):
                    self.hp = self.data["max_hp"]

        if self.aggro:
            self.update_combat(dt, player, ai, dist_p)
        else:
            self.update_patrol(dt, ai)
        self.sync_rect()

    def update_patrol(self, dt, ai):
        pts = self.patrol_points()
        if not pts:
            return
        target = pts[self.patrol_idx]
        arrived = self.move_toward(target[0], target[1], ai.get("patrol_speed", 0.8))
        if arrived:
            self.patrol_idx = (self.patrol_idx + 1) % len(pts)
        dx = target[0] - self.x
        if abs(dx) > 0.5:
            self.dir = pygame.Vector2(1 if dx > 0 else -1, 0)

    def update_combat(self, dt, player, ai, dist_p):
        dx, dy = player.x - self.x, player.y - self.y
        if dist_p > 0:
            self.dir = pygame.Vector2(dx / dist_p, dy / dist_p)
        atk_range = self.attacks()[self.primary_attack()].get("range", 60)
        if self.state in ("patrol", "chase", "idle"):
            if dist_p > atk_range * 0.8:
                self.state = "chase"
                self.move_toward(player.x, player.y, self.data["speed"])
            else:
                self.attack_cd -= dt
                if self.attack_cd <= 0:
                    self.state = "windup"
                    self.attack_kind = self.primary_attack()
                    self.state_timer = self.attacks()[self.attack_kind]["windup"]
        elif self.state == "windup":
            self.state_timer -= dt
            if self.state_timer <= 0:
                self.state = "attack"
                self.state_timer = self.attacks()[self.attack_kind]["duration"]
                self.hit_landed = False
                self.shot_fired = False
        elif self.state == "attack":
            self.state_timer -= dt
            if self.state_timer <= 0:
                self.state = "chase"
                self.attack_cd = random.uniform(*ai.get("attack_cd", (1.0, 2.0)))
        elif self.state == "recover":
            self.state_timer -= dt
            if self.state_timer <= 0:
                self.state = "chase"

    def draw(self, surf, ox=0, oy=0):
        if self.dead:
            alpha = max(0.0, self.state_timer / 0.5)
        else:
            alpha = 1.0
        if alpha <= 0:
            return
        cx, cy = self.rect.centerx - ox, self.rect.centery - oy
        fx, fy = self.dir
        tmp = pygame.Surface((80, 80), pygame.SRCALPHA)
        body = (220, 210, 210) if self.flash > 0 else self.data["color"]
        if self.state == "windup":
            flick = 0.5 + 0.5 * math.sin(self.anim_t * 30)
            body = (255, int(40 + 90 * flick), int(40 + 90 * flick))
        pygame.draw.rect(tmp, body, (24, 24, 32, 30), border_radius=4)
        pygame.draw.circle(tmp, (150, 140, 128), (40, 16), 9)
        atk = self.attacks().get(self.attack_kind) if self.attack_kind else None
        if atk and "projectile" in atk:
            pygame.draw.line(tmp, (140, 120, 90), (40 + fx * 10, 32 + fy * 10), (40 + fx * 34, 32 + fy * 34), 3)
            pygame.draw.circle(tmp, (205, 185, 130), (int(40 + fx * 36), int(32 + fy * 36)), 4)
        else:
            pygame.draw.line(tmp, (200, 200, 210), (40 + fx * 8, 34 + fy * 8), (40 + fx * 30, 34 + fy * 30), 4)
        if self.aggro:
            pygame.draw.circle(tmp, (255, 90, 60), (40, 6), 3)
        tmp.set_alpha(int(alpha * 255))
        surf.blit(tmp, (self.rect.x - 25 - ox, self.rect.y - 20 - oy))
