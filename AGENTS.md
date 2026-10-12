# Ash Hollow (Dark Souls mini-game)

Python + pygame project. Run with `.venv/bin/python main.py` or `./run.sh`.

## Working rules (from the user)
- Do NOT auto-launch the game after making changes. The user starts it themselves.
- Small changes: commit locally only, do NOT push.
- Releases: add changelog entry to `changelog.py` first, then `./tools/release.sh <version>` (commits, tags, pushes; GitHub Actions builds a versioned exe and creates a release).
- Version number lives in `settings.py` (`VERSION`).

## Testing
Headless smoke tests live in `tests/` (run from repo root):
- test_combat.py — core combat mechanics + feel (hitstun/hitstop/buffer)
- test_rooms.py — hub/arena room transitions, fog gate interact
- test_menus.py — menus, mouse, keybind conflicts, changelog browser
- test_inventory.py — inventory UI, save/load, migrations
- test_enemies.py — mobs: patrol/aggro/leash, melee/ranged, souls

Run: `.venv/bin/python tests/<file>.py` (all use SDL dummy driver,
isolated config via temp dir)

## Key architecture
- Fixed 1280x720 canvas scaled to window; two rooms: "hub" (Firelink Shrine + bonfire) and "arena" (boss, entered via fog gate with interact key).
- Items/equipment are data-driven in `items.py` (type: weapon/armor/talisman, slot: mainhand/offhand/armor/talisman). Weapon stats set player attack damage/speed, armor sets defense, talismans give hp_up/stamina_up effects. Inventory UI (TAB) in main.py.
- Fonts bundled in `fonts/` (EB Garamond EN, Source Han Serif ZH, Cinzel title; agmena.ttf preferred if dropped in).
- Config/save next to exe (or `ASH_HOLLOW_DIR` env override).
