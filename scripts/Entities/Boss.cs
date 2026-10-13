using System;
using System.Collections.Generic;
using Godot;

namespace AshHollow;

/// <summary>
/// Boss:灰烬猎人(对应旧 boss.py)。挥砍/冲锋/跳砸 + 二阶段炸气。
/// </summary>
public partial class Boss : Entity
{
    static readonly Dictionary<string, float> Windup = new()
    {
        { "slash", 0.55f }, { "charge", 0.75f }, { "slam", 0.95f }, { "blast", 1.0f },
    };
    static readonly Dictionary<string, float> Duration = new()
    {
        { "slash", 0.18f }, { "charge", 0.5f }, { "slam", 0.12f }, { "blast", 0.25f },
    };
    static readonly Dictionary<string, float> Recover = new()
    {
        { "slash", 0.55f }, { "charge", 0.6f }, { "slam", 1.0f }, { "blast", 0.9f },
    };

    public Vector2 WorldBounds;
    public bool HasPhase2 = true;

    public int Hp, MaxHp;
    public int Phase = 1;
    public bool Phase2BlastPending;
    public string State = "idle";
    public float StateTimer;
    public string AttackKind;
    public bool Aggro;
    public float AggroBanner;
    public float AttackCd = 2.4f;
    public Vector2 Dir = Vector2.Left;
    public Vector2 ChargeDir = Vector2.Left;
    public bool HitLanded;
    public bool SlamLanded = true;
    public bool BlastLanded = true;
    public float Flash;
    public float AnimT;

    Vector2 _spawn;

    public override void _Ready()
    {
        base._Ready();
        Size = new Vector2(64, 110);
        SetCollisionShape(Size);
        _spawn = Position;
        Reset();
    }

    public void Reset()
    {
        Hp = S.BossMaxHp;
        MaxHp = S.BossMaxHp;
        Phase = 1;
        Phase2BlastPending = false;
        State = "idle";
        StateTimer = 0;
        AttackKind = null;
        Aggro = false;
        AggroBanner = 0;
        AttackCd = 2.4f;
        Dir = Vector2.Left;
        ChargeDir = Vector2.Left;
        HitLanded = false;
        SlamLanded = true;
        BlastLanded = true;
        Flash = 0;
        AnimT = 0;
        Dead = false;
        Position = _spawn;
    }

    public void Clamp()
    {
        var w = WorldBounds.X;
        var h = WorldBounds.Y;
        Position = new Vector2(
            Mathf.Max(48f, Mathf.Min(w - 48f, Position.X)),
            Mathf.Max(71f, Mathf.Min(h - 71f, Position.Y)));
    }

    public void Stagger(float t)
    {
        State = "staggered";
        StateTimer = t;
        AttackKind = null;
    }

    public void TakeDamage(int dmg)
    {
        if (Dead)
        {
            return;
        }
        Hp = Mathf.Max(0, Hp - dmg);
        Flash = 0.12f;
        if (Hp <= 0)
        {
            Dead = true;
            State = "dying";
            StateTimer = 1.6f;
            DisableCollision();
            return;
        }
        if (HasPhase2 && Phase == 1 && Hp <= MaxHp * S.Phase2HpFraction)
        {
            Phase = 2;
            Phase2BlastPending = true;
        }
    }

    public Rect2? CurrentHitRect()
    {
        if (State != "attack" || AttackKind != "slash")
        {
            return null;
        }
        var hr = new Rect2(0, 0, 130, 100);
        return new Rect2(Position + new Vector2(Dir.X * 75, Dir.Y * 25) - hr.Size / 2f, hr.Size);
    }

    void ChooseAttack(float dist)
    {
        float r = GD.Randf();
        string kind;
        if (Phase == 2 && r < 0.25f)
        {
            kind = "blast";
        }
        else if (dist > 280)
        {
            kind = r < 0.75f ? "charge" : "slam";
        }
        else if (dist > 130)
        {
            kind = r < 0.4f ? "slash" : r < 0.75f ? "slam" : "charge";
        }
        else
        {
            kind = r < 0.7f ? "slash" : "slam";
        }
        AttackKind = kind;
        State = "windup";
        StateTimer = Windup[kind];
        if (Phase == 2 && kind != "blast")
        {
            StateTimer *= S.Phase2WindupMult;
        }
        if (kind == "charge")
        {
            ChargeDir = Dir;
        }
    }

