using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;

namespace AshHollow;

/// <summary>
/// 主场景根节点:全局游戏编排(对应旧 pygame main.py 的 Game 类)。
/// 世界坐标 = 房间坐标;Camera2D 跟随玩家;UI 在 CanvasLayer。
/// </summary>
public partial class MainGame : Node2D
{
    public enum GameState { Play, Pause, Died, Victory, Inventory, MainMenu }

    public GameState State = GameState.MainMenu;

    public string RoomId;
    public RoomDef RoomDef;
    public Player Player;
    public Boss Boss;
    public Bonfire Bonfire;
    public readonly Particles Particles = new();
    public readonly HashSet<(string room, int index)> KilledEnemies = new();

    float _time;
    float _fade = 1f;
    float _shake;
    float _hitstop;
    float _connCooldown;
    float _hubBanner;
    string _roomBannerKey;
    float _deathTimer;
    Vector2 _camOffset;
    (string action, float timer) _inputBuffer;

    readonly struct Msg
    {
        public readonly string Text;
        public readonly Color Color;
        public readonly float Dur, Time;
        public readonly int Size;
        public Msg(string text, Color color, float dur, float time, int size)
        {
            Text = text; Color = color; Dur = dur; Time = time; Size = size;
        }
    }
    readonly List<Msg> _messages = new();

    Sprite2D _floorSprite;
    Camera2D _camera;
    CanvasLayer _uiLayer;
    UiOverlay _ui;
    FxLayer _fx;
    Node2D _enemiesLayer;
    readonly List<Enemy> _enemies = new();
    readonly List<Projectile> _projectiles = new();
    readonly Dictionary<string, ImageTexture> _floorCache = new();

    readonly Menu _menu = new();
    string _clMode;
    int _clSel;
    float _clScroll;
    string _clVer;
    readonly List<string> _clLines = new();
    readonly List<Rect2> _clRects = new();
    Rect2 _versionRect;

    int _invTab;
    string _invHover;
    string _invSelectedSlot;
    readonly List<(Rect2 rect, string item)> _invCells = new();
    readonly List<(Rect2 rect, string key, string item)> _invEquipCells = new();
    readonly List<Rect2> _invTabRects = new();

    readonly Dictionary<Key, bool> _prevKeys = new();
    bool _mouseDown;
    bool _mouseRightDown;

