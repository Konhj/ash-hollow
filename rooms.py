# 房间定义:世界坐标,原点为房间左上角。
# size: 房间世界尺寸(可大于 1280x720,镜头跟随)
# player_spawn: 进入/复活出生点
# solid_rects: 地形阻挡矩形 (x, y, w, h)
# connections: 连接点,kind 支持 door / fog;
#   sealed: "boss_alive" 表示 Boss 存活时封印(不可交互)
# banner: 进入时显示的横幅 i18n 键(可空)
# bonfire: 篝火位置(可空)

ROOMS = {
    "hub": {
        "size": (1280, 720),
        "player_spawn": (640, 560),
        "bonfire": (640, 430),
        "banner": "firelink",
        "solid_rects": [
            (0, 0, 1280, 16),
            (0, 704, 1280, 16),
            (1264, 0, 16, 200),
            (1264, 520, 16, 200),
            (0, 0, 16, 200),
            (0, 520, 16, 200),
        ],
        "connections": [
            {
                "to": "arena",
                "rect": (1244, 200, 36, 320),
                "spawn": (150, 360),
                "kind": "fog",
            },
            {
                "to": "outpost",
                "rect": (0, 200, 36, 320),
                "spawn": (2440, 720),
                "kind": "door",
            },
        ],
    },
    "arena": {
        "size": (1280, 720),
        "player_spawn": (150, 360),
        "banner": None,
        "solid_rects": [
            (0, 0, 1280, 16),
            (0, 704, 1280, 16),
            (1264, 0, 16, 720),
            (0, 0, 16, 200),
            (0, 520, 16, 200),
        ],
        "connections": [
            {
                "to": "hub",
                "rect": (0, 200, 36, 320),
                "spawn": (1150, 400),
                "kind": "fog",
                "sealed": "boss_alive",
            },
        ],
    },
    "outpost": {
        "size": (2560, 1440),
        "player_spawn": (2440, 720),
        "bonfire": None,
        "banner": None,
        "solid_rects": [
            (0, 0, 2560, 16),
            (0, 1424, 2560, 16),
            (0, 0, 16, 1440),
            (2544, 0, 16, 1440),
            (600, 400, 80, 640),
            (1600, 600, 220, 80),
            (1800, 1050, 80, 240),
        ],
        "connections": [
            {
                "to": "hub",
                "rect": (2524, 600, 36, 240),
                "spawn": (60, 380),
                "kind": "door",
            },
        ],
    },
}


def room_by_id(room_id):
    return ROOMS.get(room_id)
