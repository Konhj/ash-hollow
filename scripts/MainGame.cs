using System.Collections.Generic;
using Godot;

namespace AshHollow;

/// <summary>
/// 主场景根节点:全局游戏编排(对应旧 pygame main.py 的 Game 类)。
/// 世界坐标 = 房间坐标;Camera2D 跟随玩家;UI 在 CanvasLayer。
/// </summary>
public partial class MainGame : Node2D
{
    public enum GameState { Play, Pause, Died, Victory, Inventory, MainMenu }

    public GameState State = GameState.Play;
    public float ShowKeys;

    public string RoomId;
    public RoomDef RoomDef;
    public Player Player;
    public Bonfire Bonfire;
    public readonly Particles Particles = new();

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
    readonly Dictionary<string, ImageTexture> _floorCache = new();
    readonly Dictionary<string, Key> _bindings = new()
    {
        { "move_up", Key.W }, { "move_down", Key.S }, { "move_left", Key.A }, { "move_right", Key.D },
        { "attack", Key.J }, { "roll", Key.K }, { "offhand", Key.L }, { "estus", Key.F },
        { "interact", Key.E }, { "sprint", Key.Shift }, { "inventory", Key.Tab },
    };
    readonly Dictionary<Key, bool> _prevKeys = new();

    public override void _Ready()
    {
        DataTables.LoadAll();
        S.Init();
        Palette.Init();
        Fonts.Load();
        ItemsDb.Load();
        RoomDb.Load();

        _floorSprite = new Sprite2D { Centered = false };
        AddChild(_floorSprite);

        Bonfire = new Bonfire();
        AddChild(Bonfire);

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
        _fade = 1f;

        GD.Print($"[AshHollow] v{S.Version} room={RoomId}");
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _fade = Mathf.Max(0, _fade - dt * 1.5f);
        _shake = Mathf.Max(0, _shake - dt * 2.2f);

        switch (State)
        {
            case GameState.Play: UpdatePlay(dt); break;
            case GameState.Died: UpdateDied(dt); break;
            case GameState.Victory: Particles.Update(dt); break;
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

    bool Just(string action)
    {
        var key = _bindings[action];
        bool down = Input.IsKeyPressed(key);
        bool prev = _prevKeys.TryGetValue(key, out var p) && p;
        _prevKeys[key] = down;
        return down && !prev;
    }

    bool Held(string action) => Input.IsKeyPressed(_bindings[action]);

    void UpdatePlay(float dt)
    {
        Just("inventory"); // 边缘消抖(背包 UI 后续阶段接入)

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
        Particles.Update(dt);

        Player.Clamp();
        Collision.ResolveTerrain(Player, RoomDef.SolidRects);
        UpdateCamera();

        if (RoomDef.BonfirePos.HasValue && GD.Randf() < 0.35f)
        {
            var b = RoomDef.BonfirePos.Value;
            Particles.Spawn(new Vector2(b.X + GD.RandRange(-8, 8), b.Y - 10),
                Palette.RGB(255, 170, 50), 1, 1.2f, 0.8f);
        }

        bool attackHeld = Held("attack");
        if ((Player.State == "attack" || Player.State == "roll" || Player.State == "guard") && attackHeld)
        {
            _inputBuffer = ("attack", S.InputBufferTime);
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
                var r = Player.StartAttack();
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
    }

    void UpdateDied(float dt)
    {
        _deathTimer -= dt;
        Particles.Update(dt);
        if (_deathTimer <= 0)
        {
            Player.Respawn();
            EnterRoom("hub", RoomDb.Get("hub").PlayerSpawn);
            State = GameState.Play;
            _fade = 1f;
        }
    }

    bool CanFight() => RoomDef.Combat;

    bool CanRest()
    {
        if (!RoomDef.BonfirePos.HasValue || Player.Dead)
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
        Player.Hp = Player.MaxHp();
        Player.Estus = S.EstusMax;
        _fade = 1f;
        Message(I18n.T("game_saved"), Palette.Orange, 1.6f, Fonts.Big);
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
            if (conn.Sealed == "boss_alive" && !_bossDefeated)
            {
                continue;
            }
            if (RoomId == "arena" && conn.To == "hub")
            {
                ResetBoss();
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
        Player.Position = spawn;
        _connCooldown = 0.4f;
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

    public string KeyName(string action) => _bindings[action].ToString().ToUpper();

    public static string KeyName(Key key) => key.ToString().ToUpper();

    bool _bossDefeated => false; // Stage B: Boss 死亡状态

    void ResetBoss()
    {
        // Stage B: Boss.Reset()
    }

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
                bool isSealed = conn.Sealed == "boss_alive" && !_bossDefeated;
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

    /// <summary>世界层特效与交互提示(在实体之上绘制)。</summary>
    public void DrawFx(CanvasItem c)
    {
        Particles.Draw(c);
        if (State == GameState.Play)
        {
            DrawInteractHints(c);
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
            if (conn.Sealed == "boss_alive" && !_bossDefeated)
            {
                continue;
            }
            DrawInteractHint(c, conn.Rect.GetCenter());
            break;
        }
    }

    static void DrawInteractHint(CanvasItem c, Vector2 pos)
    {
        string key = KeyName(Key.E);
        float w = Mathf.Max(30, key.Length * 12 + 16);
        float h = 30;
        var rect = new Rect2(pos.X - w / 2, pos.Y - h / 2, w, h);
        c.DrawRect(rect, Palette.RGB(14, 14, 18));
        c.DrawRect(rect, Palette.RGB(200, 200, 210), false, 2);
        c.DrawString(Fonts.Pick(key), pos + new Vector2(0, 7), key,
            HorizontalAlignment.Center, -1, Fonts.Small, Palette.RGB(235, 235, 240));
    }

    /// <summary>HUD/横幅/消息/死亡遮罩(CanvasLayer,屏幕坐标)。</summary>
    public void DrawUi(CanvasItem c)
    {
        if (State is GameState.Play or GameState.Pause or GameState.Died or GameState.Inventory)
        {
            DrawHud(c);
            if (RoomId == "hub")
            {
                DrawHubBanner(c);
            }
            DrawMessages(c);
            if (ShowKeys > 0)
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
        if (_fade > 0)
        {
            c.DrawRect(new Rect2(0, 0, S.Width, S.Height),
                Palette.RGB(0, 0, 0, (int)(255 * Mathf.Min(1, _fade))));
        }
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
            $"{KeyName("attack")}  {I18n.T("attack")}",
            $"{KeyName("roll")}  {I18n.T("roll")}",
            $"{KeyName("offhand")}  {I18n.T("slot_offhand")}",
            $"{KeyName("estus")}  {I18n.T("estus")}",
            $"{KeyName("interact")}  {I18n.T("rest")}",
            $"{KeyName("sprint")}  {I18n.T("sprint")}",
            $"{KeyName("inventory")}  {I18n.T("inventory")}",
        };
        float w = 0;
        foreach (var line in lines)
        {
            w = Mathf.Max(w, line.Length * 10 + 20);
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
}

/// <summary>世界层特效(粒子/弹丸/交互提示),Z 序在实体之上。</summary>
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