    public override void _Ready()
    {
        DataTables.LoadAll();
        S.Init();
        Palette.Init();
        Fonts.Load();
        ItemsDb.Load();
        RoomDb.Load();
        EnemyDb.Load();
        Config.Load();
        I18n.SetLang(Config.Language);
        Config.ApplyDisplay();

        _floorSprite = new Sprite2D { Centered = false };
        AddChild(_floorSprite);

        Bonfire = new Bonfire();
        AddChild(Bonfire);

        Boss = new Boss();
        AddChild(Boss);
        Boss.Position = new Vector2(1080, 400);
        Boss.Reset();

        _enemiesLayer = new Node2D();
        AddChild(_enemiesLayer);

        var projLayer = new ProjLayer(this);
        AddChild(projLayer);

        Player = new Player();
        Player.Inventory.AddRange(ItemsDb.DefaultInventory);
        foreach (var kv in ItemsDb.DefaultEquipment)
        {
            Player.Equipment[kv.Key] = kv.Value;
        }
        AddChild(Player);

        _fx = new FxLayer(this);
        _fx.ZIndex = 10;
        AddChild(_fx);

        _camera = new Camera2D();
        AddChild(_camera);
        _camera.MakeCurrent();

        _uiLayer = new CanvasLayer();
        AddChild(_uiLayer);
        _ui = new UiOverlay(this);
        _uiLayer.AddChild(_ui);

        EnterRoom("hub", RoomDb.Get("hub").PlayerSpawn);
        BuildMainMenu();
        _fade = 1f;

        GD.Print($"[AshHollow] v{S.Version} lang={I18n.Lang}");
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _fade = Mathf.Max(0, _fade - dt * 1.5f);
        _shake = Mathf.Max(0, _shake - dt * 2.2f);

        if (JustKey(Key.Escape))
        {
            OnEscape();
        }

        switch (State)
        {
            case GameState.Play: UpdatePlay(dt); break;
            case GameState.Pause: UpdatePause(); break;
            case GameState.Died: UpdateDied(dt); break;
            case GameState.Victory: UpdateVictory(); break;
            case GameState.Inventory: UpdateInventory(); break;
            case GameState.MainMenu: UpdateMainMenu(); break;
        }

        for (int i = _messages.Count - 1; i >= 0; i--)
        {
            var m = _messages[i];
            m = new Msg(m.Text, m.Color, m.Dur, m.Time - dt, m.Size);
            if (m.Time <= 0)
            {
                _messages.RemoveAt(i);
            }
            else
            {
                _messages[i] = m;
            }
        }

        QueueRedraw();
        _fx.QueueRedraw();
        _ui.QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_menu.Capture != null && e is InputEventKey keyEv && keyEv.Pressed && !keyEv.Echo)
        {
            if (keyEv.Keycode != Key.Escape)
            {
                Config.Bindings[_menu.Capture] = keyEv.Keycode;
                Config.Save();
            }
            _menu.Capture = null;
            GetViewport().SetInputAsHandled();
        }
    }

    bool JustKey(Key key)
    {
        bool down = Input.IsKeyPressed(key);
        bool prev = _prevKeys.TryGetValue(key, out var p) && p;
        _prevKeys[key] = down;
        return down && !prev;
    }

    bool Just(string action) => JustKey(Config.Bindings[action]);

    bool Held(string action) => Input.IsKeyPressed(Config.Bindings[action]);

    Vector2 MousePos => GetViewport().GetMousePosition();

    bool MouseClicked()
    {
        bool down = Input.IsMouseButtonPressed(MouseButton.Left);
        bool clicked = down && !_mouseDown;
        _mouseDown = down;
        return clicked;
    }

    bool MouseRightClicked()
    {
        bool down = Input.IsMouseButtonPressed(MouseButton.Right);
        bool clicked = down && !_mouseRightDown;
        _mouseRightDown = down;
        return clicked;
    }

    // ---------------------------------------------------------------- 状态机

    void OnEscape()
    {
        if (State == GameState.MainMenu && _clMode != null)
        {
            ClBack();
            return;
        }
        if (State == GameState.Inventory)
        {
            State = GameState.Play;
            return;
        }
        switch (State)
        {
            case GameState.Play:
                BuildPauseMenu();
                State = GameState.Pause;
                break;
            case GameState.Pause:
                if (!_menu.Pop())
                {
                    State = GameState.Play;
                }
                break;
            case GameState.MainMenu:
                _menu.Pop();
                break;
            case GameState.Victory:
                GoMainMenu();
                break;
        }
    }

    void GoMainMenu()
    {
        BuildMainMenu();
        State = GameState.MainMenu;
    }

    void UpdateMainMenu()
    {
        if (_clMode != null)
        {
            UpdateChangelog();
            return;
        }
        bool clicked = MouseClicked();
        if (clicked && _versionRect.HasPoint(MousePos))
        {
            OpenChangelog();
            return;
        }
        _menu.Update(JustKey, MousePos, clicked);
    }

    void UpdatePause()
    {
        _menu.Update(JustKey, MousePos, MouseClicked());
        MouseClicked();
    }

    // ---------------------------------------------------------------- 菜单构建

    void BuildMainMenu()
    {
        var items = new List<MenuItem>
        {
            new() { Label = () => I18n.T("new_game"), Action = ConfirmNewGame },
            new()
            {
                Label = () => I18n.T("continue"),
                Action = LoadAndContinue,
                Visible = () => SaveManager.Exists(),
            },
            new() { Label = () => I18n.T("settings"), Action = BuildSettingsMenu },
            new()
            {
                Label = () => I18n.T("quit"),
                Action = () => BuildConfirm("confirm_quit", () => GetTree().Quit()),
            },
        };
        _menu.Set(I18n.T("game_title"), items, titleFont: true);
        _clMode = null;
    }

    void BuildPauseMenu()
    {
        var items = new List<MenuItem>
        {
            new() { Label = () => I18n.T("resume"), Action = () => State = GameState.Play },
            new() { Label = () => I18n.T("settings"), Action = BuildSettingsMenu },
            new()
            {
                Label = () => I18n.T("save_and_exit"),
                Action = () => BuildConfirm("confirm_save_exit", SaveAndExit),
            },
        };
        _menu.Set(I18n.T("pause"), items);
    }

    void BuildConfirm(string questionKey, System.Action yesAction, string warningKey = null)
    {
        var items = new List<MenuItem>
        {
            new() { Label = () => I18n.T("yes"), Action = yesAction },
            new() { Label = () => I18n.T("no"), Action = () => _menu.Pop() },
        };
        _menu.Push(I18n.T(questionKey), items, startY: 330, step: 60,
            warning: warningKey == null ? null : () => I18n.T(warningKey));
    }

    void ConfirmNewGame()
    {
        if (!SaveManager.Exists())
        {
            StartNewGame();
        }
        else
        {
            BuildConfirm("confirm_new_game", StartNewGame, "overwrite_warning");
        }
    }

    void StartNewGame()
    {
        Restart();
        State = GameState.Play;
    }

    void BuildSettingsMenu()
    {
        string ResLabel()
        {
            var r = S.Resolutions[Mathf.Clamp(Config.Resolution, 0, S.Resolutions.Count - 1)];
            return $"{I18n.T("resolution")}: {r.X} x {r.Y}";
        }
        string DisplayLabel()
        {
            string[] keys = { "mode_windowed", "mode_borderless", "mode_fullscreen" };
            return $"{I18n.T("display")}: {I18n.T(keys[Mathf.Clamp(Config.DisplayMode, 0, 2)])}";
        }
        string LangLabel()
        {
            string cur = Config.Language == "zh" ? "lang_zh" : "lang_en";
            return $"{I18n.T("language")}: {I18n.T(cur)}";
        }
        string KeysLabel() => $"{I18n.T("show_keys")}: {I18n.T(Config.ShowKeys ? "on" : "off")}";

        var items = new List<MenuItem>
        {
            new() { Label = DisplayLabel, Adjust = AdjustDisplayMode },
            new() { Label = ResLabel, Adjust = AdjustResolution },
            new() { Label = LangLabel, Adjust = AdjustLanguage },
            new() { Label = () => I18n.T("key_bindings"), Action = BuildKeyMenu },
            new() { Label = KeysLabel, Adjust = AdjustShowKeys },
            new() { Label = () => I18n.T("back"), Action = () => _menu.Pop() },
        };
        _menu.Push(I18n.T("settings"), items,
            hint: () => (I18n.T("adjust_hint"), Palette.Gray));
    }

    void BuildKeyMenu()
    {
        var items = new List<MenuItem>();
        var actions = new List<(string, string)>
        {
            ("move_up", "up"), ("move_down", "down"), ("move_left", "left"), ("move_right", "right"),
            ("attack", "attack"), ("roll", "roll"), ("offhand", "slot_offhand"), ("estus", "estus"),
            ("interact", "rest"), ("sprint", "sprint"), ("inventory", "inventory"),
        };
        foreach (var (action, labelKey) in actions)
        {
            string act = action;
            string lk = labelKey;
            items.Add(new MenuItem
            {
                Label = () =>
                {
                    string name = Config.KeyName(Config.Bindings[act]);
                    return action.StartsWith("move")
                        ? $"{I18n.T("move")} {I18n.T(lk)}: {name}"
                        : $"{I18n.T(lk)}: {name}";
                },
                Action = () => _menu.Capture = act,
                Red = () => BindingConflicts().Contains(Config.Bindings[act]),
            });
        }
        items.Add(new MenuItem { Label = () => I18n.T("back"), Action = () => _menu.Pop() });
        _menu.Push(I18n.T("key_bindings"), items, startY: 250, step: 60, columns: 2,
            hint: () => BindingConflicts().Count > 0 ? (I18n.T("key_conflict"), Palette.RGB(255, 90, 90)) : default);
    }

    void AdjustDisplayMode(int delta)
    {
        Config.DisplayMode = (Config.DisplayMode + delta + 3) % 3;
        Config.ApplyDisplay();
        Config.Save();
    }

    void AdjustResolution(int delta)
    {
        Config.Resolution = (Config.Resolution + delta + S.Resolutions.Count) % S.Resolutions.Count;
        Config.ApplyDisplay();
        Config.Save();
    }

    void AdjustLanguage(int delta)
    {
        Config.Language = Config.Language == "en" ? "zh" : "en";
        I18n.SetLang(Config.Language);
        Config.Save();
    }

    void AdjustShowKeys(int delta)
    {
        Config.ShowKeys = !Config.ShowKeys;
        Config.Save();
    }

    HashSet<Key> BindingConflicts()
    {
        var counts = new Dictionary<Key, int>();
        foreach (var v in Config.Bindings.Values)
        {
            counts[v] = counts.GetValueOrDefault(v) + 1;
        }
        var conflicts = new HashSet<Key>();
        foreach (var kv in counts)
        {
            if (kv.Value > 1)
            {
                conflicts.Add(kv.Key);
            }
        }
        return conflicts;
    }

    // ---------------------------------------------------------------- 存档

    void SaveCurrent() => SaveManager.Save(this);

    void LoadAndContinue()
    {
        var data = SaveManager.Load();
        if (data == null)
        {
            return;
        }
        Restart();
        var p = Player;
        var b = Boss;
        var pd = data["player"] as JsonObject;
        var bd = data["boss"] as JsonObject;
        p.Position = new Vector2(Num(pd, "x", p.Position.X), Num(pd, "y", p.Position.Y));
        p.Estus = Mathf.Clamp((int)Num(pd, "estus", p.Estus), 0, S.EstusMax);
        if (data["inventory"] is JsonArray invArr)
        {
            var valid = new List<string>();
            foreach (var i in invArr)
            {
                string id = Alias(i.GetValue<string>());
                if (ItemsDb.Get(id) != null)
                {
                    valid.Add(id);
                }
            }
            if (valid.Count > 0)
            {
                p.Inventory = valid;
            }
        }
        if (data["equipment"] is JsonObject eq)
        {
            foreach (var key in p.Equipment.Keys)
            {
                string v = eq[key]?.GetValue<string>();
                v = Alias(v);
                p.Equipment[key] = v != null && ItemsDb.Get(v) != null ? v : null;
            }
        }
        p.ResetStats();
        p.Hp = Mathf.Min(p.MaxHp(), Mathf.Max(1, (int)Num(pd, "hp", p.MaxHp())));
        p.Souls = Mathf.Max(0, (int)Num(data, "souls", 0));
        KilledEnemies.Clear();
        if (data["killed"] is JsonArray killedArr)
        {
            foreach (var k in killedArr)
            {
                var a = k.AsArray();
                if (a.Count == 2)
                {
                    KilledEnemies.Add((a[0].GetValue<string>(), a[1].GetValue<int>()));
                }
            }
        }
        b.Position = new Vector2(Num(bd, "x", b.Position.X), Num(bd, "y", b.Position.Y));
        b.Hp = Mathf.Min(b.MaxHp, Mathf.Max(1, (int)Num(bd, "hp", b.Hp)));
        b.Aggro = bd["aggro"]?.GetValue<bool>() ?? false;
        string roomId = data["room"]?.GetValue<string>() ?? "hub";
        if (RoomDb.Get(roomId) == null)
        {
            roomId = "hub";
        }
        EnterRoom(roomId, p.Position);
        if (_hubBanner > 0)
        {
            _hubBanner = 1.6f;
        }
        State = GameState.Play;
        _fade = 1f;
    }

    static string Alias(string id)
    {
        return id == "life_talisman" ? "golden_tree_shard" : id;
    }

    static float Num(JsonObject o, string key, float fallback)
    {
        return o?[key] is JsonValue v ? (float)v.GetValue<double>() : fallback;
    }

    static int Num(JsonObject o, string key, int fallback)
    {
        return o?[key] is JsonValue v ? (int)v.GetValue<double>() : fallback;
    }

    void SaveAndExit()
    {
        SaveCurrent();
        Message(I18n.T("game_saved"), Palette.Orange, 1.6f, Fonts.Big);
        GoMainMenu();
    }

    // ---------------------------------------------------------------- 更新日志

    void OpenChangelog()
    {
        _clMode = "list";
        _clSel = 0;
        _clScroll = 0;
    }

    void ClBack()
    {
        if (_clMode == "detail")
        {
            _clMode = "list";
            _clScroll = 0;
        }
        else
        {
            _clMode = null;
        }
    }

    void ClOpenDetail(string ver)
    {
        _clVer = ver;
        _clMode = "detail";
        _clScroll = 0;
        _clLines.Clear();
        if (DataTables.Changelog[ver] is JsonArray entries)
        {
            foreach (var e in entries)
            {
                _clLines.AddRange(Wrap("- " + e.GetValue<string>(), Fonts.Zh, 860, Fonts.Normal));
            }
        }
    }

    void UpdateChangelog()
    {
        var mp = MousePos;
        bool clicked = MouseClicked();
        if (_clMode == "list")
        {
            var vers = ChangelogVersions();
            if (vers.Count > 0 && _clRects.Count == vers.Count)
            {
                for (int i = 0; i < _clRects.Count; i++)
                {
                    if (_clRects[i].HasPoint(mp))
                    {
                        _clSel = i;
                        break;
                    }
                }
            }
            if (JustKey(Key.Up) || JustKey(Key.W))
            {
                _clSel = Mathf.Max(0, _clSel - 1);
            }
            if (JustKey(Key.Down) || JustKey(Key.S))
            {
                _clSel = Mathf.Min(vers.Count - 1, _clSel + 1);
            }
            if (JustKey(Key.Enter) && vers.Count > 0)
            {
                ClOpenDetail(vers[_clSel]);
            }
            if (clicked && vers.Count > 0)
            {
                for (int i = 0; i < _clRects.Count; i++)
                {
                    if (_clRects[i].HasPoint(mp))
                    {
                        ClOpenDetail(vers[i]);
                        break;
                    }
                }
            }
        }
        else
        {
            if (JustKey(Key.Down) || JustKey(Key.S))
            {
                _clScroll += 60;
            }
            if (JustKey(Key.Up) || JustKey(Key.W))
            {
                _clScroll -= 60;
            }
        }
        MouseClicked();
    }

    List<string> ChangelogVersions()
    {
        var vers = new List<string>();
        if (DataTables.Changelog is JsonObject o)
        {
            foreach (var kv in o)
            {
                vers.Add(kv.Key);
            }
        }
        return vers;
    }

    static List<string> Wrap(string text, Font font, float maxW, int fontSize)
    {
        var lines = new List<string>();
        string cur = "";
        foreach (char ch in text)
        {
            if (ch == '\n')
            {
                lines.Add(cur);
                cur = "";
                continue;
            }
            if (cur.Length > 0 && font.GetStringSize(cur + ch, HorizontalAlignment.Left, -1, fontSize).X > maxW)
            {
                lines.Add(cur);
                cur = ch.ToString();
            }
            else
            {
                cur += ch;
            }
        }
        if (cur.Length > 0)
        {
            lines.Add(cur);
        }
        return lines;
    }

    // ---------------------------------------------------------------- 游戏流程

    void UpdatePlay(float dt)
    {
        if (Just("inventory"))
        {
            State = GameState.Inventory;
            _invHover = null;
            return;
        }

        if (Player.Dead)
        {
            State = GameState.Died;
            _deathTimer = 2.4f;
            return;
        }
        if (_hitstop > 0)
        {
            _hitstop -= dt;
            return;
        }

        _hubBanner = Mathf.Max(0, _hubBanner - dt);
        _connCooldown = Mathf.Max(0, _connCooldown - dt);
        Bonfire.Update(dt);
        Player.Update(dt, Held);
        if (Player.JustDrank)
        {
            Player.JustDrank = false;
            Particles.Spawn(Player.Position, Palette.RGB(120, 220, 120), 10, 2.2f, 0.5f);
        }
        if (RoomId == "arena")
        {
            Boss.Update(dt, Player);
        }
        foreach (var e in _enemies)
        {
            e.Update(dt, Player);
        }
        UpdateProjectiles(dt);
        Particles.Update(dt);

        Player.Clamp();
        Collision.ResolveTerrain(Player, RoomDef.SolidRects);
        foreach (var e in _enemies)
        {
            Collision.ResolveTerrain(e, RoomDef.SolidRects);
        }
        var bodies = new List<Entity> { Player };
        if (RoomId == "arena" && !Boss.Dead)
        {
            bodies.Add(Boss);
        }
        foreach (var e in _enemies)
        {
            if (!e.Dead)
            {
                bodies.Add(e);
            }
        }
        Collision.ResolveEntities(bodies);
        UpdateCamera();

        if (RoomDef.BonfirePos.HasValue && GD.Randf() < 0.35f)
        {
            var b = RoomDef.BonfirePos.Value;
            Particles.Spawn(new Vector2(b.X + GD.RandRange(-8, 8), b.Y - 10),
                Palette.RGB(255, 170, 50), 1, 1.2f, 0.8f);
        }
        if (RoomId == "arena" && Boss.State == "attack" && Boss.AttackKind == "charge")
        {
            Particles.Spawn(Boss.Position + new Vector2(-Boss.ChargeDir.X * 40, 20),
                Palette.RGB(180, 60, 40), 2, 2.0f, 0.3f);
        }

        bool attackHeld = Held("attack");
        if ((Player.State == "attack" || Player.State == "roll" || Player.State == "guard") && attackHeld)
        {
            _inputBuffer = ("attack", Config.InputBuffer);
        }
        else if (Player.State != "idle" && _inputBuffer.timer > 0)
        {
            _inputBuffer = (_inputBuffer.action, _inputBuffer.timer - dt);
        }

        if (Player.State == "attack" && Held("roll"))
        {
            var r = Player.StartRoll();
            if (r != null)
            {
                Message(I18n.T(r), Palette.Red, 1.4f, Fonts.Small);
            }
        }

        if (Player.State == "idle")
        {
            bool wantAttack = attackHeld || (_inputBuffer.action == "attack" && _inputBuffer.timer > 0);
            if (CanFight() && wantAttack)
            {
                string r;
                if (RoomId == "arena" && !Boss.Dead && Boss.State == "staggered" && Player.CenterDist(Boss) < 130)
                {
                    r = Player.StartRiposte();
                }
                else
                {
                    r = Player.StartAttack();
                }
                _inputBuffer = (null, 0);
                if (r != null)
                {
                    Message(I18n.T(r), Palette.Red, 1.4f, Fonts.Small);
                }
            }
            else if (Held("roll"))
            {
                var r = Player.StartRoll();
                if (r != null)
                {
                    Message(I18n.T(r), Palette.Red, 1.4f, Fonts.Small);
                }
            }
            else if (CanFight() && Held("offhand"))
            {
                if (Player.Shield() != null)
                {
                    Player.StartGuard();
                }
                else
                {
                    var r = Player.StartOffhandAttack();
                    if (r != null)
                    {
                        Message(I18n.T(r), Palette.Red, 1.4f, Fonts.Small);
                    }
                }
            }
            else if (Held("estus"))
            {
                var r = Player.StartDrink();
                if (r != null)
                {
                    Message(I18n.T(r), Palette.Red, 1.4f, Fonts.Small);
                }
            }
            else if (Held("interact"))
            {
                if (!TryRest())
                {
                    TryConnections();
                }
            }
        }

        ResolvePlayerAttackVsBoss();
        ResolvePlayerAttackVsEnemies();
        ProcessEnemyDeaths();
        ResolveRiposte();
        ResolveBossAttacks();
        if (RoomId == "arena" && Boss.Dead && Boss.StateTimer <= 0)
        {
            SaveManager.Delete();
            State = GameState.Victory;
            Message(I18n.T("victory"), Palette.Yellow, 2.0f, Fonts.Big);
        }
        ResolveEnemyAttacks();
    }

    void UpdateDied(float dt)
    {
        _deathTimer -= dt;
        Particles.Update(dt);
        if (_deathTimer <= 0)
        {
            Player.Respawn();
            Boss.Reset();
            KilledEnemies.Clear();
            EnterRoom("hub", RoomDb.Get("hub").PlayerSpawn);
            State = GameState.Play;
            _fade = 1f;
        }
    }

    void UpdateVictory()
    {
        Particles.Update(1f / 60f);
        if (JustKey(Key.Enter))
        {
            StartNewGame();
        }
    }

    void Restart()
    {
        Boss.Reset();
        Player.ResetProfile();
        KilledEnemies.Clear();
        _messages.Clear();
        Particles.Clear();
        EnterRoom("hub", RoomDb.Get("hub").PlayerSpawn);
        _fade = 1f;
    }

    // ---------------------------------------------------------------- 背包

    void UpdateInventory()
    {
        if (Just("inventory"))
        {
            State = GameState.Play;
            return;
        }
        var mp = MousePos;
        bool click = MouseClicked();
        if (click)
        {
            for (int i = 0; i < _invTabRects.Count; i++)
            {
                if (_invTabRects[i].HasPoint(mp))
                {
                    _invTab = i;
                    click = false;
                    break;
                }
            }
        }
        if (click)
        {
            foreach (var slot in _invEquipCells)
            {
                if (slot.rect.HasPoint(mp))
                {
                    _invSelectedSlot = _invSelectedSlot == slot.key ? null : slot.key;
                    click = false;
                    break;
                }
            }
        }
        if (click)
        {
            foreach (var cell in _invCells)
            {
                if (cell.rect.HasPoint(mp))
                {
                    if (cell.item != null && _invSelectedSlot != null)
                    {
                        EquipInto(_invSelectedSlot, cell.item);
                        _invSelectedSlot = null;
                    }
                    break;
                }
            }
        }
        if (MouseRightClicked())
        {
            foreach (var slot in _invEquipCells)
            {
                if (slot.rect.HasPoint(mp))
                {
                    if (Player.Equipment.TryGetValue(slot.key, out var v) && v != null)
                    {
                        UnequipSlot(slot.key);
                    }
                    break;
                }
            }
        }
        _invHover = null;
        foreach (var cell in _invCells)
        {
            if (cell.rect.HasPoint(mp))
            {
                _invHover = cell.item;
                break;
            }
        }
        if (_invHover == null)
        {
            foreach (var slot in _invEquipCells)
            {
                if (slot.rect.HasPoint(mp))
                {
                    _invHover = Player.Equipment.TryGetValue(slot.key, out var v) ? v : null;
                    break;
                }
            }
        }
        MouseClicked();
    }

    bool SlotAccepts(string key, string itemId)
    {
        var item = ItemsDb.Get(itemId);
        if (item == null)
        {
            return false;
        }
        string itype = item["type"]?.GetValue<string>();
        string islot = item["slot"]?.GetValue<string>();
        switch (key)
        {
            case "mainhand":
                return itype == "weapon" && islot == "mainhand";
            case "offhand":
                return itype == "weapon" && islot == "offhand";
            case "armor":
                return itype == "armor";
            default:
                return itype == "talisman" && !Player.Equipment.ContainsValue(itemId);
        }
    }

    void EquipInto(string key, string itemId)
    {
        if (!SlotAccepts(key, itemId))
        {
            return;
        }
        Player.Equipment[key] = itemId;
        Player.RefreshMaxHp();
        SaveCurrent();
    }

    void UnequipSlot(string key)
    {
        Player.Equipment[key] = null;
        Player.RefreshMaxHp();
        SaveCurrent();
    }

    List<string> TooltipLines(string itemId)
    {
        var item = ItemsDb.Get(itemId);
        if (item == null)
        {
            return new List<string>();
        }
        string itype = item["type"]?.GetValue<string>();
        var typeLabel = new Dictionary<string, string>
        {
            { "weapon", I18n.T("cat_weapons") },
            { "armor", I18n.T("cat_armor") },
            { "talisman", I18n.T("cat_talismans") },
        };
        var lines = new List<string>
        {
            ItemsDb.Name(itemId),
            typeLabel.TryGetValue(itype, out var tl) ? tl : itype,
        };
        if (itype == "weapon")
        {
            if (item["subtype"]?.GetValue<string>() == "shield")
            {
                lines.Add($"{I18n.T("block")}: {(int)(ItemsDb.Stat(item, "defense") * 100)}%");
                lines.Add($"{I18n.T("parry_time")}: {ItemsDb.Stat(item, "parry_time")}s");
            }
            else
            {
                lines.Add($"{I18n.T("damage")}: {ItemsDb.Stat(item, "damage")}");
                lines.Add($"{I18n.T("speed")}: {ItemsDb.Stat(item, "speed")}");
            }
            lines.Add(I18n.T(item["slot"]?.GetValue<string>() == "mainhand" ? "slot_mainhand" : "slot_offhand"));
        }
        else if (itype == "armor")
        {
            lines.Add($"{I18n.T("defense")}: {ItemsDb.Stat(item, "defense")}");
        }
        else if (itype == "talisman")
        {
            if (item["effect"] is JsonObject eff)
            {
                foreach (var kv in eff)
                {
                    lines.Add($"{I18n.T(kv.Key)} +{kv.Value.GetValue<int>()}");
                }
            }
        }
        string descText = ItemsDb.Desc(itemId);
        if (!string.IsNullOrEmpty(descText))
        {
            lines.Add("");
            lines.AddRange(Wrap(descText, Fonts.Pick(descText), 250, Fonts.Small));
        }
        return lines;
    }

    // ---------------------------------------------------------------- 战斗结算

    void ResolvePlayerAttackVsBoss()
    {
        if (RoomId != "arena" || Player.State != "attack" || Player.AttackLanded
            || !Player.AttackActive() || Boss.Dead)
        {
            return;
        }
        var hb = Player.GetAttackHitbox();
        if (hb.Intersects(Boss.Body, includeBorders: true))
        {
            Player.AttackLanded = true;
            Boss.TakeDamage(Player.CurrentAttackDamage());
            Particles.Spawn(hb.GetCenter(), Palette.RGB(255, 220, 120), 6, 3.0f, 0.3f);
        }
    }

    void ResolvePlayerAttackVsEnemies()
    {
        if (RoomId == "hub" || Player.State != "attack" || Player.AttackLanded || !Player.AttackActive())
        {
            return;
        }
        var hb = Player.GetAttackHitbox();
        foreach (var e in _enemies)
        {
            if (e.Dead)
            {
                continue;
            }
            if (hb.Intersects(e.Body, includeBorders: true))
            {
                Player.AttackLanded = true;
                e.TakeDamage(Player.CurrentAttackDamage());
                Particles.Spawn(hb.GetCenter(), Palette.RGB(255, 220, 120), 6, 3.0f, 0.3f);
                break;
            }
        }
    }

    void ProcessEnemyDeaths()
    {
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var e = _enemies[i];
            if (e.Dead && e.State == "dying" && e.StateTimer <= 0)
            {
                _enemies.RemoveAt(i);
                KilledEnemies.Add((RoomId, e.Index));
                Player.Souls += EnemyDb.Souls(e.Data);
                Particles.Spawn(e.Position, Palette.RGB(255, 200, 90), 14, 3.0f, 0.5f);
                e.QueueFree();
            }
        }
    }

    void ResolveRiposte()
    {
        if (RoomId != "arena" || Player.State != "riposte" || Player.RiposteDone)
        {
            return;
        }
        if (S.RiposteTime - Player.StateTimer >= 0.25f)
        {
            Player.RiposteDone = true;
            if (!Boss.Dead && Player.CenterDist(Boss) < 160)
            {
                Boss.TakeDamage(Player.AttackDamage() * 5);
                Boss.State = "recover";
                Boss.StateTimer = 0.8f;
                SetShake(1.2f);
                SetHitstop(S.HitstopRiposte);
                Particles.Spawn(Boss.Position, Palette.RGB(255, 200, 60), 24, 5.0f, 0.6f);
                Message(I18n.T("riposte_msg"), Palette.Yellow, 1.2f, Fonts.Big);
            }
        }
    }

    void ResolveBossAttacks()
    {
        if (RoomId != "arena" || Boss.State != "attack" || Boss.HitLanded)
        {
            return;
        }
        switch (Boss.AttackKind)
        {
            case "slash":
            {
                var hr = Boss.CurrentHitRect();
                if (hr.HasValue && hr.Value.Intersects(Player.Body, includeBorders: true))
                {
                    Boss.HitLanded = true;
                    if (Player.ParryWindowActive())
                    {
                        Boss.Stagger(2.5f);
                        Player.Invuln = 0.5f;
                        SetShake(0.6f);
                        SetHitstop(S.HitstopParry);
                        Particles.Spawn(Player.Position, Palette.RGB(255, 255, 255), 14, 4.0f, 0.4f);
                        Message(I18n.T("parry_msg"), Palette.Yellow, 1.2f, Fonts.Big);
                    }
                    else if (Player.TakeDamage(S.SlashDamage, Boss.Dir))
                    {
                        OnPlayerHit();
                    }
                }
                break;
            }
            case "charge":
            {
                var infl = Boss.Body.GrowIndividual(13, 8, 13, 8);
                if (infl.Intersects(Player.Body, includeBorders: true))
                {
                    Boss.HitLanded = true;
                    if (Player.TakeDamage(S.ChargeDamage, Boss.ChargeDir))
                    {
                        OnPlayerHit();
                    }
                }
                break;
            }
            case "slam":
                if (!Boss.SlamLanded)
                {
                    Boss.SlamLanded = true;
                    SetShake(1.4f);
                    SetHitstop(S.HitstopSlam);
                    Particles.Spawn(new Vector2(Boss.Position.X, Boss.Body.End.Y),
                        Palette.RGB(120, 90, 60), 26, 4.0f, 0.5f);
                    if (Player.CenterDist(Boss) <= S.SlamRadius)
                    {
                        if (Player.TakeDamage(S.SlamDamage, Boss.Dir))
                        {
                            OnPlayerHit();
                        }
                    }
                }
                break;
            case "blast":
                if (!Boss.BlastLanded)
                {
                    Boss.BlastLanded = true;
                    SetShake(1.6f);
                    SetHitstop(S.HitstopSlam);
                    Particles.Spawn(Boss.Position, Palette.RGB(255, 70, 60), 40, 6.0f, 0.6f);
                    if (Player.CenterDist(Boss) <= S.BlastRadius)
                    {
                        if (Player.TakeDamage(S.BlastDamage, Boss.Dir))
                        {
                            OnPlayerHit();
                        }
                    }
                }
                break;
        }
    }

    void ResolveEnemyAttacks()
    {
        foreach (var e in _enemies)
        {
            if (e.Dead || e.State != "attack")
            {
                continue;
            }
            var atk = e.Attack(e.AttackKind);
            if (atk == null)
            {
                continue;
            }
            if (atk["projectile"] != null)
            {
                if (!e.ShotFired)
                {
                    e.ShotFired = true;
                    var pr = atk["projectile"] as JsonObject;
                    float speed = (float)pr["speed"].GetValue<double>();
                    _projectiles.Add(new Projectile
                    {
                        Pos = e.Position,
                        Vel = e.Dir * speed,
                        R = (float)pr["radius"].GetValue<double>(),
                        Dmg = (int)atk["damage"].GetValue<double>(),
                        Life = (float)pr["range"].GetValue<double>() / speed,
                        Color = ColorFromJson(pr["color"]),
                    });
                }
            }
            else if (!e.HitLanded)
            {
                var hr = e.CurrentHitRect();
                if (hr.HasValue && hr.Value.Intersects(Player.Body, includeBorders: true))
                {
                    e.HitLanded = true;
                    if (Player.TakeDamage((int)atk["damage"].GetValue<double>(), e.Dir))
                    {
                        OnPlayerHit();
                    }
                }
            }
        }
    }

    void UpdateProjectiles(float dt)
    {
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            var pr = _projectiles[i];
            pr.Update(dt);
            bool gone = pr.Gone;
            if (!gone && pr.Rect.Intersects(Player.Body, includeBorders: true))
            {
                var v = pr.Vel.LengthSquared() > 0 ? pr.Vel.Normalized() : Vector2.Zero;
                if (Player.TakeDamage(pr.Dmg, v))
                {
                    OnPlayerHit();
                }
                gone = true;
            }
            if (!gone)
            {
                foreach (var sr in RoomDef.SolidRects)
                {
                    if (pr.Rect.Intersects(sr, includeBorders: true))
                    {
                        gone = true;
                        break;
                    }
                }
            }
            if (gone)
            {
                _projectiles.RemoveAt(i);
            }
        }
    }

    static Color ColorFromJson(JsonNode n)
    {
        var a = n.AsArray();
        return Palette.RGB((int)a[0].GetValue<double>(), (int)a[1].GetValue<double>(), (int)a[2].GetValue<double>());
    }

    // ---------------------------------------------------------------- 房间

    bool CanFight() => RoomDef.Combat;

    bool CanRest()
    {
        if (!RoomDef.BonfirePos.HasValue || Player.Dead || (Boss.Aggro && !Boss.Dead))
        {
            return false;
        }
        return Player.Position.DistanceSquaredTo(RoomDef.BonfirePos.Value) <= 60 * 60;
    }

    bool TryRest()
    {
        if (!RoomDef.BonfirePos.HasValue)
        {
            return false;
        }
        if (Player.Position.DistanceSquaredTo(RoomDef.BonfirePos.Value) > 60 * 60)
        {
            return false;
        }
        if (Boss.Aggro && !Boss.Dead)
        {
            Message(I18n.T("cannot_rest"), Palette.Red, 1.4f, Fonts.Big);
        }
        else
        {
            Player.Hp = Player.MaxHp();
            Player.Estus = S.EstusMax;
            Boss.Reset();
            KilledEnemies.Clear();
            SpawnEnemies(RoomId);
            SaveCurrent();
            _fade = 1f;
            Message(I18n.T("game_saved"), Palette.Orange, 1.6f, Fonts.Big);
        }
        return true;
    }

    void TryConnections()
    {
        if (_connCooldown > 0)
        {
            return;
        }
        foreach (var conn in RoomDef.Connections)
        {
            var r = conn.Rect.Grow(30);
            if (!r.Intersects(Player.Body, includeBorders: true))
            {
                continue;
            }
            if (conn.Sealed == "boss_alive" && !Boss.Dead)
            {
                continue;
            }
            if (RoomId == "arena" && conn.To == "hub")
            {
                Boss.Reset();
            }
            EnterRoom(conn.To, conn.Spawn);
            _fade = 0.8f;
            break;
        }
    }

    void EnterRoom(string roomId, Vector2 spawn)
    {
        var room = RoomDb.Get(roomId);
        RoomId = roomId;
        RoomDef = room;
        _floorSprite.Texture = GetFloor(roomId);
        Player.WorldBounds = room.Size;
        Boss.WorldBounds = room.Size;
        Player.Position = spawn;
        _connCooldown = 0.4f;
        SpawnEnemies(roomId);
        SnapCamera();
        if (room.Banner != null)
        {
            _roomBannerKey = room.Banner;
            _hubBanner = 2.6f;
        }
        else
        {
            _hubBanner = 0;
        }
    }

    void SpawnEnemies(string roomId)
    {
        foreach (var e in _enemies)
        {
            e.QueueFree();
        }
        _enemies.Clear();
        _projectiles.Clear();
        var room = RoomDb.Get(roomId);
        for (int i = 0; i < room.EnemySpawns.Count; i++)
        {
            if (KilledEnemies.Contains((roomId, i)))
            {
                continue;
            }
            var (eid, pos) = room.EnemySpawns[i];
            var e = new Enemy(eid, pos, i);
            _enemiesLayer.AddChild(e);
            _enemies.Add(e);
        }
    }

    ImageTexture GetFloor(string roomId)
    {
        if (!_floorCache.TryGetValue(roomId, out var tex))
        {
            tex = RenderFloor(RoomDb.Get(roomId));
            _floorCache[roomId] = tex;
        }
        return tex;
    }

    static ImageTexture RenderFloor(RoomDef room)
    {
        int w = (int)room.Size.X;
        int h = (int)room.Size.Y;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgb8);
        img.Fill(Palette.Floor);
        var rng = new System.Random(7);
        for (int gy = 0; gy < h; gy += 80)
        {
            for (int gx = 0; gx < w; gx += 80)
            {
                var c = new Color(
                    Palette.Floor.R + (rng.Next(-5, 6) / 255f),
                    Palette.Floor.G + (rng.Next(-5, 6) / 255f),
                    Palette.Floor.B + (rng.Next(-5, 6) / 255f));
                img.FillRect(new Rect2I(gx + 2, gy + 2, 76, 76), c);
                for (int x = gx; x < Mathf.Min(gx + 80, w); x++)
                {
                    img.SetPixel(x, gy, Palette.FloorDark);
                    img.SetPixel(x, Mathf.Min(gy + 79, h - 1), Palette.FloorDark);
                }
                for (int y = gy; y < Mathf.Min(gy + 80, h); y++)
                {
                    img.SetPixel(gx, y, Palette.FloorDark);
                    img.SetPixel(Mathf.Min(gx + 79, w - 1), y, Palette.FloorDark);
                }
            }
        }
        for (int x = 0; x < w; x++)
        {
            for (int i = 0; i < 6; i++)
            {
                img.SetPixel(x, Mathf.Min(i, h - 1), Palette.Wall);
                img.SetPixel(x, Mathf.Max(0, h - 1 - i), Palette.Wall);
            }
        }
        for (int y = 0; y < h; y++)
        {
            for (int i = 0; i < 6; i++)
            {
                img.SetPixel(Mathf.Min(i, w - 1), y, Palette.Wall);
                img.SetPixel(Mathf.Max(0, w - 1 - i), y, Palette.Wall);
            }
        }
        foreach (var sr in room.SolidRects)
        {
            var r = new Rect2I((int)sr.Position.X, (int)sr.Position.Y, (int)sr.Size.X, (int)sr.Size.Y);
            img.FillRect(r, Palette.RGB(52, 52, 60));
            for (int x = r.Position.X; x < r.End.X; x++)
            {
                img.SetPixel(x, r.Position.Y, Palette.RGB(70, 70, 80));
                img.SetPixel(x, Mathf.Max(r.Position.Y, r.End.Y - 1), Palette.RGB(70, 70, 80));
            }
            for (int y = r.Position.Y; y < r.End.Y; y++)
            {
                img.SetPixel(r.Position.X, y, Palette.RGB(70, 70, 80));
                img.SetPixel(Mathf.Max(r.Position.X, r.End.X - 1), y, Palette.RGB(70, 70, 80));
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    // ---------------------------------------------------------------- 镜头

    Vector2 ClampCam(Vector2 p)
    {
        float w = RoomDef.Size.X;
        float h = RoomDef.Size.Y;
        float tx = Mathf.Max(0, Mathf.Min(p.X - S.Width / 2f, w - S.Width));
        float ty = Mathf.Max(0, Mathf.Min(p.Y - S.Height / 2f, h - S.Height));
        return new Vector2(tx, ty);
    }

    void SnapCamera()
    {
        _camOffset = ClampCam(Player.Position);
        ApplyCamera();
    }

    void UpdateCamera()
    {
        var t = ClampCam(Player.Position);
        _camOffset += (t - _camOffset) * 0.25f;
        ApplyCamera();
    }

    void ApplyCamera()
    {
        Vector2 shake = Vector2.Zero;
        if (_shake > 0.02f)
        {
            shake = new Vector2(
                GD.RandRange(-Mathf.RoundToInt(_shake * 14), Mathf.RoundToInt(_shake * 14)),
                GD.RandRange(-Mathf.RoundToInt(_shake * 14), Mathf.RoundToInt(_shake * 14)));
        }
        _camera.Position = _camOffset + new Vector2(S.Width / 2f, S.Height / 2f) + shake;
    }

    // ---------------------------------------------------------------- 特效与消息

    public void OnPlayerHit()
    {
        _shake = Mathf.Max(_shake, 0.7f);
        _hitstop = Mathf.Max(_hitstop, S.HitstopHit);
        Particles.Spawn(Player.Position, Palette.RGB(200, 40, 40), 12, 3.5f, 0.4f);
    }

    public void SetHitstop(float t) => _hitstop = Mathf.Max(_hitstop, t);
    public void SetShake(float t) => _shake = Mathf.Max(_shake, t);

    public void Message(string text, Color color, float dur, int size)
    {
        foreach (var m in _messages)
        {
            if (m.Text == text)
            {
                return;
            }
        }
        _messages.Add(new Msg(text, color, dur, dur, size));
    }

    public static string KeyName(Key key) => key.ToString().ToUpper();

    public override void _Draw()
    {
        DrawConnections();
    }

    void DrawConnections()
    {
        foreach (var conn in RoomDef.Connections)
        {
            if (conn.Kind == "fog")
            {
                bool isSealed = conn.Sealed == "boss_alive" && !Boss.Dead;
                DrawFogGate(conn.Rect, isSealed);
            }
            else
            {
                var r = conn.Rect;
                DrawRect(r, Palette.RGB(20, 18, 24));
                DrawRect(r, Palette.RGB(110, 100, 80), false, 3);
                DrawLine(
                    new Vector2(r.Position.X + r.Size.X / 2f, r.Position.Y + 4),
                    new Vector2(r.Position.X + r.Size.X / 2f, r.End.Y - 4),
                    Palette.RGB(150, 138, 110), 2);
            }
        }
    }

    void DrawFogGate(Rect2 rect, bool isSealed)
    {
        var band = new Rect2(rect.Position.X, 200, 36, 340);
        Color fill = isSealed ? Palette.RGB(110, 115, 130, 95) : Palette.RGB(200, 205, 215, 60);
        Color circle = isSealed ? Palette.RGB(150, 155, 170, 80) : Palette.RGB(226, 229, 236, 95);
        DrawRect(band, fill);
        for (int i = 0; i < 6; i++)
        {
            float yy = 30 + Mathf.PosMod(_time * (16 + i * 7) + i * 53, 280);
            float xx = 6 + 24 * (0.5f + 0.5f * Mathf.Sin(_time * 2 + i * 2.1f));
            DrawCircle(new Vector2(band.Position.X + xx, band.Position.Y + yy), 9, circle);
        }
    }

    public void DrawFx(CanvasItem c)
    {
        Particles.Draw(c);
        if (State == GameState.Play)
        {
            DrawInteractHints(c);
        }
    }

    public void DrawProjectiles(CanvasItem c)
    {
        foreach (var pr in _projectiles)
        {
            c.DrawCircle(pr.Pos, pr.R, pr.Color);
        }
    }

    void DrawInteractHints(CanvasItem c)
    {
        if (RoomDef.BonfirePos.HasValue && CanRest())
        {
            DrawInteractHint(c, RoomDef.BonfirePos.Value + new Vector2(0, -95));
        }
        foreach (var conn in RoomDef.Connections)
        {
            var r = conn.Rect.Grow(30);
            if (!r.Intersects(Player.Body, includeBorders: true))
            {
                continue;
            }
            if (conn.Sealed == "boss_alive" && !Boss.Dead)
            {
                continue;
            }
            DrawInteractHint(c, conn.Rect.GetCenter());
            break;
        }
    }

    static void DrawInteractHint(CanvasItem c, Vector2 pos)
    {
        string key = Config.KeyName(Key.E);
        float w = Mathf.Max(30, key.Length * 12 + 16);
        float h = 30;
        var rect = new Rect2(pos.X - w / 2, pos.Y - h / 2, w, h);
        c.DrawRect(rect, Palette.RGB(14, 14, 18));
        c.DrawRect(rect, Palette.RGB(200, 200, 210), false, 2);
        c.DrawString(Fonts.Pick(key), pos + new Vector2(0, 7), key,
            HorizontalAlignment.Center, -1, Fonts.Small, Palette.RGB(235, 235, 240));
    }

    // ---------------------------------------------------------------- UI 绘制

    public void DrawUi(CanvasItem c)
    {
        if (State is GameState.Play or GameState.Pause or GameState.Died or GameState.Inventory)
        {
            DrawHud(c);
            if (RoomId == "hub")
            {
                DrawHubBanner(c);
            }
            else
            {
                DrawBossBanner(c);
            }
            DrawMessages(c);
            if (Config.ShowKeys)
            {
                DrawKeyHints(c);
            }
        }
        if (State == GameState.Died)
        {
            float a = Mathf.Max(0, Mathf.Min(1, (2.4f - _deathTimer) / 0.6f));
            c.DrawString(Fonts.Title, new Vector2(S.Width / 2f, 250), "YOU DIED",
                HorizontalAlignment.Center, -1, Fonts.Huge, Palette.RGB(190, 20, 20, (int)(255 * a)));
        }
        if (State == GameState.Victory)
        {
            DrawVictory(c);
        }
        if (State == GameState.MainMenu)
        {
            c.DrawRect(new Rect2(0, 0, S.Width, S.Height), Palette.RGB(0, 0, 0, 190));
            if (_clMode != null)
            {
                DrawChangelog(c);
            }
            else
            {
                _menu.Draw(c, Fonts.Title);
                bool hover = _versionRect.HasPoint(MousePos);
                string vtext = $"v{S.Version}";
                var vsize = Fonts.Body.GetStringSize(vtext, HorizontalAlignment.Left, -1, Fonts.Small);
                _versionRect = new Rect2(12, S.Height - 28, vsize.X + 8, vsize.Y + 4);
                c.DrawString(Fonts.Body, new Vector2(12, S.Height - 28), vtext,
                    HorizontalAlignment.Left, -1, Fonts.Small,
                    hover ? Palette.Yellow : Palette.RGB(110, 110, 120));
            }
        }
        if (State == GameState.Pause)
        {
            c.DrawRect(new Rect2(0, 0, S.Width, S.Height), Palette.RGB(0, 0, 0, 170));
            _menu.Draw(c);
        }
        if (State == GameState.Inventory)
        {
            DrawInventory(c);
        }
        if (_fade > 0)
        {
            c.DrawRect(new Rect2(0, 0, S.Width, S.Height),
                Palette.RGB(0, 0, 0, (int)(255 * Mathf.Min(1, _fade))));
        }
    }

    void DrawVictory(CanvasItem c)
    {
        c.DrawString(Fonts.Title, new Vector2(S.Width / 2f, 280), I18n.T("victory"),
            HorizontalAlignment.Center, -1, Fonts.Huge, Palette.Yellow);
        c.DrawString(Fonts.Pick(I18n.T("restart")), new Vector2(S.Width / 2f, 380), I18n.T("restart"),
            HorizontalAlignment.Center, -1, Fonts.Big, Palette.White);
        c.DrawString(Fonts.Pick(I18n.T("esc_main_menu")), new Vector2(S.Width / 2f, 440), I18n.T("esc_main_menu"),
            HorizontalAlignment.Center, -1, Fonts.Small, Palette.Gray);
    }

    void DrawHud(CanvasItem c)
    {
        c.DrawString(Fonts.Body, new Vector2(30, 8), I18n.T("unkindled"),
            HorizontalAlignment.Left, -1, Fonts.Small, Palette.Gray);
        int wHp = (int)(260 * (Player.MaxHp() / (float)S.PlayerMaxHp));
        int wSt = (int)(220 * (Player.MaxStamina() / (float)S.PlayerMaxStamina));
        DrawBar(c, 30, 36, wHp, 16, Player.Hp / (float)Player.MaxHp(), Palette.Red, Palette.DarkRed);
        DrawBar(c, 30, 60, wSt, 10, Player.Stamina / (float)Player.MaxStamina(), Palette.Green, Palette.DarkGreen);
        for (int i = 0; i < S.EstusMax; i++)
        {
            DrawFlask(c, 30 + i * 32, 84, i < Player.Estus);
        }
        c.DrawString(Fonts.Body, new Vector2(30, 116), $"{I18n.T("souls")} {Player.Souls}",
            HorizontalAlignment.Left, -1, Fonts.Small, Palette.RGB(235, 210, 120));
        if (RoomId == "arena" && Boss.Aggro && !Boss.Dead)
        {
            int w = 840;
            float x = (S.Width - w) / 2f;
            float y = S.Height - 58;
            c.DrawString(Fonts.Pick(I18n.T("boss_name")), new Vector2(x, y - 8), I18n.T("boss_name"),
                HorizontalAlignment.Left, -1, Fonts.Normal, Palette.White);
            DrawBar(c, (int)x, (int)y, w, 18, Boss.Hp / (float)Boss.MaxHp,
                Palette.RGB(150, 20, 20), Palette.RGB(60, 10, 10));
        }
    }

    static void DrawBar(CanvasItem c, int x, int y, int w, int h, float frac, Color color, Color bg)
    {
        c.DrawRect(new Rect2(x - 2, y - 2, w + 4, h + 4), Palette.RGB(16, 16, 20));
        c.DrawRect(new Rect2(x, y, w, h), bg);
        c.DrawRect(new Rect2(x, y, (int)(w * Mathf.Max(0, Mathf.Min(1, frac))), h), color);
    }

    static void DrawFlask(CanvasItem c, int x, int y, bool filled)
    {
        Color color = filled ? Palette.Orange : Palette.RGB(60, 60, 66);
        c.DrawRect(new Rect2(x, y, 16, 22), color);
        c.DrawRect(new Rect2(x + 5, y - 6, 6, 8), Palette.RGB(200, 200, 210));
    }

    void DrawHubBanner(CanvasItem c)
    {
        if (_hubBanner <= 0)
        {
            return;
        }
        float fadeIn = Mathf.Min(1, (2.6f - _hubBanner) / 0.4f);
        float fadeOut = Mathf.Min(1, _hubBanner / 0.5f);
        int alpha = (int)(255 * Mathf.Min(fadeIn, fadeOut));
        c.DrawString(Fonts.Pick(I18n.T(_roomBannerKey ?? "firelink")),
            new Vector2(S.Width / 2f, 190), I18n.T(_roomBannerKey ?? "firelink"),
            HorizontalAlignment.Center, -1, Fonts.Big, Palette.RGB(200, 200, 210, alpha));
    }

    void DrawBossBanner(CanvasItem c)
    {
        float bt = Boss.AggroBanner;
        if (bt <= 0)
        {
            return;
        }
        float fadeIn = Mathf.Min(1, (2.4f - bt) / 0.4f);
        float fadeOut = Mathf.Min(1, bt / 0.5f);
        int alpha = (int)(255 * Mathf.Min(fadeIn, fadeOut));
        c.DrawString(Fonts.Title, new Vector2(S.Width / 2f, 190), I18n.T("boss_name"),
            HorizontalAlignment.Center, -1, Fonts.Huge, Palette.RGB(220, 60, 40, alpha));
    }

    void DrawMessages(CanvasItem c)
    {
        float y = 310;
        foreach (var m in _messages)
        {
            int alpha = m.Time > 0.3f ? 255 : (int)(255 * m.Time / 0.3f);
            var col = new Color(m.Color.R, m.Color.G, m.Color.B, alpha / 255f);
            c.DrawString(Fonts.Pick(m.Text), new Vector2(S.Width / 2f, y), m.Text,
                HorizontalAlignment.Center, -1, m.Size, col);
            y += 36;
        }
    }

    void DrawKeyHints(CanvasItem c)
    {
        var lines = new List<string>
        {
            $"{Config.KeyName(Config.Bindings["attack"])}  {I18n.T("attack")}",
            $"{Config.KeyName(Config.Bindings["roll"])}  {I18n.T("roll")}",
            $"{Config.KeyName(Config.Bindings["offhand"])}  {I18n.T("slot_offhand")}",
            $"{Config.KeyName(Config.Bindings["estus"])}  {I18n.T("estus")}",
            $"{Config.KeyName(Config.Bindings["interact"])}  {I18n.T("rest")}",
            $"{Config.KeyName(Config.Bindings["sprint"])}  {I18n.T("sprint")}",
            $"{Config.KeyName(Config.Bindings["inventory"])}  {I18n.T("inventory")}",
        };
        float w = 0;
        foreach (var line in lines)
        {
            w = Mathf.Max(w, Fonts.Pick(line).GetStringSize(line, HorizontalAlignment.Left, -1, Fonts.Small).X + 20);
        }
        float h = lines.Count * 22 + 14;
        var panel = new Rect2(S.Width - w - 12, 12, w, h);
        c.DrawRect(panel, Palette.RGB(10, 10, 14, 150));
        c.DrawRect(panel, Palette.RGB(90, 90, 100), false, 1);
        float y = 20;
        foreach (var line in lines)
        {
            c.DrawString(Fonts.Pick(line), new Vector2(panel.Position.X + 6, y), line,
                HorizontalAlignment.Left, -1, Fonts.Small, Palette.RGB(195, 195, 205));
            y += 22;
        }
    }

    void DrawChangelog(CanvasItem c)
    {
        var f = Fonts.Zh;
        if (_clMode == "list")
        {
            c.DrawString(f, new Vector2(S.Width / 2f, 110), I18n.T("changelog"),
                HorizontalAlignment.Center, -1, Fonts.Big, Palette.RGB(220, 220, 228));
            var vers = ChangelogVersions();
            float maxScroll = Mathf.Max(0, vers.Count * 48 - 440);
            _clScroll = Mathf.Clamp(_clScroll, 0, maxScroll);
            _clRects.Clear();
            float y = 180 - _clScroll;
            for (int i = 0; i < vers.Count; i++)
            {
                bool sel = i == _clSel;
                Color color = sel ? Palette.Yellow : Palette.RGB(160, 160, 170);
                string label = sel ? $"> v{vers[i]} <" : $"v{vers[i]}";
                var size = f.GetStringSize(label, HorizontalAlignment.Left, -1, Fonts.Normal);
                var rect = new Rect2(S.Width / 2f - size.X / 2f, y - size.Y / 2f, size.X, size.Y);
                if (y > 120 && y < 650)
                {
                    c.DrawString(f, new Vector2(S.Width / 2f, y), label,
                        HorizontalAlignment.Center, -1, Fonts.Normal, color);
                }
                _clRects.Add(rect.GrowIndividual(20, 8, 20, 8));
                y += 48;
            }
            c.DrawString(f, new Vector2(S.Width / 2f, 690), $"ESC  {I18n.T("back")}",
                HorizontalAlignment.Center, -1, Fonts.Small, Palette.RGB(110, 110, 120));
        }
        else
        {
            c.DrawString(f, new Vector2(S.Width / 2f, 110), $"v{_clVer}",
                HorizontalAlignment.Center, -1, Fonts.Big, Palette.Yellow);
            float maxScroll = Mathf.Max(0, _clLines.Count * 36 - 500);
            _clScroll = Mathf.Clamp(_clScroll, 0, maxScroll);
            float y = 180 - _clScroll;
            foreach (var line in _clLines)
            {
                if (y > 120 && y < 650)
                {
                    c.DrawString(f, new Vector2(S.Width / 2f - 420, y), line,
                        HorizontalAlignment.Left, -1, Fonts.Normal, Palette.RGB(205, 205, 213));
                }
                y += 36;
            }
            c.DrawString(f, new Vector2(S.Width / 2f, 690), $"ESC  {I18n.T("back")}",
                HorizontalAlignment.Center, -1, Fonts.Small, Palette.RGB(110, 110, 120));
        }
    }

    void DrawInventory(CanvasItem c)
    {
        var p = Player;
        c.DrawRect(new Rect2(0, 0, S.Width, S.Height), Palette.RGB(0, 0, 0, 200));

        c.DrawRect(new Rect2(30, 40, 400, 640), Palette.RGB(22, 22, 30, 225));
        c.DrawRect(new Rect2(30, 40, 400, 640), Palette.RGB(90, 90, 100), false, 2);
        c.DrawString(Fonts.Pick(I18n.T("inventory")), new Vector2(230, 70), I18n.T("inventory"),
            HorizontalAlignment.Center, -1, Fonts.Normal, Palette.RGB(220, 220, 228));

        DrawCharacterModel(c, 230, 165);
        string eqName = p.Equipment["mainhand"] != null
            ? ItemsDb.Name(p.Equipment["mainhand"]) : I18n.T("empty");
        DrawFitText(c, eqName, 230, 288, 200, Fonts.Small, Palette.RGB(200, 200, 210));

        var slotDefs = new List<(string, string)>
        {
            ("mainhand", I18n.T("slot_mainhand")),
            ("offhand", I18n.T("slot_offhand")),
            ("armor", I18n.T("slot_armor")),
            ("talisman1", I18n.T("slot_talisman")),
            ("talisman2", I18n.T("slot_talisman")),
            ("talisman3", I18n.T("slot_talisman")),
        };
        _invEquipCells.Clear();
        for (int i = 0; i < slotDefs.Count; i++)
        {
            var (key, label) = slotDefs[i];
            int col = i % 3;
            int row = i / 3;
            float cx = 109 + col * 121;
            float cy = 375 + row * 121;
            var rect = new Rect2(cx - 52, cy - 52, 104, 104);
            c.DrawRect(rect, Palette.RGB(35, 35, 44));
            if (key == _invSelectedSlot)
            {
                c.DrawRect(rect, Palette.White, false, 3);
            }
            else
            {
                c.DrawRect(rect, Palette.RGB(110, 110, 122), false, 2);
            }
            c.DrawString(Fonts.Pick(label), new Vector2(cx, cy - 36), label,
                HorizontalAlignment.Center, -1, Fonts.Small, Palette.RGB(150, 150, 160));
            string itemId = p.Equipment[key];
            if (itemId != null)
            {
                DrawItemIcon(c, itemId, cx, cy + 6);
                DrawFitText(c, ItemsDb.Name(itemId), cx, cy + 34, 92, Fonts.Small, Palette.RGB(225, 225, 232));
            }
            else
            {
                c.DrawString(Fonts.Body, new Vector2(cx, cy + 8), "-",
                    HorizontalAlignment.Center, -1, Fonts.Normal, Palette.RGB(90, 90, 98));
            }
            _invEquipCells.Add((rect, key, itemId));
        }

        if (_invSelectedSlot != null)
        {
            c.DrawString(Fonts.Pick(I18n.T("select_item_hint")), new Vector2(230, 660), I18n.T("select_item_hint"),
                HorizontalAlignment.Center, -1, Fonts.Small, Palette.Yellow);
        }

        c.DrawRect(new Rect2(460, 40, 790, 640), Palette.RGB(22, 22, 30, 225));
        c.DrawRect(new Rect2(460, 40, 790, 640), Palette.RGB(90, 90, 100), false, 2);

        var tabs = new List<string>
        {
            I18n.T("cat_all"), I18n.T("cat_weapons"), I18n.T("cat_armor"), I18n.T("cat_talismans"),
        };
        _invTabRects.Clear();
        for (int i = 0; i < tabs.Count; i++)
        {
            var rect = new Rect2(480 + i * 158, 60, 150, 36);
            bool sel = i == _invTab;
            c.DrawRect(rect, sel ? Palette.RGB(60, 60, 74) : Palette.RGB(35, 35, 44));
            c.DrawRect(rect, sel ? Palette.Yellow : Palette.RGB(110, 110, 122), false, 2);
            c.DrawString(Fonts.Pick(tabs[i]), rect.GetCenter(), tabs[i],
                HorizontalAlignment.Center, -1, Fonts.Small, sel ? Palette.Yellow : Palette.RGB(200, 200, 210));
            _invTabRects.Add(rect);
        }

        string[] catType = { null, "weapon", "armor", "talisman" };
        var owned = new List<string>();
        foreach (var id in p.Inventory)
        {
            if (ItemsDb.Get(id) != null)
            {
                owned.Add(id);
            }
        }
        if (catType[_invTab] != null)
        {
            owned.RemoveAll(id => ItemsDb.Get(id)?["type"]?.GetValue<string>() != catType[_invTab]);
        }

        _invCells.Clear();
        for (int idx = 0; idx < owned.Count; idx++)
        {
            string itemId = owned[idx];
            int col = idx % 6;
            int row = idx / 6;
            float cx = 486 + col * 116 + 52;
            float cy = 130 + row * 116 + 52;
            var rect = new Rect2(cx - 52, cy - 52, 104, 104);
            bool equipped = p.Equipment.ContainsValue(itemId);
            bool compat = _invSelectedSlot != null && SlotAccepts(_invSelectedSlot, itemId);
            c.DrawRect(rect, Palette.RGB(35, 35, 44));
            if (compat)
            {
                c.DrawRect(rect, Palette.White, false, 3);
            }
            else if (equipped)
            {
                c.DrawRect(rect, Palette.Yellow, false, 2);
            }
            else
            {
                c.DrawRect(rect, Palette.RGB(110, 110, 122), false, 2);
            }
            DrawItemIcon(c, itemId, cx, cy - 4);
            DrawFitText(c, ItemsDb.Name(itemId), cx, cy + 34, 92, Fonts.Small, Palette.RGB(225, 225, 232));
            _invCells.Add((rect, itemId));
        }

        if (_invHover != null)
        {
            DrawTooltip(c, TooltipLines(_invHover));
        }
    }

    void DrawTooltip(CanvasItem c, List<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }
        const float pad = 10;
        float maxW = 0;
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                continue;
            }
            var font = Fonts.Pick(line);
            maxW = Mathf.Max(maxW, font.GetStringSize(line, HorizontalAlignment.Left, -1, Fonts.Small).X);
        }
        float w = Mathf.Min(300, maxW + pad * 2);
        float h = lines.Count * 24 + pad * 2;
        var mp = MousePos;
        float tx = mp.X + 20;
        float ty = mp.Y + 20;
        if (tx + w > S.Width - 10)
        {
            tx = mp.X - w - 20;
        }
        if (ty + h > S.Height - 10)
        {
            ty = mp.Y - h - 20;
        }
        c.DrawRect(new Rect2(tx, ty, w, h), Palette.RGB(10, 10, 15, 238));
        c.DrawRect(new Rect2(tx, ty, w, h), Palette.RGB(160, 160, 172), false, 1);
        float y = ty + pad;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Length == 0)
            {
                y += 8;
                continue;
            }
            Color color = i == 0 ? Palette.Yellow : Palette.RGB(205, 205, 213);
            c.DrawString(Fonts.Pick(lines[i]), new Vector2(tx + pad, y), lines[i],
                HorizontalAlignment.Left, -1, Fonts.Small, color);
            y += 24;
        }
    }

    void DrawFitText(CanvasItem c, string text, float cx, float cy, float maxW, int fontSize, Color color)
    {
        var font = Fonts.Pick(text);
        float w = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X;
        int size = w > maxW ? Mathf.Max(8, (int)(fontSize * maxW / w)) : fontSize;
        c.DrawString(font, new Vector2(cx, cy), text, HorizontalAlignment.Center, -1, size, color);
    }

    void DrawItemIcon(CanvasItem c, string itemId, float cx, float cy)
    {
        var item = ItemsDb.Get(itemId);
        if (item == null)
        {
            return;
        }
        if (item["subtype"]?.GetValue<string>() == "shield")
        {
            c.DrawCircle(new Vector2(cx, cy), 15, Palette.RGB(70, 70, 82));
            c.DrawCircle(new Vector2(cx, cy), 15, Palette.RGB(210, 210, 220), false, 3);
            c.DrawCircle(new Vector2(cx, cy), 7, Palette.RGB(150, 150, 165));
            c.DrawCircle(new Vector2(cx, cy), 3, Palette.RGB(110, 110, 122));
        }
        else if (item["type"]?.GetValue<string>() == "weapon")
        {
            c.DrawLine(new Vector2(cx - 18, cy + 16), new Vector2(cx + 18, cy - 16), Palette.RGB(210, 210, 220), 5);
            c.DrawLine(new Vector2(cx - 28, cy + 22), new Vector2(cx - 12, cy + 12), Palette.RGB(140, 110, 60), 6);
        }
        else if (item["type"]?.GetValue<string>() == "armor")
        {
            c.DrawRect(new Rect2(cx - 16, cy - 18, 32, 36), Palette.RGB(150, 150, 165));
            c.DrawRect(new Rect2(cx - 8, cy - 10, 16, 20), Palette.RGB(90, 90, 100));
        }
        else
        {
            c.DrawCircle(new Vector2(cx, cy), 13, Palette.RGB(235, 170, 70), false, 3);
            c.DrawCircle(new Vector2(cx, cy), 4, Palette.RGB(235, 170, 70));
        }
    }

    static void DrawCharacterModel(CanvasItem c, float cx, float cy)
    {
        DrawSetTransformHelper(c, new Vector2(cx, cy + 78), new Vector2(1, 22f / 110f));
        c.DrawCircle(Vector2.Zero, 55, Palette.RGB(20, 20, 26));
        c.DrawSetTransform(Vector2.Zero);
        c.DrawRect(new Rect2(cx - 32, cy - 20, 64, 100), Palette.RGB(68, 68, 80));
        c.DrawRect(new Rect2(cx - 26, cy - 12, 52, 60), Palette.RGB(48, 48, 56));
        c.DrawCircle(new Vector2(cx, cy - 38), 22, Palette.RGB(150, 140, 128));
        c.DrawRect(new Rect2(cx - 28, cy - 46, 56, 14), Palette.RGB(60, 60, 70));
        c.DrawLine(new Vector2(cx + 40, cy), new Vector2(cx + 88, cy - 40), Palette.RGB(200, 200, 214), 7);
        c.DrawLine(new Vector2(cx + 30, cy + 12), new Vector2(cx + 46, cy - 2), Palette.RGB(140, 110, 60), 6);
    }

    static void DrawSetTransformHelper(CanvasItem c, Vector2 pos, Vector2 scale)
    {
        c.DrawSetTransform(pos, 0, scale);
    }
}

/// <summary>世界层特效(粒子/交互提示),Z 序在实体之上。</summary>
public partial class FxLayer : Node2D
{
    readonly MainGame _game;

    public FxLayer(MainGame game)
    {
        _game = game;
    }

    public override void _Draw()
    {
        _game.DrawFx(this);
    }
}

/// <summary>弹丸层(小怪之上、玩家之下)。</summary>
public partial class ProjLayer : Node2D
{
    readonly MainGame _game;

    public ProjLayer(MainGame game)
    {
        _game = game;
    }

    public override void _Draw()
    {
        _game.DrawProjectiles(this);
    }
}

/// <summary>屏幕层 UI 绘制(CanvasLayer,对应旧 pygame 的 canvas 叠加层)。</summary>
public partial class UiOverlay : Control
{
    readonly MainGame _game;

    public UiOverlay(MainGame game)
    {
        _game = game;
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(S.Width, S.Height);
    }

    public override void _Draw()
    {
        _game.DrawUi(this);
    }
}
