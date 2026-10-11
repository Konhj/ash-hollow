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
import changelog
import items as items_data

if sys.platform == "win32":
    try:
        import ctypes

        ctypes.windll.shcore.SetProcessDpiAwareness(1)
    except Exception:
        pass
elif sys.platform.startswith("linux"):
    os.environ["XMODIFIERS"] = "@im=none"

KEY_ACTIONS = [
    ("move_up", "up"),
    ("move_down", "down"),
    ("move_left", "left"),
    ("move_right", "right"),
    ("attack", "attack"),
    ("roll", "roll"),
    ("offhand", "slot_offhand"),
    ("estus", "estus"),
    ("interact", "rest"),
    ("sprint", "sprint"),
    ("inventory", "inventory"),
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
        self.columns = 1
        self.hint = None
        self.hit_rects = []
        self.font_key = "big"
        self.title_font = False
        self.warning = None

    def set(self, title, items, start_y=300, step=56, columns=1, hint=None, font_key="big", title_font=False, warning=None):
        self.title = title
        self.items = items
        self.selected = 0
        self.start_y = start_y
        self.step = step
        self.columns = columns
        self.hint = hint
        self.font_key = font_key
        self.title_font = title_font
        self.warning = warning
        self.hit_rects = []

    def push(self, title, items, start_y=300, step=56, columns=1, hint=None, font_key="big", title_font=False, warning=None):
        self.stack.append(
            (
                self.title,
                self.items,
                self.selected,
                self.start_y,
                self.step,
                self.columns,
                self.hint,
                self.font_key,
                self.title_font,
                self.warning,
            )
        )
        self.set(title, items, start_y, step, columns, hint, font_key, title_font, warning)

    def pop(self):
        if self.stack:
            (
                self.title,
                self.items,
                self.selected,
                self.start_y,
                self.step,
                self.columns,
                self.hint,
                self.font_key,
                self.title_font,
                self.warning,
            ) = self.stack.pop()
            return True
        return False

    def visible_indices(self):
        out = []
        for i, it in enumerate(self.items):
            if it.get("visible") and not it["visible"]():
                continue
            out.append(i)
        return out

    def rows(self, n):
        return (n + 1) // 2 if self.columns == 2 else n

    def update(self, just, mouse=None, clicked=False):
        if self.capture:
            return
        visible = self.visible_indices()
        n = len(visible)
        if not n:
            return
        rows = self.rows(n)

        if mouse is not None and len(self.hit_rects) == n:
            for i, r in enumerate(self.hit_rects):
                if r.collidepoint(mouse):
                    self.selected = i
                    break

        if self.columns == 2:
            if pygame.K_UP in just or pygame.K_w in just:
                self.selected = max(0, self.selected - 1)
            if pygame.K_DOWN in just or pygame.K_s in just:
                self.selected = min(n - 1, self.selected + 1)
            if pygame.K_LEFT in just or pygame.K_a in just:
                if self.selected >= rows:
                    self.selected -= rows
            if pygame.K_RIGHT in just or pygame.K_d in just:
                if self.selected + rows < n:
                    self.selected += rows
        else:
            if pygame.K_UP in just or pygame.K_w in just:
                self.selected = (self.selected - 1) % n
            if pygame.K_DOWN in just or pygame.K_s in just:
                self.selected = (self.selected + 1) % n

        item = self.items[visible[self.selected]]
        if self.columns == 1:
            if pygame.K_LEFT in just and item.get("adjust"):
                item["adjust"](-1)
            if pygame.K_RIGHT in just and item.get("adjust"):
                item["adjust"](1)
        if pygame.K_RETURN in just and item.get("action"):
            item["action"]()

        if clicked and mouse is not None and len(self.hit_rects) == n:
            for i, r in enumerate(self.hit_rects):
                if r.collidepoint(mouse):
                    item = self.items[visible[i]]
                    if item.get("adjust"):
                        item["adjust"](1 if mouse[0] >= r.centerx else -1)
                    elif item.get("action") and (not item.get("enabled") or item["enabled"]()):
                        item["action"]()
                    break

    def draw(self, surf, fonts, title_font=None):
        if self.title:
            title = self.title() if callable(self.title) else self.title
            tf = title_font if (self.title_font and title_font is not None) else fonts["big"]
            img = tf.render(title, True, (220, 220, 228))
            surf.blit(img, img.get_rect(center=(WIDTH // 2, 150)))
        if self.warning:
            wtext = self.warning() if callable(self.warning) else self.warning
            img = fonts["normal"].render(wtext, True, (235, 170, 80))
            surf.blit(img, img.get_rect(center=(WIDTH // 2, 240)))
        visible = self.visible_indices()
        n = len(visible)
        rows = self.rows(n)
        self.hit_rects = []
        for i, idx in enumerate(visible):
            item = self.items[idx]
            sel = i == self.selected
            col = i // rows if self.columns == 2 else 0
            row = i % rows
            if self.columns == 2:
                x = WIDTH // 2 - 300 + col * 600
            else:
                x = WIDTH // 2
            y = self.start_y + row * self.step
            if sel:
                color = YELLOW
            elif item.get("red") and item["red"]():
                color = (255, 90, 90)
            else:
                color = GRAY
            if item.get("enabled") and not item["enabled"]():
                color = (70, 70, 78)
            label = item["label"]() if callable(item["label"]) else item["label"]
            if sel:
                label = "> " + label + " <"
            img = fonts[self.font_key].render(label, True, color)
            rect = img.get_rect(center=(x, y))
            surf.blit(img, rect)
            self.hit_rects.append(rect.inflate(40, 18))
        if self.capture:
            img = fonts["small"].render(i18n.t("press_any_key"), True, YELLOW)
            surf.blit(img, img.get_rect(bottomright=(WIDTH - 24, HEIGHT - 18)))
        if self.hint:
            h = self.hint()
            if h:
                text, color = h
                img = fonts["small"].render(text, True, color)
                surf.blit(img, img.get_rect(center=(WIDTH // 2, 692)))


class Game:
    def __init__(self):
        pygame.init()
        self.cfg = config.load_config()
        i18n.set_lang(self.cfg["language"])
        self.apply_display()
        pygame.display.set_caption(f"ASH HOLLOW v{VERSION}")
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
        self.mouse_pos = (0, 0)
        self.mouse_clicked = False
        self.menu = Menu()
        self.version_rect = pygame.Rect(0, 0, 0, 0)
        self.cl_mode = None
        self.cl_sel = 0
        self.cl_scroll = 0
        self.cl_ver = None
        self.cl_lines = []
        self.cl_rects = []
        self.room = "hub"
        self.hub_banner = 0.0
        self.inv_tab = 0
        self.inv_hover = None
        self.inv_selected_slot = None
        self.inv_cells = []
        self.inv_equip_cells = []
        self.inv_tab_rects = []
        self.state = "main_menu"
        self.build_main_menu()
        self.new_game()

    def load_fonts(self):
        def font_set(name):
            path = config.resource_path(os.path.join("fonts", name))
            try:
                return {
                    "small": pygame.font.Font(path, 22),
                    "normal": pygame.font.Font(path, 30),
                    "big": pygame.font.Font(path, 54),
                    "huge": pygame.font.Font(path, 110),
                }
            except Exception:
                return {
                    "small": pygame.font.Font(None, 22),
                    "normal": pygame.font.Font(None, 30),
                    "big": pygame.font.Font(None, 54),
                    "huge": pygame.font.Font(None, 110),
                }

        self.fonts_en = font_set("eb-garamond.ttf")
        self.fonts_zh = font_set("noto-serif-sc.ttf")
        self.fonts_cjk = self.fonts_zh
        self.fonts = self.fonts_en if self.cfg["language"] == "en" else self.fonts_zh
        self.title_font = None
        for name in ("agmena.ttf", "agmena.otf", "cinzel.ttf"):
            path = config.resource_path(os.path.join("fonts", name))
            if os.path.exists(path):
                try:
                    self.title_font = pygame.font.Font(path, 72)
                    break
                except Exception:
                    pass
        if self.title_font is None:
            self.title_font = self.fonts["big"]

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
        self.player = Player(640, 560, self.cfg["bindings"])
        self.boss = Boss(1080, 400)
        self.bonfire = Bonfire(640, 430)
        self.room = "hub"
        self.hub_banner = 2.6
        self.messages.clear()
        self.particles.items.clear()
        self.shake = 0.0
        self.fade = 1.0
        self.death_timer = 0.0

    def enter_arena(self):
        self.room = "arena"
        p = self.player
        p.x, p.y = 150.0, 360.0
        p.facing = pygame.Vector2(1, 0)
        p.sync_rect()

    def exit_to_hub(self):
        self.room = "hub"
        self.boss.reset()
        p = self.player
        p.x, p.y = 1120.0, 400.0
        p.facing = pygame.Vector2(-1, 0)
        p.sync_rect()

    def message(self, text, color=WHITE, dur=1.4, size="big"):
        for m in self.messages:
            if m[0] == text:
                return
        self.messages.append([text, color, dur, dur, size])

    def on_player_hit(self):
        p = self.player
        self.shake = max(self.shake, 0.7)
        self.particles.spawn(p.rect.centerx, p.rect.centery, (200, 40, 40), 12, 3.5, 0.4)

    def disable_ime(self):
        if sys.platform == "win32":
            try:
                import ctypes

                hwnd = pygame.display.get_wm_info()["window"]
                ctypes.windll.imm32.ImmAssociateContext(hwnd, None)
            except Exception:
                pass
        self.stop_ime()

    def stop_ime(self):
        try:
            pygame.key.stop_text_input()
        except Exception:
            pass

    def apply_display(self):
        mode = self.cfg["display_mode"]
        if mode == 0:
            size, flags = RESOLUTIONS[self.cfg["resolution"]], pygame.RESIZABLE
        elif mode == 1:
            size, flags = (0, 0), pygame.NOFRAME
        else:
            size, flags = RESOLUTIONS[self.cfg["resolution"]], pygame.FULLSCREEN
        self.screen = pygame.display.set_mode(size, flags)
        if self.screen.get_width() < 64 or self.screen.get_height() < 64:
            self.screen = pygame.display.set_mode(RESOLUTIONS[self.cfg["resolution"]], pygame.NOFRAME)
        self.disable_ime()

    def adjust_resolution(self, delta):
        n = len(RESOLUTIONS)
        self.cfg["resolution"] = (self.cfg["resolution"] + delta) % n
        self.apply_display()
        config.save_config(self.cfg)

    def adjust_display_mode(self, delta):
        self.cfg["display_mode"] = (self.cfg["display_mode"] + delta) % 3
        self.apply_display()
        config.save_config(self.cfg)

    def adjust_language(self, delta):
        self.cfg["language"] = "zh" if self.cfg["language"] == "en" else "en"
        i18n.set_lang(self.cfg["language"])
        self.fonts = self.fonts_en if self.cfg["language"] == "en" else self.fonts_zh
        config.save_config(self.cfg)

    def adjust_show_keys(self, delta):
        self.cfg["show_keys"] = not self.cfg["show_keys"]
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
            {"label": lambda: t("new_game"), "action": self.confirm_new_game},
            {
                "label": lambda: t("continue"),
                "action": self.load_and_continue,
                "visible": lambda: config.load_save() is not None,
            },
            {"label": lambda: t("settings"), "action": self.build_settings_menu},
            {
                "label": lambda: t("quit"),
                "action": lambda: self.build_confirm("confirm_quit", self.quit_game),
            },
        ]
        self.menu.set(lambda: t("game_title"), items, title_font=True)

    def build_pause_menu(self):
        t = i18n.t
        items = [
            {"label": lambda: t("resume"), "action": self.resume_game},
            {"label": lambda: t("settings"), "action": self.build_settings_menu},
            {
                "label": lambda: t("save_and_exit"),
                "action": lambda: self.build_confirm("confirm_save_exit", self.save_and_exit),
            },
        ]
        self.menu.set(lambda: t("pause"), items)

    def build_confirm(self, question_key, yes_action, warning_key=None):
        t = i18n.t
        items = [
            {"label": lambda: t("yes"), "action": yes_action},
            {"label": lambda: t("no"), "action": lambda: self.menu.pop()},
        ]
        warning = (lambda: t(warning_key)) if warning_key else None
        self.menu.push(lambda: t(question_key), items, start_y=330, step=60, warning=warning)

    def confirm_new_game(self):
        if config.load_save() is None:
            self.start_new_game()
        else:
            self.build_confirm("confirm_new_game", self.start_new_game, "overwrite_warning")

    def binding_conflicts(self):
        counts = {}
        for v in self.cfg["bindings"].values():
            counts[v] = counts.get(v, 0) + 1
        return {k for k, c in counts.items() if c > 1}

    def build_settings_menu(self):
        t = i18n.t

        def res_label():
            w, h = RESOLUTIONS[self.cfg["resolution"]]
            return f"{t('resolution')}: {w} x {h}"

        def display_label():
            mode = ("mode_windowed", "mode_borderless", "mode_fullscreen")[self.cfg["display_mode"]]
            return f"{t('display')}: {t(mode)}"

        def lang_label():
            cur = "lang_zh" if self.cfg["language"] == "zh" else "lang_en"
            return f"{t('language')}: {t(cur)}"

        def keys_label():
            return f"{t('show_keys')}: {t('on') if self.cfg['show_keys'] else t('off')}"

        items = [
            {"label": display_label, "adjust": self.adjust_display_mode},
            {"label": res_label, "adjust": self.adjust_resolution},
            {"label": lang_label, "adjust": self.adjust_language},
            {"label": lambda: t("key_bindings"), "action": self.build_key_menu},
            {"label": keys_label, "adjust": self.adjust_show_keys},
            {"label": lambda: t("back"), "action": lambda: self.menu.pop()},
        ]
        self.menu.push(lambda: t("settings"), items, hint=lambda: (t("adjust_hint"), GRAY))

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

            def red(action=action):
                return self.cfg["bindings"][action] in self.binding_conflicts()

            items.append({"label": mk, "action": act, "red": red})
        items.append({"label": lambda: t("back"), "action": lambda: self.menu.pop()})

        def conflict_hint():
            if self.binding_conflicts():
                return (t("key_conflict"), (255, 90, 90))
            return None

        self.menu.push(lambda: t("key_bindings"), items, start_y=250, step=60, columns=2, hint=conflict_hint, font_key="normal")

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
                "version": 3,
                "room": self.room,
                "player": {"x": p.x, "y": p.y, "hp": p.hp, "estus": p.estus},
                "boss": {"x": b.x, "y": b.y, "hp": b.hp, "aggro": b.aggro},
                "inventory": list(p.inventory),
                "equipment": dict(p.equipment),
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
        p.estus = min(ESTUS_MAX, max(0, int(pd.get("estus", p.estus))))
        p.sync_rect()
        inv = data.get("inventory")
        if isinstance(inv, list):
            mapped = [items_data.ITEM_ALIASES.get(i, i) for i in inv]
            valid = [m for m in mapped if items_data.get(m)]
            if valid:
                p.inventory = valid
        eq = data.get("equipment")
        if isinstance(eq, dict):
            for k in p.equipment:
                v = eq.get(k)
                v = items_data.ITEM_ALIASES.get(v, v)
                p.equipment[k] = v if v and items_data.get(v) else None
        p._prev_max_hp = p.max_hp()
        p.hp = min(p.max_hp(), max(1, int(pd.get("hp", p.max_hp()))))
        b.x = bd.get("x", b.x)
        b.y = bd.get("y", b.y)
        b.hp = min(b.max_hp, max(1, int(bd.get("hp", b.hp))))
        b.aggro = bool(bd.get("aggro", False))
        b.sync_rect()
        self.room = data.get("room", "hub")
        if self.room not in ("hub", "arena"):
            self.room = "hub"
        if self.room == "hub":
            self.hub_banner = 1.6
        self.state = "play"
        self.fade = 1.0

    def save_and_exit(self):
        self.save_current()
        self.message(i18n.t("game_saved"), ORANGE, 1.6)
        self.go_main_menu()

    def on_escape(self):
        if self.state == "main_menu" and self.cl_mode:
            self.cl_back()
            return
        if self.state == "inventory":
            self.state = "play"
            return
        if self.state == "play":
            self.build_pause_menu()
            self.state = "pause"
        elif self.state == "pause":
            if not self.menu.pop():
                self.state = "play"
        elif self.state == "main_menu":
            self.menu.pop()
        elif self.state == "victory":
            self.go_main_menu()

    def run(self):
        while True:
            dt = self.clock.tick(FPS) / 1000.0
            self.just = set()
            for event in pygame.event.get():
                if event.type == pygame.QUIT:
                    self.quit_game()
                elif event.type == pygame.KEYDOWN:
                    if self.menu.capture:
                        self.set_binding(self.menu.capture, event.key)
                        self.menu.capture = None
                        continue
                    self.just.add(event.key)
                    if event.key == pygame.K_ESCAPE:
                        self.on_escape()
                    elif event.key == pygame.K_RETURN:
                        if self.state == "victory":
                            self.start_new_game()
                elif event.type == pygame.MOUSEMOTION:
                    self.mouse_pos = self.to_canvas(event.pos)
                elif event.type == pygame.MOUSEWHEEL:
                    if self.state == "main_menu" and self.cl_mode:
                        self.cl_scroll -= event.y * 64
                elif event.type == pygame.MOUSEBUTTONDOWN:
                    if event.button == 1:
                        self.mouse_pos = self.to_canvas(event.pos)
                        self.mouse_clicked = True
                    elif event.button == 3:
                        if self.state == "inventory":
                            self.inventory_right_click()
                        elif not self.menu.capture and self.state in ("main_menu", "pause"):
                            self.on_escape()
            self.update(dt)
            self.draw()
            pygame.display.flip()

    def update(self, dt, keys=None):
        if keys is None:
            keys = pygame.key.get_pressed()
        self.stop_ime()
        self.time += dt
        self.fade = max(0.0, self.fade - dt * 1.5)
        self.shake = max(0.0, self.shake - dt * 2.2)
        for m in self.messages:
            m[3] -= dt
        self.messages = [m for m in self.messages if m[3] > 0]

        if self.state in ("main_menu", "pause"):
            if self.state == "main_menu" and self.cl_mode:
                self.update_changelog()
            else:
                self.menu.update(self.just, self.mouse_pos, self.mouse_clicked)
            if (
                self.state == "main_menu"
                and not self.cl_mode
                and not self.menu.capture
                and self.mouse_clicked
                and self.version_rect.collidepoint(self.mouse_pos)
            ):
                self.open_changelog()
            self.mouse_clicked = False
            return
        if self.state == "died":
            self.death_timer -= dt
            self.particles.update(dt)
            if self.death_timer <= 0:
                self.player.respawn()
                self.boss.reset()
                self.room = "hub"
                self.hub_banner = 2.6
                self.state = "play"
                self.fade = 1.0
            return
        if self.state == "victory":
            self.particles.update(dt)
            return
        if self.state == "inventory":
            if self.cfg["bindings"]["inventory"] in self.just:
                self.state = "play"
                return
            self.update_inventory()
            self.mouse_clicked = False
            return

        if self.cfg["bindings"]["inventory"] in self.just:
            self.state = "inventory"
            self.inv_hover = None
            return

        t = i18n.t
        p = self.player
        b = self.boss
        self.hub_banner = max(0.0, self.hub_banner - dt)
        self.bonfire.update(dt)
        p.update(dt, keys)
        if p.just_drank:
            p.just_drank = False
            self.particles.spawn(p.rect.centerx, p.rect.centery, (120, 220, 120), 10, 2.2, 0.5)
        if self.room == "arena":
            b.update(dt, p)
        self.particles.update(dt)

        if self.room == "hub":
            p.x = min(p.x, 1175.0)
            p.clamp()
            if random.random() < 0.35:
                bx, by = self.bonfire.pos
                self.particles.spawn(bx + random.uniform(-8, 8), by - 10, (255, 170, 50), 1, 1.2, 0.8)
        else:
            p.x = max(p.x, 105.0)
            p.clamp()
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
            if self.room == "arena" and keys[self.cfg["bindings"]["attack"]] and not b.dead:
                if b.state == "staggered" and p.center_dist(b) < 130:
                    r = p.start_riposte()
                else:
                    r = p.start_attack()
                if r:
                    self.message(t(r), RED, 1.4, "small")
            elif keys[self.cfg["bindings"]["roll"]]:
                r = p.start_roll()
                if r:
                    self.message(t(r), RED, 1.4, "small")
            elif keys[self.cfg["bindings"]["offhand"]]:
                if p.shield() is not None:
                    p.start_guard()
                else:
                    r = p.start_offhand_attack()
                    if r:
                        self.message(t(r), RED, 1.4, "small")
            elif keys[self.cfg["bindings"]["estus"]]:
                r = p.start_drink()
                if r:
                    self.message(t(r), RED, 1.4, "small")
            elif keys[self.cfg["bindings"]["interact"]]:
                if self.room == "hub" and self.bonfire.in_range(p.rect.center):
                    if b.aggro and not b.dead:
                        self.message(t("cannot_rest"), RED, 1.4)
                    else:
                        p.hp = p.max_hp()
                        p.estus = ESTUS_MAX
                        b.reset()
                        self.save_current()
                        self.fade = 1.0
                        self.message(t("game_saved"), ORANGE, 1.6)
                elif self.room == "hub" and p.rect.right > 1150 and 200 < p.rect.centery < 520:
                    self.enter_arena()
                    self.fade = 0.8
                elif self.room == "arena" and self.boss.dead and p.rect.left < 130 and 200 < p.rect.centery < 520:
                    self.exit_to_hub()
                    self.fade = 0.8

        if (
            self.room == "arena"
            and p.state == "attack"
            and not p.attack_landed
            and p.attack_active()
            and not b.dead
        ):
            hb = p.get_attack_hitbox()
            if hb.colliderect(b.rect):
                p.attack_landed = True
                b.take_damage(p.current_attack_damage())
                self.particles.spawn(hb.centerx, hb.centery, (255, 220, 120), 6, 3.0, 0.3)

        if self.room == "arena" and p.state == "riposte" and not p.riposte_done:
            if (RIPOSTE_TIME - p.state_timer) >= 0.25:
                p.riposte_done = True
                if not b.dead and p.center_dist(b) < 160:
                    b.take_damage(p.attack_damage() * 5)
                    b.state = "recover"
                    b.state_timer = 0.8
                    self.shake = 1.2
                    self.particles.spawn(b.rect.centerx, b.rect.centery, (255, 200, 60), 24, 5.0, 0.6)
                    self.message(t("riposte_msg"), YELLOW, 1.2)

        if self.room == "arena" and b.state == "attack" and not b.hit_landed:
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
            elif b.attack_kind == "blast":
                if not b.blast_landed:
                    b.blast_landed = True
                    self.shake = 1.6
                    self.particles.spawn(b.rect.centerx, b.rect.centery, (255, 70, 60), 40, 6.0, 0.6)
                    if p.center_dist(b) <= BLAST_RADIUS:
                        if p.take_damage(BLAST_DAMAGE, b.dir):
                            self.on_player_hit()

        if self.room == "arena" and b.dead and b.state_timer <= 0:
            config.delete_save()
            self.state = "victory"
            self.message(t("victory"), YELLOW, 2.0)

    def draw(self):
        canvas = self.canvas
        canvas.fill(BG)
        world = pygame.Surface((WIDTH, HEIGHT))
        world.blit(self.floor, (0, 0))
        pygame.draw.rect(world, WALL, (0, 0, WIDTH, HEIGHT), 6)
        if self.state in ("play", "pause", "died", "victory", "inventory"):
            if self.room == "hub":
                self.bonfire.draw(world)
                self.draw_fog_gate(world, 1244)
            else:
                self.draw_fog_gate(world, 0, sealed=not self.boss.dead)
                self.boss.draw(world)
            if not self.player.dead:
                self.player.draw(world)
            self.particles.draw(world)
            if self.state == "play":
                p = self.player
                if self.room == "hub":
                    if self.can_rest():
                        self.draw_interact_hint(world, self.bonfire.pos[0], self.bonfire.pos[1] - 95)
                    if p.rect.right > 1150 and 200 < p.rect.centery < 520:
                        self.draw_interact_hint(world, 1195, 370)
                else:
                    if p.rect.left < 130 and 200 < p.rect.centery < 520 and self.boss.dead:
                        self.draw_interact_hint(world, 85, 370)
        if self.shake > 0.02:
            ox = random.randint(-int(self.shake * 14), int(self.shake * 14))
            oy = random.randint(-int(self.shake * 14), int(self.shake * 14))
            canvas.blit(world, (ox, oy))
        else:
            canvas.blit(world, (0, 0))

        if self.state in ("play", "pause", "died", "inventory"):
            self.draw_hud(canvas)
            if self.room == "hub":
                self.draw_hub_banner(canvas)
            else:
                self.draw_banner(canvas)
            self.draw_messages(canvas)
            if self.cfg["show_keys"]:
                self.draw_key_hints(canvas)
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
        elif self.state == "inventory":
            self.draw_inventory(canvas)

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
        if self.room != "hub" or p.dead or (b.aggro and not b.dead):
            return False
        return self.bonfire.in_range(p.rect.center)

    def slot_accepts(self, key, item_id):
        item = items_data.get(item_id)
        if not item:
            return False
        itype = item["type"]
        islot = item["slot"]
        if key == "mainhand":
            return itype == "weapon" and islot == "mainhand"
        if key == "offhand":
            return itype == "weapon" and islot == "offhand"
        if key == "armor":
            return itype == "armor"
        if key.startswith("talisman"):
            return itype == "talisman" and item_id not in self.player.equipment.values()
        return False

    def equip_into(self, key, item_id):
        if not self.slot_accepts(key, item_id):
            return
        self.player.equipment[key] = item_id
        self.player.refresh_max_hp()
        self.save_current()

    def unequip_slot(self, key):
        self.player.equipment[key] = None
        self.player.refresh_max_hp()
        self.save_current()

    def inventory_right_click(self):
        mp = self.mouse_pos
        for slot in self.inv_equip_cells:
            if slot["rect"].collidepoint(mp):
                if self.player.equipment.get(slot["key"]):
                    self.unequip_slot(slot["key"])
                break

    def update_inventory(self):
        mp = self.mouse_pos
        click = self.mouse_clicked
        hover = None
        if click:
            for i, r in enumerate(self.inv_tab_rects):
                if r.collidepoint(mp):
                    self.inv_tab = i
                    click = False
                    break
        if click:
            for slot in self.inv_equip_cells:
                if slot["rect"].collidepoint(mp):
                    if self.inv_selected_slot == slot["key"]:
                        self.inv_selected_slot = None
                    else:
                        self.inv_selected_slot = slot["key"]
                    click = False
                    break
        if click:
            for cell in self.inv_cells:
                if cell["rect"].collidepoint(mp):
                    if cell["item"] and self.inv_selected_slot:
                        self.equip_into(self.inv_selected_slot, cell["item"])
                        self.inv_selected_slot = None
                    break
        for cell in self.inv_cells:
            if cell["rect"].collidepoint(mp):
                hover = cell["item"]
                break
        else:
            for slot in self.inv_equip_cells:
                if slot["rect"].collidepoint(mp):
                    hover = self.player.equipment.get(slot["key"])
                    break
        self.inv_hover = hover

    def tooltip_lines(self, item_id):
        t = i18n.t
        lang = i18n.get_lang()
        item = items_data.get(item_id)
        if not item:
            return []
        type_label = {
            "weapon": t("cat_weapons"),
            "armor": t("cat_armor"),
            "talisman": t("cat_talismans"),
        }[item["type"]]
        lines = [items_data.name(item_id, lang), type_label]
        if item["type"] == "weapon":
            if item.get("subtype") == "shield":
                st = item["stats"]
                lines.append(f"{t('block')}: {int(st['defense'] * 100)}%")
                lines.append(f"{t('parry_time')}: {st['parry_time']}s")
            else:
                lines.append(f"{t('damage')}: {item['stats']['damage']}")
                lines.append(f"{t('speed')}: {item['stats']['speed']}")
            slot_key = "slot_mainhand" if item["slot"] == "mainhand" else "slot_offhand"
            lines.append(t(slot_key))
        elif item["type"] == "armor":
            lines.append(f"{t('defense')}: {item['stats']['defense']}")
        elif item["type"] == "talisman":
            for k, v in item.get("effect", {}).items():
                lines.append(f"{t(k)} +{v}")
        desc_text = items_data.desc(item_id, lang)
        if desc_text:
            lines.append("")
            lines += self.wrap_text(desc_text, self.fonts["small"], 250)
        return lines

    def fit_text(self, text, font, max_w, color):
        img = font.render(text, True, color)
        if img.get_width() > max_w:
            scale = max_w / img.get_width()
            img = pygame.transform.smoothscale(
                img, (max_w, max(8, int(img.get_height() * scale)))
            )
        return img

    def draw_item_icon(self, surf, item_id, cx, cy):
        item = items_data.get(item_id)
        if not item:
            return
        if item.get("subtype") == "shield":
            pygame.draw.circle(surf, (70, 70, 82), (cx, cy), 15)
            pygame.draw.circle(surf, (210, 210, 220), (cx, cy), 15, 3)
            pygame.draw.circle(surf, (150, 150, 165), (cx, cy), 7)
            pygame.draw.circle(surf, (110, 110, 122), (cx, cy), 3)
        elif item["type"] == "weapon":
            pygame.draw.line(surf, (210, 210, 220), (cx - 18, cy + 16), (cx + 18, cy - 16), 5)
            pygame.draw.line(surf, (140, 110, 60), (cx - 28, cy + 22), (cx - 12, cy + 12), 6)
        elif item["type"] == "armor":
            pygame.draw.rect(surf, (150, 150, 165), (cx - 16, cy - 18, 32, 36), border_radius=6)
            pygame.draw.rect(surf, (90, 90, 100), (cx - 8, cy - 10, 16, 20), border_radius=3)
        elif item["type"] == "talisman":
            pygame.draw.circle(surf, (235, 170, 70), (cx, cy), 13, 3)
            pygame.draw.circle(surf, (235, 170, 70), (cx, cy), 4)

    def draw_character_model(self, surf, cx, cy):
        pygame.draw.ellipse(surf, (20, 20, 26), (cx - 55, cy + 78, 110, 22))
        pygame.draw.rect(surf, (68, 68, 80), (cx - 32, cy - 20, 64, 100), border_radius=8)
        pygame.draw.rect(surf, (48, 48, 56), (cx - 26, cy - 12, 52, 60), border_radius=6)
        pygame.draw.circle(surf, (150, 140, 128), (cx, cy - 38), 22)
        pygame.draw.rect(surf, (60, 60, 70), (cx - 28, cy - 46, 56, 14), border_radius=4)
        pygame.draw.line(surf, (200, 200, 214), (cx + 40, cy), (cx + 88, cy - 40), 7)
        pygame.draw.line(surf, (140, 110, 60), (cx + 30, cy + 12), (cx + 46, cy - 2), 6)

    def draw_inventory(self, canvas):
        t = i18n.t
        lang = i18n.get_lang()
        f = self.fonts
        p = self.player
        dark = pygame.Surface((WIDTH, HEIGHT), pygame.SRCALPHA)
        dark.fill((0, 0, 0, 200))
        canvas.blit(dark, (0, 0))

        left = pygame.Surface((400, 640), pygame.SRCALPHA)
        left.fill((22, 22, 30, 225))
        canvas.blit(left, (30, 40))
        pygame.draw.rect(canvas, (90, 90, 100), (30, 40, 400, 640), 2)

        title = f["normal"].render(t("inventory"), True, (220, 220, 228))
        canvas.blit(title, title.get_rect(center=(230, 70)))

        self.draw_character_model(canvas, 230, 165)
        eq_name = items_data.name(p.equipment["mainhand"], lang) if p.equipment["mainhand"] else t("empty")
        img = self.fit_text(eq_name, f["small"], 200, (200, 200, 210))
        canvas.blit(img, img.get_rect(center=(230, 288)))

        slot_defs = [
            ("mainhand", t("slot_mainhand")),
            ("offhand", t("slot_offhand")),
            ("armor", t("slot_armor")),
            ("talisman1", t("slot_talisman")),
            ("talisman2", t("slot_talisman")),
            ("talisman3", t("slot_talisman")),
        ]
        self.inv_equip_cells = []
        for i, (key, label) in enumerate(slot_defs):
            col, row = i % 3, i // 3
            cx = 109 + col * 121
            cy = 40 + 335 + row * 121
            rect = pygame.Rect(cx - 52, cy - 52, 104, 104)
            pygame.draw.rect(canvas, (35, 35, 44), rect, border_radius=4)
            if key == self.inv_selected_slot:
                pygame.draw.rect(canvas, (255, 255, 255), rect, 3, border_radius=4)
            else:
                pygame.draw.rect(canvas, (110, 110, 122), rect, 2, border_radius=4)
            lab = f["small"].render(label, True, (150, 150, 160))
            canvas.blit(lab, lab.get_rect(center=(cx, cy - 36)))
            item_id = p.equipment[key]
            if item_id:
                self.draw_item_icon(canvas, item_id, cx, cy + 6)
                nm = self.fit_text(items_data.name(item_id, lang), f["small"], 92, (225, 225, 232))
                canvas.blit(nm, nm.get_rect(center=(cx, cy + 34)))
            else:
                dash = f["normal"].render("-", True, (90, 90, 98))
                canvas.blit(dash, dash.get_rect(center=(cx, cy + 8)))
            self.inv_equip_cells.append({"rect": rect, "key": key, "item": item_id})

        if self.inv_selected_slot:
            hint = f["small"].render(t("select_item_hint"), True, YELLOW)
            canvas.blit(hint, hint.get_rect(center=(230, 660)))

        right = pygame.Surface((790, 640), pygame.SRCALPHA)
        right.fill((22, 22, 30, 225))
        canvas.blit(right, (460, 40))
        pygame.draw.rect(canvas, (90, 90, 100), (460, 40, 790, 640), 2)

        tabs = [t("cat_all"), t("cat_weapons"), t("cat_armor"), t("cat_talismans")]
        self.inv_tab_rects = []
        for i, label in enumerate(tabs):
            rect = pygame.Rect(480 + i * 158, 60, 150, 36)
            sel = i == self.inv_tab
            pygame.draw.rect(canvas, (60, 60, 74) if sel else (35, 35, 44), rect, border_radius=4)
            pygame.draw.rect(canvas, YELLOW if sel else (110, 110, 122), rect, 2, border_radius=4)
            img = f["small"].render(label, True, YELLOW if sel else (200, 200, 210))
            canvas.blit(img, img.get_rect(center=rect.center))
            self.inv_tab_rects.append(rect)

        cat_type = [None, "weapon", "armor", "talisman"][self.inv_tab]
        owned = [i for i in p.inventory if items_data.get(i)]
        if cat_type:
            owned = [i for i in owned if items_data.get(i)["type"] == cat_type]

        self.inv_cells = []
        for idx, item_id in enumerate(owned):
            col, row = idx % 6, idx // 6
            cx = 486 + col * 116 + 52
            cy = 130 + row * 116 + 52
            rect = pygame.Rect(cx - 52, cy - 52, 104, 104)
            equipped = item_id in p.equipment.values()
            compat = self.inv_selected_slot is not None and self.slot_accepts(self.inv_selected_slot, item_id)
            pygame.draw.rect(canvas, (35, 35, 44), rect, border_radius=4)
            if compat:
                pygame.draw.rect(canvas, (255, 255, 255), rect, 3, border_radius=4)
            elif equipped:
                pygame.draw.rect(canvas, YELLOW, rect, 2, border_radius=4)
            else:
                pygame.draw.rect(canvas, (110, 110, 122), rect, 2, border_radius=4)
            self.draw_item_icon(canvas, item_id, cx, cy - 4)
            nm = self.fit_text(items_data.name(item_id, lang), f["small"], 92, (225, 225, 232))
            canvas.blit(nm, nm.get_rect(center=(cx, cy + 34)))
            self.inv_cells.append({"rect": rect, "item": item_id, "compat": compat})

        if self.inv_hover:
            lines = self.tooltip_lines(self.inv_hover)
            if lines:
                pad = 10
                widths = [f["small"].render(l, True, (255, 255, 255)).get_width() for l in lines]
                w = min(300, max(widths) + pad * 2)
                h = len(lines) * 24 + pad * 2
                mx, my = self.mouse_pos
                tx, ty = mx + 20, my + 20
                if tx + w > WIDTH - 10:
                    tx = mx - w - 20
                if ty + h > HEIGHT - 10:
                    ty = my - h - 20
                box = pygame.Surface((w, h), pygame.SRCALPHA)
                box.fill((10, 10, 15, 238))
                pygame.draw.rect(box, (160, 160, 172), (0, 0, w, h), 1)
                y = pad
                for i, line in enumerate(lines):
                    if line == "":
                        y += 8
                        continue
                    color = YELLOW if i == 0 else (205, 205, 213)
                    img = f["small"].render(line, True, color)
                    box.blit(img, (pad, y))
                    y += 24
                canvas.blit(box, (tx, ty))

    def draw_key_hints(self, surf):
        b = self.cfg["bindings"]
        kn = config.key_name
        t = i18n.t
        lines = [
            f"{kn(b['attack'])}  {t('attack')}",
            f"{kn(b['roll'])}  {t('roll')}",
            f"{kn(b['offhand'])}  {t('slot_offhand')}",
            f"{kn(b['estus'])}  {t('estus')}",
            f"{kn(b['interact'])}  {t('rest')}",
            f"{kn(b['sprint'])}  {t('sprint')}",
            f"{kn(b['inventory'])}  {t('inventory')}",
        ]
        font = self.fonts["small"]
        imgs = [font.render(line, True, (195, 195, 205)) for line in lines]
        w = max(img.get_width() for img in imgs) + 20
        h = len(imgs) * 22 + 14
        panel = pygame.Surface((w, h), pygame.SRCALPHA)
        panel.fill((10, 10, 14, 150))
        pygame.draw.rect(panel, (90, 90, 100), (0, 0, w, h), 1)
        surf.blit(panel, (WIDTH - w - 12, 12))
        y = 20
        for img in imgs:
            surf.blit(img, (WIDTH - w + 6, y))
            y += 22

    def draw_interact_hint(self, surf, x, y):
        key = config.key_name(self.cfg["bindings"]["interact"])
        img = self.fonts["small"].render(key, True, (235, 235, 240))
        rect = pygame.Rect(0, 0, max(30, img.get_width() + 16), img.get_height() + 10)
        rect.center = (x, y)
        pygame.draw.rect(surf, (14, 14, 18), rect, border_radius=4)
        pygame.draw.rect(surf, (200, 200, 210), rect, 2, border_radius=4)
        surf.blit(img, img.get_rect(center=rect.center))

    def draw_fog_gate(self, surf, x, sealed=False):
        band = pygame.Surface((36, 340), pygame.SRCALPHA)
        if sealed:
            band.fill((110, 115, 130, 95))
            circle_color = (150, 155, 170, 80)
        else:
            band.fill((200, 205, 215, 60))
            circle_color = (226, 229, 236, 95)
        tm = self.time
        for i in range(6):
            yy = 30 + ((tm * (16 + i * 7) + i * 53) % 280)
            xx = 6 + 24 * (0.5 + 0.5 * math.sin(tm * 2 + i * 2.1))
            pygame.draw.circle(band, circle_color, (int(xx), int(yy)), 9)
        surf.blit(band, (x, 200))

    def draw_hub_banner(self, surf):
        bt = self.hub_banner
        if bt <= 0:
            return
        fade_in = min(1.0, (2.6 - bt) / 0.4)
        fade_out = min(1.0, bt / 0.5)
        alpha = int(255 * min(fade_in, fade_out))
        img = self.fonts["big"].render(i18n.t("firelink"), True, (200, 200, 210))
        img.set_alpha(alpha)
        surf.blit(img, img.get_rect(center=(WIDTH // 2, 190)))

    def to_canvas(self, pos):
        w, h = self.screen.get_size()
        return (pos[0] * WIDTH / w, pos[1] * HEIGHT / h)

    def open_changelog(self):
        self.cl_mode = "list"
        self.cl_sel = 0
        self.cl_scroll = 0

    def cl_back(self):
        if self.cl_mode == "detail":
            self.cl_mode = "list"
            self.cl_scroll = 0
        else:
            self.cl_mode = None

    def cl_open_detail(self, ver):
        self.cl_ver = ver
        self.cl_mode = "detail"
        self.cl_scroll = 0
        lines = []
        for entry in changelog.CHANGELOG.get(ver, []):
            lines += self.wrap_text("- " + entry, self.fonts_cjk["normal"], 860)
        self.cl_lines = lines

    def wrap_text(self, text, font, max_w):
        lines = []
        cur = ""
        for ch in text:
            if ch == "\n":
                lines.append(cur)
                cur = ""
                continue
            if cur and font.size(cur + ch)[0] > max_w:
                lines.append(cur)
                cur = ch
            else:
                cur += ch
        if cur:
            lines.append(cur)
        return lines

    def update_changelog(self):
        just = self.just
        click = self.mouse_clicked
        mp = self.mouse_pos
        vers = list(changelog.CHANGELOG.keys())
        if self.cl_mode == "list":
            if vers and len(self.cl_rects) == len(vers):
                for i, r in enumerate(self.cl_rects):
                    if r.collidepoint(mp):
                        self.cl_sel = i
                        break
            if pygame.K_UP in just or pygame.K_w in just:
                self.cl_sel = max(0, self.cl_sel - 1)
            if pygame.K_DOWN in just or pygame.K_s in just:
                self.cl_sel = min(len(vers) - 1, self.cl_sel + 1)
            if pygame.K_RETURN in just and vers:
                self.cl_open_detail(vers[self.cl_sel])
            if click and vers:
                for i, r in enumerate(self.cl_rects):
                    if r.collidepoint(mp):
                        self.cl_open_detail(vers[i])
                        break
        else:
            if pygame.K_DOWN in just or pygame.K_s in just:
                self.cl_scroll += 60
            if pygame.K_UP in just or pygame.K_w in just:
                self.cl_scroll -= 60

    def draw_main_menu(self, canvas):
        dark = pygame.Surface((WIDTH, HEIGHT), pygame.SRCALPHA)
        dark.fill((0, 0, 0, 190))
        canvas.blit(dark, (0, 0))
        if self.cl_mode:
            self.draw_changelog(canvas)
            return
        self.menu.draw(canvas, self.fonts, self.title_font)
        hover = self.version_rect.collidepoint(self.mouse_pos)
        color = YELLOW if hover else (110, 110, 120)
        ver_img = self.fonts["small"].render(f"v{VERSION}", True, color)
        self.version_rect = ver_img.get_rect(topleft=(12, HEIGHT - 28))
        canvas.blit(ver_img, self.version_rect)

    def draw_changelog(self, canvas):
        f = self.fonts_cjk
        if self.cl_mode == "list":
            title = f["big"].render(i18n.t("changelog"), True, (220, 220, 228))
            canvas.blit(title, title.get_rect(center=(WIDTH // 2, 110)))
            vers = list(changelog.CHANGELOG.keys())
            max_scroll = max(0, len(vers) * 48 - 440)
            self.cl_scroll = max(0, min(self.cl_scroll, max_scroll))
            self.cl_rects = []
            y = 180 - self.cl_scroll
            for i, v in enumerate(vers):
                sel = i == self.cl_sel
                color = YELLOW if sel else (160, 160, 170)
                label = f"> v{v} <" if sel else f"v{v}"
                img = f["normal"].render(label, True, color)
                rect = img.get_rect(center=(WIDTH // 2, y))
                if 120 < y < 650:
                    canvas.blit(img, rect)
                self.cl_rects.append(rect)
                y += 48
            hint = f["small"].render(f"ESC  {i18n.t('back')}", True, (110, 110, 120))
            canvas.blit(hint, hint.get_rect(center=(WIDTH // 2, 690)))
        else:
            head = f["big"].render(f"v{self.cl_ver}", True, YELLOW)
            canvas.blit(head, head.get_rect(center=(WIDTH // 2, 110)))
            max_scroll = max(0, len(self.cl_lines) * 36 - 500)
            self.cl_scroll = max(0, min(self.cl_scroll, max_scroll))
            y = 180 - self.cl_scroll
            for line in self.cl_lines:
                img = f["normal"].render(line, True, (205, 205, 213))
                if 120 < y < 650:
                    canvas.blit(img, img.get_rect(midleft=(WIDTH // 2 - 420, y)))
                y += 36
            hint = f["small"].render(f"ESC  {i18n.t('back')}", True, (110, 110, 120))
            canvas.blit(hint, hint.get_rect(center=(WIDTH // 2, 690)))

    def draw_pause_overlay(self, canvas):
        dark = pygame.Surface((WIDTH, HEIGHT), pygame.SRCALPHA)
        dark.fill((0, 0, 0, 170))
        canvas.blit(dark, (0, 0))
        self.menu.draw(canvas, self.fonts)

    def draw_hud(self, surf):
        label = self.fonts["small"].render(i18n.t("unkindled"), True, GRAY)
        surf.blit(label, (30, 8))
        w_hp = int(260 * (self.player.max_hp() / PLAYER_MAX_HP))
        w_st = int(220 * (self.player.max_stamina() / PLAYER_MAX_STAMINA))
        draw_bar(surf, 30, 36, w_hp, 16, self.player.hp / self.player.max_hp(), RED, DARK_RED)
        draw_bar(surf, 30, 60, w_st, 10, self.player.stamina / self.player.max_stamina(), GREEN, DARK_GREEN)
        for i in range(ESTUS_MAX):
            self.draw_flask(surf, 30 + i * 32, 84, i < self.player.estus)
        if self.room == "arena" and self.boss.aggro and not self.boss.dead:
            w = 840
            x = (WIDTH - w) // 2
            y = HEIGHT - 58
            name = self.fonts["normal"].render(i18n.t("boss_name"), True, WHITE)
            surf.blit(name, name.get_rect(bottomleft=(x, y - 8)))
            draw_bar(surf, x, y, w, 18, self.boss.hp / self.boss.max_hp, (150, 20, 20), (60, 10, 10))

    def draw_flask(self, surf, x, y, filled):
        color = ORANGE if filled else (60, 60, 66)
        pygame.draw.rect(surf, color, (x, y, 16, 22), border_radius=3)
        pygame.draw.rect(surf, (200, 200, 210), (x + 5, y - 6, 6, 8), border_radius=2)

    def draw_banner(self, surf, subtitle_key=None):
        bt = self.boss.aggro_banner
        if bt <= 0:
            return
        fade_in = min(1.0, (2.4 - bt) / 0.4)
        fade_out = min(1.0, bt / 0.5)
        alpha = int(255 * min(fade_in, fade_out))
        img = self.fonts["huge"].render(i18n.t("boss_name"), True, (220, 60, 40))
        img.set_alpha(alpha)
        surf.blit(img, img.get_rect(center=(WIDTH // 2, 190)))
        if subtitle_key:
            sub = self.fonts["big"].render(i18n.t(subtitle_key), True, GRAY)
            sub.set_alpha(alpha)
            surf.blit(sub, sub.get_rect(center=(WIDTH // 2, 330)))

    def draw_messages(self, surf):
        y = 310
        for text, color, dur, tm, size in self.messages:
            alpha = 255 if tm > 0.3 else int(255 * tm / 0.3)
            img = self.fonts[size].render(text, True, color)
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
