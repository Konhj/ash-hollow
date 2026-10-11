LANG = "en"

STRINGS = {
    "game_title": {"en": "ASH HOLLOW", "zh": "灰烬谷"},
    "game_subtitle": {"en": "A TINY SOULS-LIKE", "zh": "迷你魂系小游戏"},
    "boss_name": {"en": "ABYSS WATCHER", "zh": "深渊监视者"},
    "boss_subtitle": {"en": "HEIR OF THE EMBERS", "zh": "余烬的继承者"},
    "unkindled": {"en": "UNKINDLED", "zh": "无火余烬"},
    "new_game": {"en": "NEW GAME", "zh": "新游戏"},
    "continue": {"en": "CONTINUE", "zh": "继续游戏"},
    "settings": {"en": "SETTINGS", "zh": "设置"},
    "quit": {"en": "QUIT", "zh": "退出游戏"},
    "back": {"en": "BACK", "zh": "返回"},
    "resume": {"en": "RESUME", "zh": "继续"},
    "save_and_exit": {"en": "SAVE & EXIT TO MAIN MENU", "zh": "保存并返回主菜单"},
    "game_saved": {"en": "GAME SAVED", "zh": "游戏已保存"},
    "resolution": {"en": "RESOLUTION", "zh": "分辨率"},
    "language": {"en": "LANGUAGE", "zh": "语言"},
    "key_bindings": {"en": "KEY BINDINGS", "zh": "按键映射"},
    "lang_en": {"en": "English", "zh": "English"},
    "lang_zh": {"en": "Simplified Chinese", "zh": "简体中文"},
    "press_any_key": {"en": "PRESS ANY KEY...", "zh": "请按任意键..."},
    "move": {"en": "MOVE", "zh": "移动"},
    "up": {"en": "UP", "zh": "上"},
    "down": {"en": "DOWN", "zh": "下"},
    "left": {"en": "LEFT", "zh": "左"},
    "right": {"en": "RIGHT", "zh": "右"},
    "attack": {"en": "ATTACK / RIPOSTE", "zh": "攻击 / 处决"},
    "roll": {"en": "ROLL (I-FRAMES)", "zh": "翻滚 (无敌帧)"},
    "parry": {"en": "PARRY", "zh": "弹反"},
    "estus": {"en": "ESTUS FLASK", "zh": "原素瓶"},
    "rest": {"en": "REST AT BONFIRE", "zh": "在篝火休息"},
    "pause": {"en": "PAUSE", "zh": "暂停"},
    "victory": {"en": "VICTORY ACHIEVED", "zh": "胜利达成"},
    "restart": {"en": "PRESS ENTER TO PLAY AGAIN", "zh": "按回车再来一次"},
    "esc_main_menu": {"en": "PRESS ESC FOR MAIN MENU", "zh": "按 ESC 返回主菜单"},
    "out_of_stamina": {"en": "OUT OF STAMINA", "zh": "精力不足"},
    "no_estus": {"en": "NO ESTUS LEFT", "zh": "没有原素瓶了"},
    "parry_msg": {"en": "PARRY!", "zh": "弹反成功!"},
    "riposte_msg": {"en": "RIPOSTE!", "zh": "处决!"},
    "cannot_rest": {"en": "CANNOT REST, FOE NEARBY", "zh": "敌人在附近,无法休息"},
    "bonfire_rested": {"en": "BONFIRE RESTED", "zh": "已在篝火休息"},
    "respawned": {"en": "RESPAWNED AT BONFIRE", "zh": "在篝火复活"},
    "rest_hint": {"en": "PRESS {key} TO REST", "zh": "按 {key} 休息"},
}


def set_lang(lang):
    global LANG
    LANG = lang


def get_lang():
    return LANG


def t(name, **kw):
    s = STRINGS[name].get(LANG, STRINGS[name]["en"])
    return s.format(**kw) if kw else s
