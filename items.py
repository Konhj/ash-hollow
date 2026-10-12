# 物品数据框架:新增物品只需在此添加条目。
# type: weapon / armor / talisman
# slot: mainhand / offhand / armor / talisman
# weapon stats: damage, speed(倍率, 1.0 为基准)
# armor stats: defense
# talisman effect: hp_up / stamina_up (数值加成)

ITEMS = {
    "broadsword": {
        "name": {"en": "BROADSWORD", "zh": "阔剑"},
        "type": "weapon",
        "slot": "mainhand",
        "attack_style": "slash",
        "stats": {"damage": 35, "speed": 1.0, "range": 60, "stamina": 22},
        "desc": {
            "en": "A reliable standard sword, well balanced.",
            "zh": "标准可靠的直剑,各方面均衡。",
        },
    },
    "longsword": {
        "name": {"en": "LONGSWORD", "zh": "长剑"},
        "type": "weapon",
        "slot": "mainhand",
        "attack_style": "slash",
        "stats": {"damage": 42, "speed": 0.9, "range": 66, "stamina": 24},
        "desc": {
            "en": "Longer blade, heavier swing.",
            "zh": "更长的剑刃,挥动略慢。",
        },
    },
    "greatsword": {
        "name": {"en": "GREATSWORD", "zh": "巨剑"},
        "type": "weapon",
        "slot": "mainhand",
        "attack_style": "slash",
        "stats": {"damage": 60, "speed": 0.65, "range": 74, "stamina": 32},
        "desc": {
            "en": "Slow but devastating.",
            "zh": "缓慢但破坏力惊人。",
        },
    },
    "dagger": {
        "name": {"en": "DAGGER", "zh": "短剑"},
        "type": "weapon",
        "slot": "offhand",
        "attack_style": "slash",
        "stats": {"damage": 18, "speed": 1.5, "range": 48, "stamina": 14},
        "desc": {
            "en": "A quick off-hand blade.",
            "zh": "快速的副手利刃。",
        },
    },
    "small_round_shield": {
        "name": {"en": "SMALL ROUND SHIELD", "zh": "小圆盾"},
        "type": "weapon",
        "slot": "offhand",
        "subtype": "shield",
        "stats": {"defense": 0.5, "parry_time": 0.22},
        "desc": {
            "en": "A small shield. Block to reduce damage; parry at the start of a block.",
            "zh": "轻巧的小圆盾。招架减伤,举盾瞬间可弹反。",
        },
    },
    "knight_armor": {
        "name": {"en": "KNIGHT ARMOR", "zh": "骑士铠甲"},
        "type": "armor",
        "slot": "armor",
        "stats": {"defense": 18},
        "desc": {
            "en": "Solid plate armor of a knight.",
            "zh": "骑士的厚重板甲。",
        },
    },
    "leather_armor": {
        "name": {"en": "LEATHER ARMOR", "zh": "皮甲"},
        "type": "armor",
        "slot": "armor",
        "stats": {"defense": 8},
        "desc": {
            "en": "Light leather armor.",
            "zh": "轻便的皮甲。",
        },
    },
    "golden_tree_shard": {
        "name": {"en": "GOLDEN TREE SHARD", "zh": "黄金树碎片"},
        "type": "talisman",
        "slot": "talisman",
        "stats": {},
        "effect": {"hp_up": 20},
        "desc": {
            "en": "A blessing from a distant land.",
            "zh": "来自遥远国度的赐福。",
        },
    },
    "stamina_talisman": {
        "name": {"en": "STAMINA TALISMAN", "zh": "精力护符"},
        "type": "talisman",
        "slot": "talisman",
        "stats": {},
        "effect": {"stamina_up": 20},
        "desc": {
            "en": "Raises maximum stamina by 20.",
            "zh": "最大精力 +20。",
        },
    },
    "fire_talisman": {
        "name": {"en": "FIRE TALISMAN", "zh": "火焰护符"},
        "type": "talisman",
        "slot": "talisman",
        "stats": {},
        "effect": {},
        "desc": {
            "en": "A dormant charm, to be awakened in future updates.",
            "zh": "沉睡的护符,未来的版本中将被唤醒。",
        },
    },
}

DEFAULT_INVENTORY = ["broadsword", "dagger", "small_round_shield", "leather_armor", "golden_tree_shard", "fire_talisman"]

ITEM_ALIASES = {"life_talisman": "golden_tree_shard"}
DEFAULT_EQUIPMENT = {
    "mainhand": "broadsword",
    "offhand": None,
    "armor": None,
    "talisman1": None,
    "talisman2": None,
    "talisman3": None,
}


def get(item_id):
    return ITEMS.get(item_id)


def name(item_id, lang="en"):
    item = ITEMS.get(item_id)
    if not item:
        return "?"
    return item["name"].get(lang, item["name"]["en"])


def desc(item_id, lang="en"):
    item = ITEMS.get(item_id)
    if not item:
        return ""
    return item["desc"].get(lang, item["desc"]["en"])
