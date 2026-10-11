import pygame

from settings import *


class Player:
    def __init__(self, x, y, bindings):
        self.bindings = bindings
        self.spawn = (float(x), float(y))
        self.rect = pygame.Rect(0, 0, 30, 46)
        self.facing = pygame.Vector2(1, 0)
        self.last_move = pygame.Vector2(0, 0)
        self.reset_stats()

    def reset_stats(self):
        self.hp = PLAYER_MAX_HP
        self.stamina = PLAYER_MAX_STAMINA
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

        if self.state == "idle":
            self.stamina = min(PLAYER_MAX_STAMINA, self.stamina + STAMINA_REGEN * dt)
            if move.length_squared() > 0:
                self.facing = move.copy()
                self.x += move.x * PLAYER_SPEED
                self.y += move.y * PLAYER_SPEED
                self.clamp()
        elif self.state == "roll":
            self.x += self.roll_dir.x * ROLL_SPEED
            self.y += self.roll_dir.y * ROLL_SPEED
            self.clamp()
            self.state_timer -= dt
            if self.state_timer <= 0:
                self.state = "idle"
                self.roll_cooldown = ROLL_COOLDOWN
        elif self.state in ("attack", "parry", "riposte", "drink"):
            self.state_timer -= dt
            if self.state_timer <= 0:
                if self.state == "drink":
                    self.hp = min(PLAYER_MAX_HP, self.hp + ESTUS_HEAL)
                    self.estus -= 1
                    self.just_drank = True
                self.state = "idle"

    def start_attack(self):
        if self.stamina < ATTACK_COST:
            return "out_of_stamina"
        self.stamina -= ATTACK_COST
        self.state = "attack"
        self.state_timer = ATTACK_TIME
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

    def start_parry(self):
        if self.stamina < PARRY_COST:
            return "out_of_stamina"
        self.stamina -= PARRY_COST
        self.state = "parry"
        self.state_timer = PARRY_TIME

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
        return self.state == "parry" and self.state_timer >= PARRY_TIME - PARRY_WINDOW

    def attack_active(self):
        if self.state != "attack":
            return False
        elapsed = ATTACK_TIME - self.state_timer
        return ATTACK_ACTIVE_START <= elapsed <= ATTACK_ACTIVE_END

    def get_attack_hitbox(self):
        fx, fy = self.facing
        hb = pygame.Rect(0, 0, 74, 52)
        hb.center = (self.rect.centerx + fx * 46, self.rect.centery + fy * 30)
        return hb

    def take_damage(self, dmg, from_dir):
        if self.invuln > 0 or self.state == "roll":
            return False
        self.hp -= dmg
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
            if self.state == "parry":
                pygame.draw.circle(tmp, (222, 222, 235, 200), (35 + fx * 16, 32 + fy * 16), 11, 3)
            if self.state == "drink":
                pygame.draw.circle(tmp, (240, 150, 40, 230), (35 + fx * 10, 14), 5)

        if self.invuln > 0 and self.state != "roll":
            tmp.set_alpha(150)
        surf.blit(tmp, (self.rect.x - 20, self.rect.y - 14))
