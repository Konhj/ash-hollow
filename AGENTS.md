# Ash Hollow (Dark Souls mini-game)

Python + pygame project. Run with `.venv/bin/python main.py` or `./run.sh`.

## Model routing (Windows 部署后生效)
- 本地模型(建议 Qwen2.5-Coder-32B / Qwen3-Coder-30B,Q4 量化,LM Studio/Ollama/vLLM):
  单文件编辑、小功能、机械性重构、测试编写与运行、格式与文案修改。
- 云端模型(DeepSeek V4 Pro):
  架构与系统设计、跨模块重构、疑难 bug、长会话设计讨论、发布与 CI 事务。
- 无论哪个模型:动代码前先读 AGENTS.md 与 DESIGN.md(规格书),改完跑 tests/ 全绿。

## OpenCode 双 provider 配置示例
`~/.config/opencode/opencode.json` 增加本地 provider(OpenAI 兼容 API):
```json
{
  "provider": {
    "local": {
      "npm": "@ai-sdk/openai-compatible",
      "options": { "baseURL": "http://localhost:11434/v1" },
      "models": { "qwen-coder": { "name": "Qwen3-Coder-30B" } }
    }
  }
}
```
用法:`opencode run --model local/qwen-coder "..."`(LM Studio 默认端口 1234,Ollama 默认 11434)。

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
