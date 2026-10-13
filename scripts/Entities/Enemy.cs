using System.Text.Json.Nodes;
using Godot;

namespace AshHollow;

/// <summary>敌人数据表访问器(对应旧 enemies.py 的 ENEMIES 表)。</summary>
public static class EnemyDb
{
    public static JsonObject Table;

    public static void Load() => Table = DataTables.Enemies as JsonObject;

    public static JsonObject Get(string id) => Table?[id] as JsonObject;

    public static int MaxHp(JsonObject d) => (int)d["max_hp"].GetValue<double>();

    public static int Souls(JsonObject d) => (int)d["souls"].GetValue<double>();

    public static Vector2 Size(JsonObject d)
    {
        var a = d["size"].AsArray();
        return new Vector2((float)a[0].GetValue<double>(), (float)a[1].GetValue<double>());
    }

    public static Color Color(JsonObject d)
    {
        var a = d["color"].AsArray();
        return Palette.RGB((int)a[0].GetValue<double>(), (int)a[1].GetValue<double>(), (int)a[2].GetValue<double>());
    }

    public static double Num(JsonNode o, string key, double fallback)
    {
        if (o is JsonObject obj && obj[key] is JsonValue v)
        {
            return v.GetValue<double>();
        }
        return fallback;
    }

    public static bool Flag(JsonNode o, string key)
    {
        return o is JsonObject obj && obj[key]?.GetValue<bool>() == true;
    }

    public static double Arr(JsonNode o, string key, int i, double fallback)
    {
        if (o is JsonObject obj && obj[key] is JsonArray a && a.Count > i)
        {
            return a[i].GetValue<double>();
        }
        return fallback;
    }
}

/// <summary>
/// 小怪(对应旧 enemies.py):通用 Enemy 类 + 数据表驱动
/// (巡逻 pingpong/route、仇恨半径、追击上限、脱战回血、近战/远程弹丸)。
/// </summary>
public partial class Enemy : Entity
{
    public string Eid;
    public int Index;
    public JsonObject Data;
    public Vector2 Home;
    public int Hp;
    public string State = "patrol";
    public float StateTimer;
    public string AttackKind;
    public Vector2 Dir = Vector2.Left;
    public bool Aggro;
    public float AttackCd;
    public bool HitLanded;
    public bool ShotFired;
    public float Flash;
    public float AnimT;
    public int PatrolIdx;

    public Enemy(string eid, Vector2 pos, int index)
    {
        Eid = eid;
        Index = index;
        Data = EnemyDb.Get(eid);
        var size = EnemyDb.Size(Data);
        Size = size;
        Position = pos;
        Home = pos;
        Hp = EnemyDb.MaxHp(Data);
        AttackCd = (float)GD.RandRange(0.6f, 1.4f);
    }

    public override void _Ready()
    {
        base._Ready();
        SetCollisionShape(Size);
        QueueRedraw();
    }

    JsonObject Attacks() => Data["attacks"] as JsonObject;

    public JsonObject Attack(string kind)
    {
        return Attacks()?[kind] as JsonObject;
    }

    public string PrimaryAttack()
    {
        foreach (var kv in Attacks())
        {
            return kv.Key;
        }
        return null;
    }

    bool MoveToward(Vector2 target, float speedPerFrame)
    {
        var d = target - Position;
        if (d.Length() < 3)
        {
            return true;
        }
        Velocity = d.Normalized() * speedPerFrame * 60f;
        MoveAndSlide();
        return false;
    }

    public void TakeDamage(int dmg)
    {
        if (Dead)
        {
            return;
        }
        Hp -= dmg;
        Flash = 0.15f;
        if (Hp <= 0)
        {
            Dead = true;
            State = "dying";
            StateTimer = 0.5f;
            HitLanded = true;
            DisableCollision();
        }
        else
        {
            State = "recover";
            StateTimer = 0.3f;
            HitLanded = true;
        }
    }

    public Rect2? CurrentHitRect()
    {
        if (State != "attack")
        {
            return null;
        }
        var atk = Attack(AttackKind);
        if (atk == null || atk["projectile"] != null)
        {
            return null;
        }
        float rng = (float)EnemyDb.Num(atk, "range", 60);
        var hb = new Vector2((float)EnemyDb.Arr(atk, "hitbox", 0, 90), (float)EnemyDb.Arr(atk, "hitbox", 1, 70));
        var center = Position + new Vector2(Dir.X * rng * 0.7f, Dir.Y * 20);
        return new Rect2(center - hb / 2f, hb);
    }

    public void Update(float dt, Player player)
    {
        if (Dead)
        {
            if (State == "dying")
            {
                StateTimer -= dt;
            }
            return;
        }
        float frame = dt * 60f;
        Flash = Mathf.Max(0, Flash - dt);
        AnimT += dt;
        if (State == "dying")
        {
            StateTimer -= dt;
            return;
        }

        var ai = Data["ai"] as JsonObject;
        float distP = Position.DistanceTo(player.Position);
        if (!Aggro)
        {
            if (distP <= (float)EnemyDb.Num(ai, "aggro_radius", 320))
            {
                Aggro = true;
            }
        }
        else
        {
            float distHome = Position.DistanceTo(Home);
            if (distHome > (float)EnemyDb.Num(ai, "leash_radius", 600))
            {
                Aggro = false;
                if (EnemyDb.Flag(ai, "disengage_heal"))
                {
                    Hp = EnemyDb.MaxHp(Data);
                }
            }
        }

        if (Aggro)
        {
            UpdateCombat(dt, player, ai, distP, frame);
        }
        else
        {
            UpdatePatrol(ai, frame);
        }
    }

