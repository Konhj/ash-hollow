import math

import pygame

from settings import *
import items as items_data


class Player:
    def __init__(self, x, y, bindings):
        self.bindings = bindings
        self.spawn = (float(x), float(y))
        self.rect = pygame.Rect(0, 0, 30, 46)
        self.facing = pygame.Vector2(1, 0)
        self.last_move = pygame.Vector2(0, 0)
        self.inventory = list(items_data.DEFAULT_INVENTORY)
        self.equipment = dict(items_data.DEFAULT_EQUIPMENT)
        self.reset_stats()

    def shield(self):
        oid = self.equipment.get("offhand")
        item = items_data.get(oid) if oid else None
        if item and item["type"] == "weapon" and item.get("subtype") == "shield":
            return item
        return None

    def talisman_effects(self):
        out = {}
        for i in (1, 2, 3):
            tid = self.equipment.get(f"talisman{i}")
            item = items_data.get(tid) if tid else None
            if item:
                for k, v in item.get("effect", {}).items():
                    out[k] = out.get(k, 0) + v
        return out

    def max_hp(self):
        return PLAYER_MAX_HP + self.talisman_effects().get("hp_up", 0)

    def max_stamina(self):
        return PLAYER_MAX_STAMINA + self.talisman_effects().get("stamina_up", 0)

    def defense(self):
        d = 0
        aid = self.equipment.get("armor")
        item = items_data.get(aid) if aid else None
        if item:
            d += item["stats"].get("defense", 0)
        return d

    def attack_damage(self):
        wid = self.equipment.get("mainhand")
        item = items_data.get(wid) if wid else None
        if item and item["type"] == "weapon":
            return item["stats"].get("damage", ATTACK_DAMAGE)
        return ATTACK_DAMAGE

    def attack_speed(self):
        wid = self.equipment.get("mainhand")
        item = items_data.get(wid) if wid else None
        if item and item["type"] == "weapon":
            return item["stats"].get("speed", 1.0)
        return 1.0

    def current_attack_damage(self):
        if self.state == "attack" and getattr(self, "attack_is_offhand", False):
            oid = self.equipment.get("offhand")
            item = items_data.get(oid) if oid else None
            if item and item["type"] == "weapon":
                return item["stats"].get("damage", ATTACK_DAMAGE)
        return self.attack_damage()

    def current_attack_style(self):
        item = None
        if self.state == "attack" and getattr(self, "attack_is_offhand", False):
            oid = self.equipment.get("offhand")
            item = items_data.get(oid) if oid else None
        else:
            wid = self.equipment.get("mainhand")
            item = items_data.get(wid) if wid else None
        if item and item["type"] == "weapon":
            return item.get("attack_style", "slash")
        return "slash"

    def draw_slash_effect(self, surf):
        fx, fy = self.facing
        ang = math.atan2(fy, fx)
        frac = (self.attack_duration - self.state_timer) / max(0.001, self.attack_duration)
        seg = pygame.Surface((150, 150), pygame.SRCALPHA)
        r = 62
        for i, off in enumerate((0.0, -0.12, -0.24)):
            p = max(0.0, min(1.0, frac + off))
            a1 = math.radians(-110 + 220 * p - 27.5)
            a2 = math.radians(-110 + 220 * p + 27.5)
            alpha = int(230 - i * 90)
            pygame.draw.arc(seg, (255, 255, 255, alpha), (75 - r, 75 - r, r * 2, r * 2), a1, a2, 5)
            pygame.draw.arc(
                seg,
                (255, 255, 255, alpha // 2),
                (75 - r + 12, 75 - r + 12, (r - 12) * 2, (r - 12) * 2),
                a1,
                a2,
                3,
            )
        rotated = pygame.transform.rotate(seg, -math.degrees(ang))
        surf.blit(rotated, rotated.get_rect(center=self.rect.center))

    def refresh_max_hp(self):
        new_max = self.max_hp()
        self.hp = min(new_max, max(1, self.hp + (new_max - self._prev_max_hp)))
        self._prev_max_hp = new_max

    def reset_stats(self):
        self.hp = self.max_hp()
        self._prev_max_hp = self.max_hp()
        self.stamina = self.max_stamina()
        self.estus = ESTUS_MAX
        self.state = "idle"
        self.state_timer = 0.0
        self.invuln = 0.0
        self.roll_cooldown = 0.0
        self.roll_dir = pygame.Vector2(1, 0)
        self.attack_landed = False
        self.riposte_done = False
        self.just_drank = False
        self.flash = 0.0
        self.dead = False
        self.x, self.y = self.spawn
        self.sync_rect()

    def respawn(self):
        self.rect.center = self.spawn
        self.facing = pygame.Vector2(1, 0)
        self.reset_stats()

    def sync_rect(self):
        self.rect.center = (round(self.x), round(self.y))

    def clamp(self):
        self.x = max(31.0, min(WIDTH - 31.0, self.x))
        self.y = max(39.0, min(HEIGHT - 39.0, self.y))
        self.sync_rect()

    def center_dist(self, other):
        ox, oy = other.rect.center
        return ((self.rect.centerx - ox) ** 2 + (self.rect.centery - oy) ** 2) ** 0.5

    def update(self, dt, keys):
        if self.dead:
            return
        self.invuln = max(0.0, self.invuln - dt)
        self.roll_cooldown = max(0.0, self.roll_cooldown - dt)
        self.flash = max(0.0, self.flash - dt)

        move = pygame.Vector2(0, 0)
        if keys[self.bindings["move_left"]]:
            move.x -= 1
        if keys[self.bindings["move_right"]]:
            move.x += 1
        if keys[self.bindings["move_up"]]:
            move.y -= 1
        if keys[self.bindings["move_down"]]:
            move.y += 1
        if move.length_squared() > 0:
            move.normalize_ip()
        self.last_move = move

        if self.state == "guard":
            held = keys[self.bindings["offhand"]] and self.shield() is not None
            if not held:
                self.state = "idle"
            else:
                self.guard_timer += dt
                if move.length_squared() > 0:
                    self.facing = move.copy()
                    self.x += move.x * PLAYER_SPEED * GUARD_SPEED_MULT
                    self.y += move.y * PLAYER_SPEED * GUARD_SPEED_MULT
                    self.clamp()
                self.stamina = max(0.0, self.stamina - GUARD_DRAIN * dt)
                if self.stamina <= 0:
                    self.state = "idle"
        elif self.state == "idle":
            sprint_held = (
                keys[self.bindings["sprint"]]
                and move.length_squared() > 0
            )
            sprinting = sprint_held and self.stamina > 0
            if not sprint_held:
                self.stamina = min(self.max_stamina(), self.stamina + STAMINA_REGEN * dt)
            if move.length_squared() > 0:
                self.facing = move.copy()
                spd = PLAYER_SPEED * SPRINT_MULT if sprinting else PLAYER_SPEED
                self.x += move.x * spd
                self.y += move.y * spd
                self.clamp()
                if sprinting:
                    self.stamina = max(0.0, self.stamina - SPRINT_DRAIN * dt)
        elif self.state == "roll":
            self.x += self.roll_dir.x * ROLL_SPEED
            self.y += self.roll_dir.y * ROLL_SPEED
            self.clamp()
            self.state_timer -= dt
            if self.state_timer <= 0:
                self.state = "idle"
                self.roll_cooldown = ROLL_COOLDOWN
        elif self.state in ("attack", "riposte", "drink"):
            self.state_timer -= dt
            if self.state_timer <= 0:
                if self.state == "drink":
                    self.hp = min(self.max_hp(), self.hp + ESTUS_HEAL)
                    self.estus -= 1
                    self.just_drank = True
                self.state = "idle"

    def start_attack(self):
        if self.stamina < ATTACK_COST:
            return "out_of_stamina"
        self.stamina -= ATTACK_COST
        self.state = "attack"
        self.attack_is_offhand = False
        self.attack_duration = ATTACK_TIME / self.attack_speed()
        self.state_timer = self.attack_duration
        self.attack_landed = False

    def start_roll(self):
        if self.roll_cooldown > 0:
            return None
        if self.stamina < ROLL_COST:
            return "out_of_stamina"
        self.stamina -= ROLL_COST
        self.state = "roll"
        self.state_timer = ROLL_TIME
        self.invuln = ROLL_TIME
        self.roll_dir = self.last_move.copy() if self.last_move.length_squared() > 0 else self.facing.copy()

    def start_guard(self):
        if self.stamina <= 0:
            return
        self.state = "guard"
        self.guard_timer = 0.0

    def start_offhand_attack(self):
        oid = self.equipment.get("offhand")
        item = items_data.get(oid) if oid else None
        if not item or item["type"] != "weapon" or item.get("subtype") == "shield":
            return None
        if self.stamina < ATTACK_COST:
            return "out_of_stamina"
        self.stamina -= ATTACK_COST
        self.state = "attack"
        self.attack_is_offhand = True
        self.attack_duration = ATTACK_TIME / item["stats"].get("speed", 1.0)
        self.state_timer = self.attack_duration
        self.attack_landed = False

    def start_drink(self):
        if self.estus <= 0:
            return "no_estus"
        self.state = "drink"
        self.state_timer = DRINK_TIME

    def start_riposte(self):
        if self.stamina < RIPOSTE_COST:
            return "out_of_stamina"
        self.stamina -= RIPOSTE_COST
        self.state = "riposte"
        self.state_timer = RIPOSTE_TIME
        self.riposte_done = False

    def parry_window_active(self):
        if self.state != "guard":
            return False
        sh = self.shield()
        if not sh:
            return False
        return self.guard_timer <= sh["stats"].get("parry_time", 0.2)

    def attack_active(self):
        if self.state != "attack":
            return False
        if self.attack_duration <= 0:
            return False
        frac = (self.attack_duration - self.state_timer) / self.attack_duration
        return 0.22 <= frac <= 0.61

    def get_attack_hitbox(self):
        fx, fy = self.facing
        hb = pygame.Rect(0, 0, 74, 52)
        hb.center = (self.rect.centerx + fx * 46, self.rect.centery + fy * 30)
        return hb

    def take_damage(self, dmg, from_dir):
        if self.invuln > 0 or self.state == "roll":
            return False
        final = max(1, int(dmg - self.defense()))
        if self.state == "guard":
            sh = self.shield()
            if sh:
                final = max(1, int(final * (1 - sh["stats"].get("defense", 0.0))))
        self.hp -= final
        self.flash = 0.18
        if from_dir is not None:
            self.x += from_dir.x * 14
            self.y += from_dir.y * 14
            self.clamp()
        if self.hp <= 0:
            self.hp = 0
            self.dead = True
        return True

    def draw(self, surf):
        if self.dead:
            return
        fx, fy = self.facing
        tmp = pygame.Surface((70, 74), pygame.SRCALPHA)

        if self.state == "roll":
            pygame.draw.circle(tmp, (95, 95, 110, 220), (35, 40), 15)
            pygame.draw.circle(tmp, (70, 70, 84, 220), (35, 40), 15, 3)
        else:
            body = (200, 200, 210) if self.flash > 0 else (68, 68, 80)
            pygame.draw.rect(tmp, body, (20, 22, 30, 26), border_radius=4)
            pygame.draw.rect(tmp, (48, 48, 56), (22, 26, 26, 18), border_radius=3)
            pygame.draw.circle(tmp, (150, 140, 128), (35, 16), 8)
            tip = (35 + fx * 24, 30 + fy * 24)
            if self.state == "attack":
                tip = (35 + fx * 34, 30 + fy * 34)
            if self.state == "riposte":
                tip = (35 + fx * 38, 30 + fy * 38)
                pygame.draw.line(tmp, (255, 240, 160), (35 + fx * 8, 30 + fy * 8), tip, 5)
            else:
                pygame.draw.line(tmp, (200, 200, 214), (35 + fx * 8, 30 + fy * 8), tip, 4)
            if self.state == "guard":
                pygame.draw.circle(tmp, (200, 200, 215, 220), (35 + fx * 16, 32 + fy * 16), 13, 4)
                pygame.draw.circle(tmp, (140, 140, 155, 180), (35 + fx * 16, 32 + fy * 16), 13)
            if self.state == "drink":
                pygame.draw.circle(tmp, (240, 150, 40, 230), (35 + fx * 10, 14), 5)

        if self.invuln > 0 and self.state != "roll":
            tmp.set_alpha(150)
        surf.blit(tmp, (self.rect.x - 20, self.rect.y - 14))
        if self.state == "attack" and self.current_attack_style() == "slash":
            self.draw_slash_effect(surf)