    public void Update(float dt, Player player)
    {
        Flash = Mathf.Max(0, Flash - dt);
        AnimT += dt;
        if (State == "dying")
        {
            StateTimer -= dt;
            return;
        }

        if (!Aggro)
        {
            if (Position.DistanceTo(player.Position) < 430)
            {
                Aggro = true;
                AggroBanner = 2.4f;
            }
        }
        else
        {
            AggroBanner = Mathf.Max(0, AggroBanner - dt);
        }
        if (AggroBanner > 0)
        {
            return;
        }

        var to = player.Position - Position;
        float dist = to.Length();
        if (dist > 0)
        {
            Dir = to.Normalized();
        }

        if (Phase2BlastPending && (State == "idle" || State == "recover"))
        {
            Phase2BlastPending = false;
            State = "windup";
            AttackKind = "blast";
            StateTimer = Windup["blast"];
        }

        switch (State)
        {
            case "idle":
                if (dist > 130)
                {
                    Velocity = Dir * S.BossSpeed * 60f;
                    MoveAndSlide();
                    Clamp();
                }
                AttackCd -= dt;
                if (AttackCd <= 0)
                {
                    ChooseAttack(dist);
                }
                break;
            case "windup":
                StateTimer -= dt;
                if (AttackKind == "slam" && Phase == 2)
                {
                    if (dist > 0)
                    {
                        Dir = (player.Position - Position).Normalized();
                    }
                    Velocity = Dir * S.SlamTrackSpeed * 60f;
                    MoveAndSlide();
                    Clamp();
                }
                if (StateTimer <= 0)
                {
                    State = "attack";
                    StateTimer = Duration[AttackKind];
                    HitLanded = false;
                    SlamLanded = false;
                    BlastLanded = false;
                }
                break;
            case "attack":
                StateTimer -= dt;
                if (AttackKind == "charge")
                {
                    float elapsed = Duration["charge"] - StateTimer;
                    float p = Mathf.Min(1, Mathf.Max(0, elapsed / Duration["charge"]));
                    float spd = S.ChargeSpeed * 6f * p * (1f - p);
                    Velocity = ChargeDir * spd * 60f;
                    MoveAndSlide();
                    Clamp();
                }
                if (StateTimer <= 0)
                {
                    State = "recover";
                    StateTimer = Recover[AttackKind];
                }
                break;
            case "recover":
                StateTimer -= dt;
                if (StateTimer <= 0)
                {
                    State = "idle";
                    AttackCd = (float)GD.RandRange(1.4f, 2.6f);
                }
                break;
            case "staggered":
                StateTimer -= dt;
                if (StateTimer <= 0)
                {
                    State = "recover";
                    StateTimer = 0.8f;
                }
                break;
        }
    }

