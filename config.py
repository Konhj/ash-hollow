import json
import os
import sys

import pygame

from settings import RESOLUTIONS

DEFAULT_BINDINGS = {
    "move_up": pygame.K_w,
    "move_down": pygame.K_s,
    "move_left": pygame.K_a,
    "move_right": pygame.K_d,
    "attack": pygame.K_j,
    "roll": pygame.K_k,
    "offhand": pygame.K_l,
    "estus": pygame.K_f,
    "interact": pygame.K_e,
    "sprint": pygame.K_LSHIFT,
    "inventory": pygame.K_TAB,
}

DEFAULTS = {
    "resolution": 2,
    "display_mode": 0,
    "language": "en",
    "show_keys": True,
    "bindings": dict(DEFAULT_BINDINGS),
}


def base_dir():
    env = os.environ.get("ASH_HOLLOW_DIR")
    if env:
        return env
    if getattr(sys, "frozen", False):
        return os.path.dirname(os.path.abspath(sys.executable))
    return os.path.dirname(os.path.abspath(__file__))


def resource_path(name):
    base = getattr(sys, "_MEIPASS", os.path.dirname(os.path.abspath(__file__)))
    return os.path.join(base, name)


CONFIG_PATH = os.path.join(base_dir(), "config.json")
SAVE_PATH = os.path.join(base_dir(), "save.json")


def key_name(key):
    try:
        return pygame.key.name(key).upper()
    except Exception:
        return "?"


def load_config():
    cfg = json.loads(json.dumps(DEFAULTS))
    try:
        with open(CONFIG_PATH, "r", encoding="utf-8") as f:
            data = json.load(f)
        for k in ("resolution", "display_mode", "language"):
            if k in data:
                cfg[k] = data[k]
        if "bindings" in data and isinstance(data["bindings"], dict):
            for k, v in data["bindings"].items():
                if k in cfg["bindings"] and isinstance(v, int):
                    cfg["bindings"][k] = v
            if "parry" in data["bindings"] and "offhand" not in data["bindings"]:
                cfg["bindings"]["offhand"] = data["bindings"]["parry"]
        if not (0 <= cfg["resolution"] < len(RESOLUTIONS)):
            cfg["resolution"] = DEFAULTS["resolution"]
        if not (0 <= cfg["display_mode"] <= 2):
            cfg["display_mode"] = DEFAULTS["display_mode"]
        if cfg["language"] not in ("en", "zh"):
            cfg["language"] = DEFAULTS["language"]
        if "show_keys" in data:
            cfg["show_keys"] = bool(data["show_keys"])
    except Exception:
        pass
    return cfg


def save_config(cfg):
    try:
        with open(CONFIG_PATH, "w", encoding="utf-8") as f:
            json.dump(cfg, f, ensure_ascii=False, indent=2)
    except Exception:
        pass


def save_game(data):
    try:
        with open(SAVE_PATH, "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
    except Exception:
        pass


def load_save():
    try:
        with open(SAVE_PATH, "r", encoding="utf-8") as f:
            data = json.load(f)
        if isinstance(data, dict) and "player" in data and "boss" in data:
            return data
    except Exception:
        pass
    return None


def delete_save():
    try:
        if os.path.exists(SAVE_PATH):
            os.remove(SAVE_PATH)
    except Exception:
        pass
