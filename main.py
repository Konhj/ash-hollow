import math
import random
import sys

import pygame

from settings import *
from player import Player
from boss import Boss
from bonfire import Bonfire
from effects import Particles

if sys.platform == "win32":
    try:
        import ctypes

        ctypes.windll.shcore.SetProcessDpiAwareness(1)
    except Exception:
        pass


def draw_bar(surf, x, y, w, h, frac, color, bg):
    pygame.draw.rect(surf, (16, 16, 20), (x - 2, y - 2, w + 4, h + 4))
    pygame.draw.rect(surf, bg, (x, y, w, h))
    pygame.draw.rect(surf, color, (x, y, int(w * max(0.0, min(1.0, frac))), h))


class Game:
    def __init__(self):
        pygame.init()
        self.screen = pygame.display.set_mode((WIDTH, HEIGHT))
        pygame.display.set_caption("ASH HOLLOW")
        self.clock = pygame.time.Clock()
        self.font_small = pygame.font.Font(None, 22)
        self.font = pygame.font.Font(None, 30)
        self.font_big = pygame.font.Font(None, 56)
        self.font_huge = pygame.font.Font(None, 110)
        self.floor = self.make_floor()
        self.particles = Particles()
        self.messages = []
        self.time = 0.0
        self.shake = 0.0
        self.fade = 1.0
        self.death_timer = 0.0
        self.state = "title"
        self.new_game()

    def make_floor(self):
        surf = pygame.Surface((WIDTH, HEIGHT))
        surf.fill(FLOOR)
        random.seed(7)
        for gy in range(0, HEIGHT, 80):
            for gx in range(0, WIDTH, 80):
                c = (
                    FLOOR[0] + random.randint(-5, 5),
                    FLOOR[1] + random.randint(-5, 5),
                    FLOOR[2] + random.randint(-5, 5),
                )
                pygame.draw.rect(surf, c, (gx + 2, gy + 2, 76, 76))
                pygame.draw.rect(surf, FLOOR_DARK, (gx, gy, 80, 80), 1)
        return surf

    def new_game(self):
        self.player = Player(260, 560)
        self.boss = Boss(1080, 400)
        self.bonfire = Bonfire(150, 540)
        self.messages.clear()
        self.particles.items.clear()
        self.shake = 0.0
        self.fade = 1.0
        self.death_timer = 0.0

    def message(self, text, color=WHITE, dur=1.4):
        for m in self.messages:
            if m[0] == text:
                return
        self.messages.append([text, color, dur, dur])

    def on_player_hit(self):
        p = self.player
        self.shake = max(self.shake, 0.7)
        self.particles.spawn(p.rect.centerx, p.rect.centery, (200, 40, 40), 12, 3.5, 0.4)

    def run(self):
        while True:
            dt = self.clock.tick(FPS) / 1000.0
            for event in pygame.event.get():
                if event.type == pygame.QUIT:
                    pygame.quit()
                    sys.exit()
                if event.type == pygame.KEYDOWN:
                    if event.key == pygame.K_ESCAPE:
                        pygame.quit()
                        sys.exit()
                    if event.key == pygame.K_RETURN:
                        if self.state in ("title", "victory"):
                            self.new_game()
                            self.state = "play"
            self.update(dt)
            self.draw()
            pygame.display.flip()

    def update(self, dt, keys=None):
        if keys is None:
            keys = pygame.key.get_pressed()
        self.time += dt
        self.fade = max(0.0, self.fade - dt * 1.5)
        self.shake = max(0.0, self.shake - dt * 2.2)
        for m in self.messages:
            m[3] -= dt
        self.messages = [m for m in self.messages if m[3] > 0]

        if self.state == "title":
            return
        if self.state == "died":
            self.death_timer -= dt
            self.particles.update(dt)
            if self.death_timer <= 0:
                self.player.respawn()
                self.boss.reset()
                self.state = "play"
                self.fade = 1.0
                self.message("RESPAWNED AT BONFIRE", ORANGE, 1.6)
            return
        if self.state == "victory":
            self.particles.update(dt)
            return

        p = self.player
        b = self.boss
        self.bonfire.update(dt)
        p.update(dt, keys)
        if p.just_drank:
            p.just_drank = False
            self.particles.spawn(p.rect.centerx, p.rect.centery, (120, 220, 120), 10, 2.2, 0.5)
        b.update(dt, p)
        self.particles.update(dt)

        if random.random() < 0.35:
            bx, by = self.bonfire.pos
            self.particles.spawn(bx + random.uniform(-8, 8), by - 10, (255, 170, 50), 1, 1.2, 0.8)
        if b.state == "attack" and b.attack_kind == "charge":
            self.particles.spawn(
                b.rect.centerx - b.charge_dir.x * 40,
                b.rect.centery + 20,
                (180, 60, 40),
                2,
                2.0,
                0.3,
            )

        if p.dead:
            self.state = "died"
            self.death_timer = 2.4
            return

        if p.state == "idle":
            if keys[pygame.K_j] and not b.dead:
                if b.state == "staggered" and p.center_dist(b) < 130:
                    r = p.start_riposte()
                else:
                    r = p.start_attack()
                if r:
                    self.message(r, RED)
            elif keys[pygame.K_k]:
                r = p.start_roll()
                if r:
                    self.message(r, RED)
            elif keys[pygame.K_l]:
                r = p.start_parry()
                if r:
                    self.message(r, RED)
            elif keys[pygame.K_f]:
                r = p.start_drink()
                if r:
                    self.message(r, RED)
            elif keys[pygame.K_e] and self.bonfire.in_range(p.rect.center):
                if b.aggro and not b.dead:
                    self.message("CANNOT REST, FOE NEARBY", RED, 1.4)
                else:
                    p.hp = PLAYER_MAX_HP
                    p.estus = ESTUS_MAX
                    b.reset()
                    self.fade = 1.0
                    self.message("BONFIRE RESTED", ORANGE, 1.6)

        if p.state == "attack" and not p.attack_landed and p.attack_active() and not b.dead:
            hb = p.get_attack_hitbox()
            if hb.colliderect(b.rect):
                p.attack_landed = True
                b.take_damage(ATTACK_DAMAGE)
                self.particles.spawn(hb.centerx, hb.centery, (255, 220, 120), 6, 3.0, 0.3)

        if p.state == "riposte" and not p.riposte_done:
            if (RIPOSTE_TIME - p.state_timer) >= 0.25:
                p.riposte_done = True
                if not b.dead and p.center_dist(b) < 160:
                    b.take_damage(RIPOSTE_DAMAGE)
                    b.state = "recover"
                    b.state_timer = 0.8
                    self.shake = 1.2
                    self.particles.spawn(b.rect.centerx, b.rect.centery, (255, 200, 60), 24, 5.0, 0.6)
                    self.message("RIPOSTE!", YELLOW, 1.2)

        if b.state == "attack" and not b.hit_landed:
            if b.attack_kind == "slash":
                hr = b.current_hit_rect()
                if hr and hr.colliderect(p.rect):
                    b.hit_landed = True
                    if p.parry_window_active():
                        b.stagger(2.5)
                        p.invuln = 0.5
                        self.shake = 0.6
                        self.particles.spawn(p.rect.centerx, p.rect.centery, (255, 255, 255), 14, 4.0, 0.4)
                        self.message("PARRY!", YELLOW, 1.2)
                    elif p.take_damage(SLASH_DAMAGE, b.dir):
                        self.on_player_hit()
            elif b.attack_kind == "charge":
                if b.rect.inflate(26, 16).colliderect(p.rect):
                    b.hit_landed = True
                    if p.take_damage(CHARGE_DAMAGE, b.charge_dir):
                        self.on_player_hit()
            elif b.attack_kind == "slam":
                if not b.slam_landed:
                    b.slam_landed = True
                    self.shake = 1.4
                    self.particles.spawn(b.rect.centerx, b.rect.bottom, (120, 90, 60), 26, 4.0, 0.5)
                    if p.center_dist(b) <= SLAM_RADIUS:
                        if p.take_damage(SLAM_DAMAGE, b.dir):
                            self.on_player_hit()

        if b.dead and b.state_timer <= 0:
            self.state = "victory"
            self.message("VICTORY ACHIEVED", YELLOW, 2.0)

    def draw(self):
        surf = self.screen
        surf.fill(BG)
        world = pygame.Surface((WIDTH, HEIGHT))
        world.blit(self.floor, (0, 0))
        pygame.draw.rect(world, WALL, (0, 0, WIDTH, HEIGHT), 6)
        if self.state in ("play", "died", "victory"):
            self.bonfire.draw(world)
            self.boss.draw(world)
            if not self.player.dead:
                self.player.draw(world)
            self.particles.draw(world)
        if self.shake > 0.02:
            ox = random.randint(-int(self.shake * 14), int(self.shake * 14))
            oy = random.randint(-int(self.shake * 14), int(self.shake * 14))
            surf.blit(world, (ox, oy))
        else:
            surf.blit(world, (0, 0))

        if self.state in ("play", "died"):
            self.draw_hud()
            self.draw_banner()
            self.draw_messages()
        if self.state == "died":
            a = max(0.0, min(1.0, (2.4 - self.death_timer) / 0.6))
            img = self.font_huge.render("YOU DIED", True, (190, 20, 20))
            img.set_alpha(int(255 * a))
            surf.blit(img, img.get_rect(center=(WIDTH // 2, 250)))
        elif self.state == "victory":
            self.draw_victory()
        elif self.state == "title":
            self.draw_title()

        if self.fade > 0:
            ov = pygame.Surface((WIDTH, HEIGHT), pygame.SRCALPHA)
            ov.fill((0, 0, 0, int(255 * min(1.0, self.fade))))
            surf.blit(ov, (0, 0))

    def draw_hud(self):
        s = self.screen
        label = self.font_small.render("UNKINDLED", True, GRAY)
        s.blit(label, (30, 8))
        draw_bar(s, 30, 28, 260, 16, self.player.hp / PLAYER_MAX_HP, RED, DARK_RED)
        draw_bar(s, 30, 50, 220, 10, self.player.stamina / PLAYER_MAX_STAMINA, GREEN, DARK_GREEN)
        for i in range(ESTUS_MAX):
            self.draw_flask(s, 30 + i * 32, 68, i < self.player.estus)
        if self.boss.aggro and not self.boss.dead:
            w = 840
            x = (WIDTH - w) // 2
            y = HEIGHT - 58
            name = self.font.render(BOSS_NAME, True, WHITE)
            s.blit(name, (x, y - 26))
            draw_bar(s, x, y, w, 18, self.boss.hp / self.boss.max_hp, (150, 20, 20), (60, 10, 10))

    def draw_flask(self, s, x, y, filled):
        color = ORANGE if filled else (60, 60, 66)
        pygame.draw.rect(s, color, (x, y, 16, 22), border_radius=3)
        pygame.draw.rect(s, (200, 200, 210), (x + 5, y - 6, 6, 8), border_radius=2)

    def draw_banner(self):
        t = self.boss.aggro_banner
        if t <= 0:
            return
        fade_in = min(1.0, (2.4 - t) / 0.4)
        fade_out = min(1.0, t / 0.5)
        alpha = int(255 * min(fade_in, fade_out))
        img = self.font_huge.render(BOSS_NAME, True, (220, 60, 40))
        img.set_alpha(alpha)
        self.screen.blit(img, img.get_rect(center=(WIDTH // 2, 190)))
        sub = self.font_big.render("HEIR OF THE EMBERS", True, GRAY)
        sub.set_alpha(alpha)
        self.screen.blit(sub, sub.get_rect(center=(WIDTH // 2, 260)))

    def draw_messages(self):
        y = 310
        for text, color, dur, t in self.messages:
            alpha = 255 if t > 0.3 else int(255 * t / 0.3)
            img = self.font_big.render(text, True, color)
            img.set_alpha(alpha)
            self.screen.blit(img, img.get_rect(center=(WIDTH // 2, y)))
            y += 36

    def draw_victory(self):
        v = self.font_huge.render("VICTORY ACHIEVED", True, YELLOW)
        self.screen.blit(v, v.get_rect(center=(WIDTH // 2, 280)))
        pr = self.font_big.render("PRESS ENTER TO PLAY AGAIN", True, WHITE)
        self.screen.blit(pr, pr.get_rect(center=(WIDTH // 2, 380)))

    def draw_title(self):
        s = self.screen
        s.blit(self.floor, (0, 0))
        dark = pygame.Surface((WIDTH, HEIGHT), pygame.SRCALPHA)
        dark.fill((0, 0, 0, 190))
        s.blit(dark, (0, 0))
        t = self.font_huge.render("ASH HOLLOW", True, (200, 200, 210))
        s.blit(t, t.get_rect(center=(WIDTH // 2, 180)))
        sub = self.font.render("A TINY SOULS-LIKE", True, GRAY)
        s.blit(sub, sub.get_rect(center=(WIDTH // 2, 245)))
        lines = [
            "WASD  MOVE",
            "J  ATTACK / RIPOSTE",
            "K  ROLL (I-FRAMES)",
            "L  PARRY",
            "F  ESTUS FLASK",
            "E  REST AT BONFIRE",
        ]
        y = 320
        for line in lines:
            img = self.font.render(line, True, (150, 150, 160))
            s.blit(img, img.get_rect(center=(WIDTH // 2, y)))
            y += 34
        blink = int((math.sin(self.time * 3) + 1) / 2 * 255)
        pr = self.font_big.render("PRESS ENTER TO BEGIN", True, (255, 200, 120))
        pr.set_alpha(blink)
        s.blit(pr, pr.get_rect(center=(WIDTH // 2, 560)))


if __name__ == "__main__":
    Game().run()