    public override void _Draw()
    {
        float alpha = State == "dying" ? Mathf.Max(0, Mathf.Min(1, StateTimer / 1.6f)) : 1f;
        if (alpha <= 0)
        {
            return;
        }
        float dx = Dir.X;
        float dy = Dir.Y;

        float dyOff = 0;
        if (State == "windup" && AttackKind == "slam")
        {
            float p = 1 - StateTimer / Windup["slam"];
            dyOff = -150 * p;
        }
        else if (State == "attack" && AttackKind == "slam")
        {
            float p = 1 - Mathf.Max(0, StateTimer) / Duration["slam"];
            dyOff = -150 * (1 - p);
        }

        if (dyOff < -20)
        {
            DrawSetTransform(new Vector2(Position.X, Position.Y + 55 + dyOff), 0, new Vector2(1, 20f / 90f));
            DrawCircle(Vector2.Zero, 45, Palette.RGB(0, 0, 0, 120));
            DrawSetTransform(Vector2.Zero);
        }

        if (State == "attack" && AttackKind == "slam")
        {
            float p = 1 - Mathf.Max(0, StateTimer) / Duration["slam"];
            DrawCircle(new Vector2(Position.X, Position.Y + 55 + dyOff), 20 + 160 * p,
                Palette.RGB(240, 150, 60, (int)(150 * (1 - p))), false, 6);
        }
        if (State == "attack" && AttackKind == "blast")
        {
            float p = 1 - Mathf.Max(0, StateTimer) / Duration["blast"];
            DrawCircle(Position + new Vector2(0, dyOff), 30 + S.BlastRadius * p,
                Palette.RGB(255, 70, 60, (int)(200 * (1 - p))), false, 10);
            DrawCircle(Position + new Vector2(0, dyOff), Mathf.Max(8, 30 + S.BlastRadius * p - 24),
                Palette.RGB(255, 140, 120, (int)(120 * (1 - p))), false, 4);
        }

        var off = Position + new Vector2(-80, -61 + dyOff);

        if (State == "staggered")
        {
            DrawRect(new Rect2(off + new Vector2(44, 62), new Vector2(72, 48)), Palette.RGB(150, 100, 40));
            DrawCircle(off + new Vector2(80, 52), 14, Palette.RGB(180, 130, 60));
            return;
        }

        Color body = Flash > 0 ? Palette.RGB(220, 210, 210) : Palette.RGB(108, 24, 24);
        if (State == "windup" && AttackKind == "blast")
        {
            float flick = 0.5f + 0.5f * Mathf.Sin(AnimT * 30);
            body = Palette.RGB(255, (int)(30 + 120 * flick), (int)(30 + 120 * flick));
        }
        DrawRect(new Rect2(off + new Vector2(46, 36), new Vector2(68, 74)), body);
        DrawRect(new Rect2(off + new Vector2(54, 46), new Vector2(52, 40)), Palette.RGB(70, 14, 14));
        DrawCircle(off + new Vector2(80, 28), 15, Palette.RGB(46, 46, 56));
        DrawCircle(off + new Vector2(80 + dx * 6, 26 + dy * 6), 3, Palette.RGB(255, 120, 40));
        DrawCircle(off + new Vector2(80 + dx * 11, 26 + dy * 11), 3, Palette.RGB(255, 120, 40));

        Color sc = Palette.RGB(150, 150, 164);
        if (State == "windup")
        {
            float flick = 0.5f + 0.5f * Mathf.Sin(AnimT * 40);
            sc = Palette.RGB(255, (int)(60 + 100 * flick), (int)(60 + 100 * flick));
        }
        else if (State == "attack" && AttackKind == "slash")
        {
            sc = Palette.RGB(240, 230, 240);
        }
        var by = off + new Vector2(80, 100);
        if (State == "windup" && AttackKind == "slash")
        {
            DrawLine(by + new Vector2(0, 16), by + new Vector2(0, -88), sc, 9);
        }
        else
        {
            DrawLine(by + new Vector2(dx * 16, dy * 16), by + new Vector2(dx * 70, dy * 70), sc, 9);
        }

        if (State == "attack" && AttackKind == "slash")
        {
            DrawSlashEffect();
        }
    }

    void DrawSlashEffect()
    {
        float ang = Mathf.Atan2(Dir.Y, Dir.X);
        float frac = (Duration["slash"] - Mathf.Max(0, StateTimer)) / Duration["slash"];
        DrawSetTransform(Position, ang);
        float[] offs = { 0f, -0.15f, -0.3f };
        for (int i = 0; i < offs.Length; i++)
        {
            float p = Mathf.Clamp(frac + offs[i], 0, 1);
            float a1 = Mathf.DegToRad(-60 + 120 * p - 12);
            float a2 = Mathf.DegToRad(-60 + 120 * p + 12);
            DrawArc(Vector2.Zero, 120, a1, a2, 32, Palette.RGB(255, 95, 75, 220 - i * 85), 7);
            DrawArc(Vector2.Zero, 106, a1, a2, 32, Palette.RGB(255, 150, 130, 110 - i * 42), 4);
        }
        DrawSetTransform(Vector2.Zero);
    }
}
