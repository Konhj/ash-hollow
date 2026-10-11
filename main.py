import math
import os
import random
import sys

import pygame

from settings import *
from player import Player
from boss import Boss
from bonfire import Bonfire
from effects import Particles
import config
import i18n

if sys.platform == "win32":
    try:
        import ctypes

        ctypes.windll.shcore.SetProcessDpiAwareness(1)
    except Exception:
        pass

KEY_ACTIONS = [
    ("move_up", "up"),
    ("move_down", "down"),
    ("move_left", "left"),
    ("move_right", "right"),
    ("attack", "attack"),
    ("roll", "roll"),
    ("parry", "parry"),
    ("estus", "estus"),
    ("interact", "rest"),
]
MOVE_DIRS = {"move_up": "up", "move_down": "down", "move_left": "left", "move_right": "right"}


def draw_bar(surf, x, y, w, h, frac, color, bg):
    pygame.draw.rect(surf, (16, 16, 20), (x - 2, y - 2, w + 4, h + 4))
    pygame.draw.rect(surf, bg, (x, y, w, h))
    pygame.draw.rect(surf, color, (x, y, int(w * max(0.0, min(1.0, frac))), h))


class Menu:
    def __init__(self):
        self.title = ""
        self.items = []
        self.selected = 0
        self.stack = []
        self.capture = None
        self.start_y = 300
        self.step = 56

    def set(self, title, items, start_y=300, step=56):
        self.title = title
        self.items = items
        self.selected = 0
        self.start_y = start_y
        self.step = step

    def push(self, title, items, start_y=300, step=56):
        self.stack.append((self.title, self.items, self.selected, self.start_y, self.step))
        self.set(title, items, start_y, step)

    def pop(self):
        if self.stack:
            self.title, self.items, self.selected, self.start_y, self.step = self.stack.pop()
            return True
        return False

    def update(self, just):
        if self.capture or not self.items:
            return
        if pygame.K_UP in just or pygame.K_w in just:
            self.selected = (self.selected - 1) % len(self.items)
        if pygame.K_DOWN in just or pygame.K_s in just:
            self.selected = (self.selected + 1) % len(self.items)
        item = self.items[self.selected]
        if pygame.K_LEFT in just and item.get("adjust"):
            item["adjust"](-1)
        if pygame.K_RIGHT in just and item.get("adjust"):
            item["adjust"](1)
        if pygame.K_RETURN in just and item.get("action"):
            item["action"]()

    def draw(self, surf, fonts):
        if self.title:
            img = fonts["big"].render(self.title, True, (220, 220, 228))
            surf.blit(img, img.get_rect(center=(WIDTH // 2, 150)))
        y = self.start_y
        for i, item in enumerate(self.items):
            sel = i == self.selected
            if item.get("enabled") and not item["enabled"]():
                color = (70, 70, 78)
            elif sel:
                color = YELLOW
            else:
                color = GRAY
            label = item["label"]() if callable(item["label"]) else item["label"]
            if sel:
                label = "> " + label + " <"
            img = fonts["big"].render(label, True, color)
            surf.blit(img, img.get_rect(center=(WIDTH // 2, y)))
            y += self.step
        if self.capture:
            img = fonts["big"].render(i18n.t("press_any_key"), True, YELLOW)
            surf.blit(img, img.get_rect(center=(WIDTH // 2, 640)))


class Game:
    def __init__(self):
        pygame.init()
        self.cfg = config.load_config()
        i18n.set_lang(self.cfg["language"])
        self.screen = pygame.display.set_mode(RESOLUTIONS[self.cfg["resolution"]], pygame.RESIZABLE)
        pygame.display.set_caption("ASH HOLLOW")
        self.clock = pygame.time.Clock()
        self.fonts = {}
        self.load_fonts()
        self.canvas = pygame.Surface((WIDTH, HEIGHT))
        self.floor = self.make_floor()
        self.particles = Particles()
        self.messages = []
        self.time = 0.0
        self.shake = 0.0
        self.fade = 1.0
        self.death_timer = 0.0
        self.just = set()
        self.menu = Menu()
        self.state = "main_menu"
        self.build_main_menu()
        self.new_game()

    def load_fonts(self):
        path = config.resource_path(os.path.join("fonts", "wqy-subset.ttf"))
        try:
            self.fonts["small"] = pygame.font.Font(path, 22)
            self.fonts["normal"] = pygame.font.Font(path, 30)
            self.fonts["big"] = pygame.font.Font(path, 54)
            self.fonts["huge"] = pygame.font.Font(path, 110)
        except Exception:
            self.fonts["small"] = pygame.font.Font(None, 22)
            self.fonts["normal"] = pygame.font.Font(None, 30)
            self.fonts["big"] = pygame.font.Font(None, 54)
            self.fonts["huge"] = pygame.font.Font(None, 110)

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
        self.player = Player(260, 560, self.cfg["bindings"])
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

    def apply_resolution(self):
        self.screen = pygame.display.set_mode(RESOLUTIONS[self.cfg["resolution"]], pygame.RESIZABLE)

    def adjust_resolution(self, delta):
        n = len(RESOLUTIONS)
        self.cfg["resolution"] = (self.cfg["resolution"] + delta) % n
        self.apply_resolution()
        config.save_config(self.cfg)

    def adjust_language(self, delta):
        self.cfg["language"] = "zh" if self.cfg["language"] == "en" else "en"
        i18n.set_lang(self.cfg["language"])
        config.save_config(self.cfg)

    def set_binding(self, action, key):
        if key == pygame.K_ESCAPE:
            return
        self.cfg["bindings"][action] = key
        config.save_config(self.cfg)

    def quit_game(self):
        pygame.quit()
        sys.exit()

    def go_main_menu(self):
        self.build_main_menu()
        self.state = "main_menu"

    def build_main_menu(self):
        t = i18n.t
        items = [
            {"label": lambda: t("new_game"), "action": self.start_new_game},
            {
                "label": lambda: t("continue"),
                "action": self.load_and_continue,
                "enabled": lambda: config.load_save() is not None,
            },
            {"label": lambda: t("settings"), "action": self.build_settings_menu},
            {"label": lambda: t("quit"), "action": self.quit_game},
        ]
        self.menu.set(t("game_title"), items)

    def build_pause_menu(self):
        t = i18n.t
        items = [
            {"label": lambda: t("resume"), "action": self.resume_game},
            {"label": lambda: t("settings"), "action": self.build_settings_menu},
            {"label": lambda: t("save_and_exit"), "action": self.save_and_exit},
        ]
        self.menu.set(t("pause"), items)

    def build_settings_menu(self):
        t = i18n.t

        def res_label():
            w, h = RESOLUTIONS[self.cfg["resolution"]]
            return f"{t('resolution')}: {w} x {h}"

        def lang_label():
            cur = "lang_zh" if self.cfg["language"] == "zh" else "lang_en"
            return f"{t('language')}: {t(cur)}"

        items = [
            {"label": res_label, "adjust": self.adjust_resolution},
            {"label": lang_label, "adjust": self.adjust_language},
            {"label": lambda: t("key_bindings"), "action": self.build_key_menu},
            {"label": lambda: t("back"), "action": lambda: self.menu.pop()},
        ]
        self.menu.push(t("settings"), items)

    def build_key_menu(self):
        t = i18n.t
        items = []
        for action, label_key in KEY_ACTIONS:

            def mk(action=action, label_key=label_key):
                name = config.key_name(self.cfg["bindings"][action])
                if action in MOVE_DIRS:
                    return f"{t('move')} {t(MOVE_DIRS[action])}: {name}"
                return f"{t(label_key)}: {name}"

            def act(action=action):
                self.menu.capture = action

            items.append({"label": mk, "action": act})
        items.append({"label": lambda: t("back"), "action": lambda: self.menu.pop()})
        self.menu.push(t("key_bindings"), items, start_y=250, step=44)

    def start_new_game(self):
        self.new_game()
        self.state = "play"

    def resume_game(self):
        self.state = "play"

    def save_current(self):
        p, b = self.player, self.boss
        if b.dead:
            config.delete_save()
            return
        config.save_game(
            {
                "version": 1,
                "player": {"x": p.x, "y": p.y, "hp": p.hp, "estus": p.estus},
                "boss": {"x": b.x, "y": b.y, "hp": b.hp, "aggro": b.aggro},
            }
        )

    def load_and_continue(self):
        data = config.load_save()
        if not data:
            return
        self.new_game()
        p, b = self.player, self.boss
        pd = data.get("player", {})
        bd = data.get("boss", {})
        p.x = pd.get("x", p.x)
        p.y = pd.get("y", p.y)
        p.hp = min(PLAYER_MAX_HP, max(1, int(pd.get("hp", p.hp))))
        p.estus = min(ESTUS_MAX, max(0, int(pd.get("estus", p.estus))))
        p.sync_rect()
        b.x = bd.get("x", b.x)
        b.y = bd.get("y", b.y)
        b.hp = min(b.max_hp, max(1, int(bd.get("hp", b.hp))))
        b.aggro = bool(bd.get("aggro", False))
        b.sync_rect()
        self.state = "play"
        self.fade = 1.0

    def save_and_exit(self):
        self.save_current()
        self.message(i18n.t("game_saved"), ORANGE, 1.6)
        self.go_main_menu()

    def on_escape(self):
        if self.state == "play":
            self.build_pause_menu()
            self.state = "pause"
        elif self.state == "pause":
            if not self.menu.pop():
                self.state = "play"
        elif self.state == "main_menu":
            if not self.menu.pop():
                self.quit_game()
        elif self.state == "victory":
            self.go_main_menu()

    def run(self):
        while True:
            dt = self.clock.tick(FPS) / 1000.0
            self.just = set()
            for event in pygame.event.get():
                if event.type == pygame.QUIT:
                    self.quit_game()
                if event.type == pygame.KEYDOWN:
                    self.just.add(event.key)
                    if self.menu.capture:
                        self.set_binding(self.menu.capture, event.key)
                        self.menu.capture = None
                        continue
                    if event.key == pygame.K_ESCAPE:
                        self.on_escape()
                    elif event.key == pygame.K_RETURN:
                        if self.state == "victory":
                            self.start_new_game()
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

        if self.state in ("main_menu", "pause"):
            self.menu.update(self.just)
            return
        if self.state == "died":
            self.death_timer -= dt
            self.particles.update(dt)
            if self.death_timer <= 0:
                self.player.respawn()
                self.boss.reset()
                self.state = "play"
                self.fade = 1.0
                self.message(i18n.t("respawned"), ORANGE, 1.6)
            return
        if self.state == "victory":
            self.particles.update(dt)
            return

        t = i18n.t
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
            if keys[self.cfg["bindings"]["attack"]] and not b.dead:
                if b.state == "staggered" and p.center_dist(b) < 130:
                    r = p.start_riposte()
                else:
                    r = p.start_attack()
                if r:
                    self.message(t(r), RED)
            elif keys[self.cfg["bindings"]["roll"]]:
                r = p.start_roll()
                if r:
                    self.message(t(r), RED)
            elif keys[self.cfg["bindings"]["parry"]]:
                r = p.start_parry()
                if r:
                    self.message(t(r), RED)
            elif keys[self.cfg["bindings"]["estus"]]:
                r = p.start_drink()
                if r:
                    self.message(t(r), RED)
            elif keys[self.cfg["bindings"]["interact"]] and self.bonfire.in_range(p.rect.center):
                if b.aggro and not b.dead:
                    self.message(t("cannot_rest"), RED, 1.4)
                else:
                    p.hp = PLAYER_MAX_HP
                    p.estus = ESTUS_MAX
                    b.reset()
                    self.save_current()
                    self.fade = 1.0
                    self.message(t("bonfire_rested"), ORANGE, 1.6)
                    self.message(t("game_saved"), ORANGE, 1.6)

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
                    self.message(t("riposte_msg"), YELLOW, 1.2)

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
                        self.message(t("parry_msg"), YELLOW, 1.2)
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
            config.delete_save()
            self.state = "victory"
            self.message(t("victory"), YELLOW, 2.0)

    def draw(self):
        canvas = self.canvas
        canvas.fill(BG)
        world = pygame.Surface((WIDTH, HEIGHT))
        world.blit(self.floor, (0, 0))
        pygame.draw.rect(world, WALL, (0, 0, WIDTH, HEIGHT), 6)
        if self.state in ("play", "pause", "died", "victory"):
            self.bonfire.draw(world)
            self.boss.draw(world)
            if not self.player.dead:
                self.player.draw(world)
            self.particles.draw(world)
            if self.state == "play" and self.can_rest():
                hint = i18n.t("rest_hint", key=config.key_name(self.cfg["bindings"]["interact"]))
                img = self.fonts["small"].render(hint, True, (200, 200, 210))
                world.blit(img, img.get_rect(center=(self.bonfire.pos[0], self.bonfire.pos[1] - 80)))
        if self.shake > 0.02:
            ox = random.randint(-int(self.shake * 14), int(self.shake * 14))
            oy = random.randint(-int(self.shake * 14), int(self.shake * 14))
            canvas.blit(world, (ox, oy))
        else:
            canvas.blit(world, (0, 0))

        if self.state in ("play", "pause", "died"):
            self.draw_hud(canvas)
            self.draw_banner(canvas)
            self.draw_messages(canvas)
        if self.state == "died":
            a = max(0.0, min(1.0, (2.4 - self.death_timer) / 0.6))
            img = self.fonts["huge"].render("YOU DIED", True, (190, 20, 20))
            img.set_alpha(int(255 * a))
            canvas.blit(img, img.get_rect(center=(WIDTH // 2, 250)))
        elif self.state == "victory":
            self.draw_victory(canvas)
        elif self.state == "main_menu":
            self.draw_main_menu(canvas)
        elif self.state == "pause":
            self.draw_pause_overlay(canvas)

        if self.fade > 0:
            ov = pygame.Surface((WIDTH, HEIGHT), pygame.SRCALPHA)
            ov.fill((0, 0, 0, int(255 * min(1.0, self.fade))))
            canvas.blit(ov, (0, 0))

        win = self.screen
        if win.get_size() == (WIDTH, HEIGHT):
            win.blit(canvas, (0, 0))
        else:
            pygame.transform.smoothscale(canvas, win.get_size(), win)

    def can_rest(self):
        p, b = self.player, self.boss
        if p.dead or (b.aggro and not b.dead):
            return False
        return self.bonfire.in_range(p.rect.center)

    def draw_main_menu(self, canvas):
        dark = pygame.Surface((WIDTH, HEIGHT), pygame.SRCALPHA)
        dark.fill((0, 0, 0, 190))
        canvas.blit(dark, (0, 0))
        if not self.menu.stack:
            sub = self.fonts["normal"].render(i18n.t("game_subtitle"), True, GRAY)
            canvas.blit(sub, sub.get_rect(center=(WIDTH // 2, 215)))
        self.menu.draw(canvas, self.fonts)
        if not self.menu.stack:
            self.draw_control_hints(canvas)

    def draw_pause_overlay(self, canvas):
        dark = pygame.Surface((WIDTH, HEIGHT), pygame.SRCALPHA)
        dark.fill((0, 0, 0, 170))
        canvas.blit(dark, (0, 0))
        self.menu.draw(canvas, self.fonts)

    def draw_control_hints(self, surf):
        b = self.cfg["bindings"]
        kn = config.key_name
        lines = [
            f"{kn(b['move_up'])} {kn(b['move_down'])} {kn(b['move_left'])} {kn(b['move_right'])}   {i18n.t('move')}",
            f"{kn(b['attack'])}   {i18n.t('attack')}",
            f"{kn(b['roll'])}   {i18n.t('roll')}",
            f"{kn(b['parry'])}   {i18n.t('parry')}",
            f"{kn(b['estus'])}   {i18n.t('estus')}",
            f"{kn(b['interact'])}   {i18n.t('rest')}",
            f"ESC   {i18n.t('pause')}",
        ]
        y = 540
        for line in lines:
            img = self.fonts["small"].render(line, True, (140, 140, 150))
            surf.blit(img, img.get_rect(center=(WIDTH // 2, y)))
            y += 24

    def draw_hud(self, surf):
        label = self.fonts["small"].render(i18n.t("unkindled"), True, GRAY)
        surf.blit(label, (30, 8))
        draw_bar(surf, 30, 28, 260, 16, self.player.hp / PLAYER_MAX_HP, RED, DARK_RED)
        draw_bar(surf, 30, 50, 220, 10, self.player.stamina / PLAYER_MAX_STAMINA, GREEN, DARK_GREEN)
        for i in range(ESTUS_MAX):
            self.draw_flask(surf, 30 + i * 32, 68, i < self.player.estus)
        if self.boss.aggro and not self.boss.dead:
            w = 840
            x = (WIDTH - w) // 2
            y = HEIGHT - 58
            name = self.fonts["normal"].render(i18n.t("boss_name"), True, WHITE)
            surf.blit(name, (x, y - 26))
            draw_bar(surf, x, y, w, 18, self.boss.hp / self.boss.max_hp, (150, 20, 20), (60, 10, 10))

    def draw_flask(self, surf, x, y, filled):
        color = ORANGE if filled else (60, 60, 66)
        pygame.draw.rect(surf, color, (x, y, 16, 22), border_radius=3)
        pygame.draw.rect(surf, (200, 200, 210), (x + 5, y - 6, 6, 8), border_radius=2)

    def draw_banner(self, surf):
        bt = self.boss.aggro_banner
        if bt <= 0:
            return
        fade_in = min(1.0, (2.4 - bt) / 0.4)
        fade_out = min(1.0, bt / 0.5)
        alpha = int(255 * min(fade_in, fade_out))
        img = self.fonts["huge"].render(i18n.t("boss_name"), True, (220, 60, 40))
        img.set_alpha(alpha)
        surf.blit(img, img.get_rect(center=(WIDTH // 2, 190)))
        sub = self.fonts["big"].render(i18n.t("boss_subtitle"), True, GRAY)
        sub.set_alpha(alpha)
        surf.blit(sub, sub.get_rect(center=(WIDTH // 2, 260)))

    def draw_messages(self, surf):
        y = 310
        for text, color, dur, tm in self.messages:
            alpha = 255 if tm > 0.3 else int(255 * tm / 0.3)
            img = self.fonts["big"].render(text, True, color)
            img.set_alpha(alpha)
            surf.blit(img, img.get_rect(center=(WIDTH // 2, y)))
            y += 36

    def draw_victory(self, surf):
        v = self.fonts["huge"].render(i18n.t("victory"), True, YELLOW)
        surf.blit(v, v.get_rect(center=(WIDTH // 2, 280)))
        pr = self.fonts["big"].render(i18n.t("restart"), True, WHITE)
        surf.blit(pr, pr.get_rect(center=(WIDTH // 2, 380)))
        esc = self.fonts["small"].render(i18n.t("esc_main_menu"), True, GRAY)
        surf.blit(esc, esc.get_rect(center=(WIDTH // 2, 440)))


if __name__ == "__main__":
    Game().run()
