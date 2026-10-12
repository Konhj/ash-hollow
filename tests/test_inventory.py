import pygame

from common import make_game
import config
import i18n
import items as items_data
from settings import *

game = make_game()
game.fade = 0.0
game.state = "play"
p = game.player


def click(rect):
    game.mouse_pos = rect.center
    game.mouse_clicked = True
    game.update(1 / 60)
    game.just = set()
    game.mouse_clicked = False
    game.draw()


print("== inventory toggles with TAB ==")
game.just = {pygame.K_TAB}
game.update(1 / 60)
game.draw()
assert game.state == "inventory"
game.just = {pygame.K_TAB}
game.update(1 / 60)
assert game.state == "play"
game.just = {pygame.K_TAB}
game.update(1 / 60)
game.draw()
game.just = set()

print("== layout ==")
assert len(game.inv_equip_cells) == 6
assert len(game.inv_cells) >= 6
assert len(game.inv_tab_rects) == 4
for s in game.inv_equip_cells:
    assert s["rect"].left >= 30 and s["rect"].right <= 430

print("== defaults ==")
assert p.equipment["mainhand"] == "broadsword"
assert p.attack_damage() == 35
assert "small_round_shield" in p.inventory

print("== tooltip ==")
lines = game.tooltip_lines("dagger")
assert any("DAMAGE" in l for l in lines)
lines = game.tooltip_lines("small_round_shield")
assert any("50%" in l for l in lines) and any("0.22" in l for l in lines)

print("== two-step equip with highlight ==")
slot = [s for s in game.inv_equip_cells if s["key"] == "armor"][0]
click(slot["rect"])
assert game.inv_selected_slot == "armor"
leather = [c for c in game.inv_cells if c["item"] == "leather_armor"][0]
dagger = [c for c in game.inv_cells if c["item"] == "dagger"][0]
assert leather["compat"] is True and dagger["compat"] is False
click(leather["rect"])
assert p.equipment["armor"] == "leather_armor"
assert p.defense() == 8
assert game.inv_selected_slot is None

print("== no duplicate talisman ==")
slot = [s for s in game.inv_equip_cells if s["key"] == "talisman1"][0]
click(slot["rect"])
gold = [c for c in game.inv_cells if c["item"] == "golden_tree_shard"][0]
click(gold["rect"])
assert p.equipment["talisman1"] == "golden_tree_shard"
assert p.max_hp() == PLAYER_MAX_HP + 20
assert p.hp == PLAYER_MAX_HP + 20
slot2 = [s for s in game.inv_equip_cells if s["key"] == "talisman2"][0]
click(slot2["rect"])
game.draw()
gold = [c for c in game.inv_cells if c["item"] == "golden_tree_shard"][0]
assert gold["compat"] is False
click(gold["rect"])
assert p.equipment["talisman2"] is None

print("== right-click unequip ==")
game.draw()
slot = [s for s in game.inv_equip_cells if s["key"] == "talisman1"][0]
game.mouse_pos = slot["rect"].center
game.inventory_right_click()
assert p.equipment["talisman1"] is None

print("== hp preserved across save with talisman ==")
p.equipment["talisman1"] = "golden_tree_shard"
p.refresh_max_hp()
assert p.hp == 120
game.save_current()
game.load_and_continue()
p = game.player
assert p.max_hp() == 120 and p.hp == 120
p.hp = 73
game.save_current()
game.load_and_continue()
p = game.player
assert p.hp == 73 and p.max_hp() == 120

print("== old save migration ==")
config.save_game({
    "version": 3,
    "room": "hub",
    "player": {"hp": 50},
    "boss": {},
    "inventory": ["life_talisman"],
    "equipment": {"mainhand": None, "offhand": None, "armor": None,
                  "talisman1": "life_talisman", "talisman2": None, "talisman3": None},
})
game.load_and_continue()
p = game.player
assert p.equipment["talisman1"] == "golden_tree_shard"
assert "life_talisman" not in p.inventory

print("== zh tooltip ==")
i18n.set_lang("zh")
game.fonts = game.fonts_zh
lines = game.tooltip_lines("broadsword")
assert any("伤害" in l for l in lines)
assert items_data.name("golden_tree_shard", "zh") == "黄金树碎片"
i18n.set_lang("en")
game.fonts = game.fonts_en

print("ALL INVENTORY TESTS PASSED")
