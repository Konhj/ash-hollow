import pygame

from common import make_game
import config
import i18n
import items as items_data
from settings import *

game = make_game()
game.fade = 0.0


def press(*keys):
    game.just = set(keys)
    game.update(1 / 60)
    game.draw()


def park_mouse():
    game.mouse_pos = (-1000, -1000)
    game.mouse_clicked = False


print("== main menu: continue hidden without save ==")
assert len(game.menu.visible_indices()) == 3

print("== new game confirm when save exists ==")
config.save_game({"version": 3, "room": "hub", "player": {"hp": 50}, "boss": {}})
game.build_main_menu()
assert len(game.menu.visible_indices()) == 4
park_mouse()
game.menu.selected = 0
press(pygame.K_RETURN)
assert game.menu.title() == i18n.t("confirm_new_game")
press(pygame.K_RETURN)
assert game.state == "play"
print("confirmed, new game started")

print("== pause menu save&exit confirm ==")
game.on_escape()
assert game.state == "pause"
park_mouse()
game.menu.selected = 2
press(pygame.K_RETURN)
assert game.menu.title() == i18n.t("confirm_save_exit")
press(pygame.K_RETURN)
assert game.state == "main_menu"
assert config.load_save() is not None

print("== quit confirm ==")
park_mouse()
game.menu.selected = 3
press(pygame.K_RETURN)
assert game.menu.title() == i18n.t("confirm_quit")
press(pygame.K_DOWN, pygame.K_RETURN)
assert game.menu.title() == i18n.t("game_title")

print("== settings: display modes + language + title refresh ==")
park_mouse()
game.menu.selected = 2
press(pygame.K_RETURN)
assert len(game.menu.visible_indices()) == 6
game.menu.selected = 0
game.just = {pygame.K_RIGHT}
game.update(1 / 60)
assert game.cfg["display_mode"] == 1
game.just = {pygame.K_RIGHT}
game.update(1 / 60)
assert game.cfg["display_mode"] == 2
game.just = {pygame.K_RIGHT}
game.update(1 / 60)
assert game.cfg["display_mode"] == 0

game.menu.selected = 2
press(pygame.K_RIGHT)
assert i18n.get_lang() == "zh"
game.draw()
assert game.menu.title() == "设置"
press(pygame.K_LEFT)
assert i18n.get_lang() == "en"

print("== key menu: two columns, conflicts, capture guard ==")
game.mouse_pos = (-1000, -1000)
game.menu.selected = 3
press(pygame.K_RETURN)
game.draw()
assert game.menu.columns == 2
rects = game.menu.hit_rects
assert len(rects) == 12
assert rects[0].centerx < WIDTH // 2 < rects[6].centerx

game.cfg["bindings"]["offhand"] = pygame.K_r
game.cfg["bindings"]["estus"] = pygame.K_r
game.build_key_menu()
game.draw()
red_items = [i for i, it in enumerate(game.menu.items) if it.get("red") and it["red"]()]
assert len(red_items) == 2, red_items
text, _ = game.menu.hint()
assert "CONFLICT" in text or "冲突" in text

game.menu.selected = 2
game.menu.capture = "move_up"
game.just = {pygame.K_w}
game.update(1 / 60)
assert game.menu.selected == 2
game.menu.capture = None
game.just = set()

print("== mouse click adjust ==")
game.just = set()
game.mouse_pos = (-1000, -1000)
game.build_settings_menu()
game.draw()
r = game.menu.hit_rects[0]
game.mouse_pos = (r.centerx + 40, r.centery)
game.mouse_clicked = True
game.update(1 / 60)
assert game.cfg["display_mode"] == 1
game.mouse_clicked = False

print("== changelog browser ==")
game.cl_mode = None
game.state = "main_menu"
game.build_main_menu()
game.draw()
game.open_changelog()
game.draw()
assert game.cl_mode == "list"
press(pygame.K_RETURN)
assert game.cl_mode == "detail"
assert game.cl_ver == "1.1.1"
game.on_escape()
assert game.cl_mode == "list"
game.on_escape()
assert game.cl_mode is None

print("ALL MENU TESTS PASSED")