    void UpdatePatrol(JsonObject ai, float frame)
    {
        var patrol = ai["patrol"] as JsonObject;
        if (patrol == null)
        {
            return;
        }
        var pts = patrol["points"] as JsonArray;
        if (pts == null || pts.Count == 0)
        {
            return;
        }
        int idx = PatrolIdx % pts.Count;
        var arr = pts[idx].AsArray();
        var target = new Vector2((float)arr[0].GetValue<double>(), (float)arr[1].GetValue<double>());
        bool arrived = MoveToward(target, (float)EnemyDb.Num(ai, "patrol_speed", 0.8) * frame);
        if (arrived)
        {
            PatrolIdx = (PatrolIdx + 1) % pts.Count;
        }
        float dx = target.X - Position.X;
        if (Mathf.Abs(dx) > 0.5f)
        {
            Dir = new Vector2(dx > 0 ? 1 : -1, 0);
        }
    }

    void UpdateCombat(float dt, Player player, JsonObject ai, float distP, float frame)
    {
        var to = player.Position - Position;
        if (distP > 0)
        {
            Dir = to.Normalized();
        }
        float atkRange = (float)EnemyDb.Num(Attack(PrimaryAttack()), "range", 60);
        switch (State)
        {
            case "patrol":
            case "chase":
            case "idle":
                if (distP > atkRange * 0.8f)
                {
                    State = "chase";
                    MoveToward(player.Position, (float)EnemyDb.Num(Data, "speed", 1.6) * frame);
                }
                else
                {
                    AttackCd -= dt;
                    if (AttackCd <= 0)
                    {
                        State = "windup";
                        AttackKind = PrimaryAttack();
                        StateTimer = (float)EnemyDb.Num(Attack(AttackKind), "windup", 0.55);
                    }
                }
                break;
            case "windup":
                StateTimer -= dt;
                if (StateTimer <= 0)
                {
                    State = "attack";
                    StateTimer = (float)EnemyDb.Num(Attack(AttackKind), "duration", 0.18);
                    HitLanded = false;
                    ShotFired = false;
                }
                break;
            case "attack":
                StateTimer -= dt;
                if (StateTimer <= 0)
                {
                    State = "chase";
                    AttackCd = (float)GD.RandRange(
                        EnemyDb.Arr(ai, "attack_cd", 0, 1.0),
                        EnemyDb.Arr(ai, "attack_cd", 1, 2.0));
                }
                break;
            case "recover":
                StateTimer -= dt;
                if (StateTimer <= 0)
                {
                    State = "chase";
                }
                break;
        }
    }

    public override void _Draw()
    {
        float alpha = Dead ? Mathf.Max(0, StateTimer / 0.5f) : 1f;
        if (alpha <= 0)
        {
            return;
        }
        float fx = Dir.X;
        float fy = Dir.Y;
        var off = Position + new Vector2(-40, -43);

        Color body = Flash > 0 ? Palette.RGB(220, 210, 210) : EnemyDb.Color(Data);
        if (State == "windup")
        {
            float flick = 0.5f + 0.5f * Mathf.Sin(AnimT * 30);
            body = Palette.RGB(255, (int)(40 + 90 * flick), (int)(40 + 90 * flick));
        }
        body = new Color(body.R, body.G, body.B, alpha);
        DrawRect(new Rect2(off + new Vector2(24, 24), new Vector2(32, 30)), body);
        DrawCircle(off + new Vector2(40, 16), 9, new Color(150 / 255f, 140 / 255f, 128 / 255f, alpha));

        var atk = AttackKind != null ? Attack(AttackKind) : null;
        if (atk != null && atk["projectile"] != null)
        {
            DrawLine(off + new Vector2(40 + fx * 10, 32 + fy * 10), off + new Vector2(40 + fx * 34, 32 + fy * 34),
                new Color(140 / 255f, 120 / 255f, 90 / 255f, alpha), 3);
            DrawCircle(off + new Vector2(40 + fx * 36, 32 + fy * 36), 4,
                new Color(205 / 255f, 185 / 255f, 130 / 255f, alpha));
        }
        else
        {
            DrawLine(off + new Vector2(40 + fx * 8, 34 + fy * 8), off + new Vector2(40 + fx * 30, 34 + fy * 30),
                new Color(200 / 255f, 200 / 255f, 210 / 255f, alpha), 4);
        }
        if (Aggro)
        {
            DrawCircle(off + new Vector2(40, 6), 3, new Color(1, 90 / 255f, 60 / 255f, alpha));
        }
    }
}
