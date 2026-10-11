# Ash Hollow (Dark Souls mini-game)

Python + pygame project. Run with `.venv/bin/python main.py` or `./run.sh`.

## Working rules (from the user)
- Do NOT auto-launch the game after making changes. The user starts it themselves.
- Small changes: commit locally only, do NOT push.
- Releases: add changelog entry to `changelog.py` first, then `./tools/release.sh <version>` (commits, tags, pushes; GitHub Actions builds a versioned exe and creates a release).
- Version number lives in `settings.py` (`VERSION`).

## Testing
Headless smoke tests live in `/tmp/opencode/test_*.py` (may be cleaned up between sessions):
- test_rooms.py — hub/arena room transitions, fog gate interact
- test_regression.py — core combat mechanics
- test_v3.py — menus, mouse, keybind conflicts
- test_changelog.py — changelog browser
- test_display.py — display modes, resolution
- test_newgame_confirm.py — overwrite-save confirm

Run a test: `PYTHONPATH=. .venv/bin/python /tmp/opencode/<test>.py`

## Key architecture
- Fixed 1280x720 canvas scaled to window; two rooms: "hub" (Firelink Shrine + bonfire) and "arena" (boss, entered via fog gate with interact key).
- Items/equipment are data-driven in `items.py` (type: weapon/armor/talisman, slot: mainhand/offhand/armor/talisman). Weapon stats set player attack damage/speed, armor sets defense, talismans give hp_up/stamina_up effects. Inventory UI (TAB) in main.py.
- Fonts bundled in `fonts/` (EB Garamond EN, Source Han Serif ZH, Cinzel title; agmena.ttf preferred if dropped in).
- Config/save next to exe (or `ASH_HOLLOW_DIR` env override).
