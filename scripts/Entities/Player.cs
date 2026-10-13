using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;

namespace AshHollow;

/// <summary>
/// 玩家(对应旧 player.py):状态机 + 精力 + 原素瓶 + 装备数值(全部来自 items 数据表)。
/// </summary>
public partial class Player : Entity
{
    public const float AttackActiveFrom = 0.22f;
    public const float AttackActiveTo = 0.61f;

    public Vector2 WorldBounds;
    public Vector2 Facing = Vector2.Right;
    public Vector2 LastMove;
    public List<string> Inventory = new();
    public Dictionary<string, string> Equipment = new();

    public int Souls;
    public int Hp, Stamina, Estus;
    public string State = "idle";
    public float StateTimer;
    public float Invuln;
    public float RollCooldown;
    public Vector2 RollDir = Vector2.Right;
    public bool AttackLanded;
    public bool AttackIsOffhand;
    public float AttackDuration;
    public bool RiposteDone;
    public bool JustDrank;
    public float Flash;
    public float GuardTimer;

    int _prevMaxHp;

    public override void _Ready()
    {
        base._Ready();
        Size = new Vector2(30, 46);
        SetCollisionShape(Size);
        ResetStats();
    }

    public JsonObject Shield()
    {
        var oid = Equipment.TryGetValue("offhand", out var o) ? o : null;
        var item = ItemsDb.Get(oid);
        if (item != null && item["type"]?.GetValue<string>() == "weapon"
            && item["subtype"]?.GetValue<string>() == "shield")
        {
            return item;
        }
        return null;
    }

    public Dictionary<string, int> TalismanEffects()
    {
        var outd = new Dictionary<string, int>();
        for (int i = 1; i <= 3; i++)
        {
            var tid = Equipment.TryGetValue($"talisman{i}", out var t) ? t : null;
            var item = ItemsDb.Get(tid);
            if (item == null)
            {
                continue;
            }
            var eff = item["effect"] as JsonObject;
            if (eff == null)
            {
                continue;
            }
            foreach (var kv in eff)
            {
                int v = kv.Value.GetValue<int>();
                outd[kv.Key] = outd.TryGetValue(kv.Key, out var cur) ? cur + v : v;
            }
        }
        return outd;
    }

    public int MaxHp() => S.PlayerMaxHp + TalismanEffects().GetValueOrDefault("hp_up");
    public int MaxStamina() => S.PlayerMaxStamina + TalismanEffects().GetValueOrDefault("stamina_up");

    public int Defense()
    {
        var aid = Equipment.TryGetValue("armor", out var a) ? a : null;
        var item = ItemsDb.Get(aid);
        return item == null ? 0 : (int)ItemsDb.Stat(item, "defense");
    }

    public int AttackDamage()
    {
        var wid = Equipment.TryGetValue("mainhand", out var w) ? w : null;
        var item = ItemsDb.Get(wid);
        if (item != null && item["type"]?.GetValue<string>() == "weapon")
        {
            return (int)ItemsDb.Stat(item, "damage", S.AttackDamage);
        }
        return S.AttackDamage;
    }

    public float AttackSpeed()
    {
        var wid = Equipment.TryGetValue("mainhand", out var w) ? w : null;
        var item = ItemsDb.Get(wid);
        if (item != null && item["type"]?.GetValue<string>() == "weapon")
        {
            return (float)ItemsDb.Stat(item, "speed", 1.0);
        }
        return 1f;
    }

    public int CurrentAttackDamage()
    {
        if (State == "attack" && AttackIsOffhand)
        {
            var oid = Equipment.TryGetValue("offhand", out var o) ? o : null;
            var item = ItemsDb.Get(oid);
            if (item != null && item["type"]?.GetValue<string>() == "weapon")
            {
                return (int)ItemsDb.Stat(item, "damage", S.AttackDamage);
            }
        }
        return AttackDamage();
    }

    public string CurrentAttackStyle()
    {
        JsonObject item = null;
        if (State == "attack" && AttackIsOffhand)
        {
            var oid = Equipment.TryGetValue("offhand", out var o) ? o : null;
            item = ItemsDb.Get(oid);
        }
        else
        {
            var wid = Equipment.TryGetValue("mainhand", out var w) ? w : null;
            item = ItemsDb.Get(wid);
        }
        if (item != null && item["type"]?.GetValue<string>() == "weapon")
        {
            return item["attack_style"]?.GetValue<string>() ?? "slash";
        }
        return "slash";
    }

    public float CurrentAttackRange()
    {
        JsonObject item = null;
        if (State == "attack" && AttackIsOffhand)
        {
            var oid = Equipment.TryGetValue("offhand", out var o) ? o : null;
            item = ItemsDb.Get(oid);
        }
        else
        {
            var wid = Equipment.TryGetValue("mainhand", out var w) ? w : null;
            item = ItemsDb.Get(wid);
        }
        if (item != null && item["type"]?.GetValue<string>() == "weapon")
        {
            return (float)ItemsDb.Stat(item, "range", S.AttackRange);
        }
        return S.AttackRange;
    }

    public void RefreshMaxHp()
    {
        int newMax = MaxHp();
        Hp = Math.Min(newMax, Math.Max(1, Hp + (newMax - _prevMaxHp)));
        _prevMaxHp = newMax;
    }

    public void ResetStats()
    {
        Hp = MaxHp();
        _prevMaxHp = MaxHp();
        Stamina = MaxStamina();
        Estus = S.EstusMax;
        State = "idle";
        StateTimer = 0;
        Invuln = 0;
        RollCooldown = 0;
        RollDir = Vector2.Right;
        AttackLanded = false;
        RiposteDone = false;
        JustDrank = false;
        Flash = 0;
        Dead = false;
    }

    public void Respawn()
    {
        Facing = Vector2.Right;
        ResetStats();
    }

    /// <summary>新游戏:重置背包/装备/灵魂/状态(对应旧 pygame 新建 Player)。</summary>
    public void ResetProfile()
    {
        Inventory.Clear();
        Inventory.AddRange(ItemsDb.DefaultInventory);
        Equipment.Clear();
        foreach (var kv in ItemsDb.DefaultEquipment)
        {
            Equipment[kv.Key] = kv.Value;
        }
        Souls = 0;
        Facing = Vector2.Right;
        ResetStats();
    }

    public void Clamp()
    {
        var w = WorldBounds.X;
        var h = WorldBounds.Y;
        Position = new Vector2(
            Mathf.Max(31f, Mathf.Min(w - 31f, Position.X)),
            Mathf.Max(39f, Mathf.Min(h - 39f, Position.Y)));
    }

    public void Update(float dt, Func<string, bool> held)
    {
        if (Dead)
        {
            return;
        }
        Invuln = Mathf.Max(0, Invuln - dt);
        RollCooldown = Mathf.Max(0, RollCooldown - dt);
        Flash = Mathf.Max(0, Flash - dt);

        var move = Vector2.Zero;
        if (held("move_left"))
        {
            move.X -= 1;
        }
        if (held("move_right"))
        {
            move.X += 1;
        }
        if (held("move_up"))
        {
            move.Y -= 1;
        }
        if (held("move_down"))
        {
            move.Y += 1;
        }
        move = move.LengthSquared() > 0 ? move.Normalized() : move;
        LastMove = move;

        switch (State)
        {
            case "guard":
            {
                bool guardHeld = held("offhand") && Shield() != null;
                if (!guardHeld)
                {
                    State = "idle";
                }
                else
                {
                    GuardTimer += dt;
                    if (move.LengthSquared() > 0)
                    {
                        Facing = move;
                        Velocity = move * S.PlayerSpeed * S.GuardSpeedMult * 60f;
                        MoveAndSlide();
                        Clamp();
                    }
                    Stamina = Math.Max(0, Stamina - (int)(S.GuardDrain * dt));
                    if (Stamina <= 0)
                    {
                        State = "idle";
                    }
                }
                break;
            }
            case "idle":
            {
                bool sprintHeld = held("sprint") && move.LengthSquared() > 0;
                bool sprinting = sprintHeld && Stamina > 0;
                if (!sprintHeld)
                {
                    Stamina = Math.Min(MaxStamina(), Stamina + (int)(S.StaminaRegen * dt));
                }
                if (move.LengthSquared() > 0)
                {
                    Facing = move;
                    float spd = sprinting ? S.PlayerSpeed * S.SprintMult : S.PlayerSpeed;
                    Velocity = move * spd * 60f;
                    MoveAndSlide();
                    Clamp();
                    if (sprinting)
                    {
                        Stamina = Math.Max(0, Stamina - (int)(S.SprintDrain * dt));
                    }
                }
                break;
            }
            case "roll":
            {
                Velocity = RollDir * S.RollSpeed * 60f;
                MoveAndSlide();
                Clamp();
                StateTimer -= dt;
                if (StateTimer <= 0)
                {
                    State = "idle";
                    RollCooldown = S.RollCooldown;
                }
                break;
            }
            case "attack":
            case "riposte":
            case "drink":
            case "hitstun":
            {
                StateTimer -= dt;
                if (StateTimer <= 0)
                {
                    if (State == "drink")
                    {
                        Hp = Math.Min(MaxHp(), Hp + S.EstusHeal);
                        Estus -= 1;
                        JustDrank = true;
                    }
                    State = "idle";
                }
                break;
            }
        }
    }

    public int AttackStaminaCost()
    {
        JsonObject item = null;
        if (State == "attack" && AttackIsOffhand)
        {
            var oid = Equipment.TryGetValue("offhand", out var o) ? o : null;
            item = ItemsDb.Get(oid);
        }
        else
        {
            var wid = Equipment.TryGetValue("mainhand", out var w) ? w : null;
            item = ItemsDb.Get(wid);
        }
        if (item != null && item["type"]?.GetValue<string>() == "weapon")
        {
            return (int)ItemsDb.Stat(item, "stamina", S.AttackCost);
        }
        return S.AttackCost;
    }

    public string StartAttack()
    {
        int cost = AttackStaminaCost();
        if (Stamina < cost)
        {
            return "out_of_stamina";
        }
        Stamina -= cost;
        State = "attack";
        AttackIsOffhand = false;
        AttackDuration = S.AttackTime / AttackSpeed();
        StateTimer = AttackDuration;
        AttackLanded = false;
        return null;
    }

    public string StartRoll()
    {
        if (RollCooldown > 0)
        {
            return null;
        }
        if (Stamina < S.RollCost)
        {
            return "out_of_stamina";
        }
        Stamina -= S.RollCost;
        State = "roll";
        StateTimer = S.RollTime;
        Invuln = S.RollTime;
        RollDir = LastMove.LengthSquared() > 0 ? LastMove : Facing;
        return null;
    }

    public void StartGuard()
    {
        if (Stamina <= 0)
        {
            return;
        }
        State = "guard";
        GuardTimer = 0;
    }

    public string StartOffhandAttack()
    {
        var oid = Equipment.TryGetValue("offhand", out var o) ? o : null;
        var item = ItemsDb.Get(oid);
        if (item == null || item["type"]?.GetValue<string>() != "weapon"
            || item["subtype"]?.GetValue<string>() == "shield")
        {
            return null;
        }
        int cost = (int)ItemsDb.Stat(item, "stamina", S.AttackCost);
        if (Stamina < cost)
        {
            return "out_of_stamina";
        }
        Stamina -= cost;
        State = "attack";
        AttackIsOffhand = true;
        AttackDuration = S.AttackTime / (float)ItemsDb.Stat(item, "speed", 1.0);
        StateTimer = AttackDuration;
        AttackLanded = false;
        return null;
    }

    public string StartDrink()
    {
        if (Estus <= 0)
        {
            return "no_estus";
        }
        State = "drink";
        StateTimer = S.DrinkTime;
        return null;
    }

    public string StartRiposte()
    {
        if (Stamina < S.RiposteCost)
        {
            return "out_of_stamina";
        }
        Stamina -= S.RiposteCost;
        State = "riposte";
        StateTimer = S.RiposteTime;
        RiposteDone = false;
        return null;
    }

    public bool ParryWindowActive()
    {
        if (State != "guard")
        {
            return false;
        }
        var sh = Shield();
        if (sh == null)
        {
            return false;
        }
        return GuardTimer <= (float)ItemsDb.Stat(sh, "parry_time", 0.2);
    }

    public bool AttackActive()
    {
        if (State != "attack" || AttackDuration <= 0)
        {
            return false;
        }
        float frac = (AttackDuration - StateTimer) / AttackDuration;
        return frac >= AttackActiveFrom && frac <= AttackActiveTo;
    }

    public Rect2 GetAttackHitbox()
    {
        float rng = CurrentAttackRange();
        float back = 12f;
        var size = new Vector2(rng - back, 52);
        var center = Position + new Vector2(Facing.X * (rng + back) / 2f, Facing.Y * 30f);
        return new Rect2(center - size / 2f, size);
    }

    public bool TakeDamage(int dmg, Vector2? fromDir)
    {
        if (Invuln > 0 || State == "roll")
        {
            return false;
        }
        int final = Math.Max(1, dmg - Defense());
        if (State == "guard")
        {
            var sh = Shield();
            if (sh != null)
            {
                final = Math.Max(1, (int)(final * (1 - ItemsDb.Stat(sh, "defense", 0))));
            }
        }
        Hp -= final;
        Flash = 0.18f;
        State = "hitstun";
        StateTimer = S.PlayerHitstun;
        if (fromDir.HasValue)
        {
            Position += fromDir.Value * S.KnockbackDist;
            Clamp();
        }
        if (Hp <= 0)
        {
            Hp = 0;
            Dead = true;
            DisableCollision();
        }
        return true;
    }

    public override void _Draw()
    {
        if (Dead)
        {
            return;
        }
        if (Invuln > 0 && State != "roll")
        {
            Modulate = new Color(1, 1, 1, 150f / 255f);
        }
        else
        {
            Modulate = Colors.White;
        }

        float fx = Facing.X;
        float fy = Facing.Y;

        if (State == "roll")
        {
            DrawCircle(Vector2.Zero, 15, Palette.RGB(95, 95, 110, 220));
            DrawCircle(Vector2.Zero, 15, Palette.RGB(70, 70, 84, 220), false, 3);
            return;
        }

        Color body = Flash > 0 ? Palette.RGB(200, 200, 210) : Palette.RGB(68, 68, 80);
        DrawRect(new Rect2(-15, -15, 30, 26), body);
        DrawRect(new Rect2(-13, -9, 26, 18), Palette.RGB(48, 48, 56));
        DrawCircle(new Vector2(0, -21), 8, Palette.RGB(150, 140, 128));

        var from = new Vector2(fx * 8, -7 + fy * 8);
        var tip = State == "riposte"
            ? new Vector2(fx * 38, -7 + fy * 38)
            : new Vector2(fx * 24, -7 + fy * 24);
        if (State == "riposte")
        {
            DrawLine(from, tip, Palette.RGB(255, 240, 160), 5);
        }
        else
        {
            DrawLine(from, tip, Palette.RGB(200, 200, 214), 4);
        }

        if (State == "guard")
        {
            var c = new Vector2(fx * 16, -7 + fy * 16);
            DrawCircle(c, 13, Palette.RGB(200, 200, 215, 220), false, 4);
            DrawCircle(c, 13, Palette.RGB(140, 140, 155, 180));
        }
        if (State == "drink")
        {
            DrawCircle(new Vector2(fx * 10, -23), 5, Palette.RGB(240, 150, 40, 230));
        }
        if (State == "attack" && CurrentAttackStyle() == "slash")
        {
            DrawSlashEffect();
        }
    }

    void DrawSlashEffect()
    {
        float ang = Mathf.Atan2(Facing.Y, Facing.X);
        float frac = (AttackDuration - StateTimer) / Mathf.Max(0.001f, AttackDuration);
        float r = CurrentAttackRange();
        DrawSetTransform(Vector2.Zero, ang);
        float[] offs = { 0f, -0.12f, -0.24f };
        for (int i = 0; i < offs.Length; i++)
        {
            float p = Mathf.Clamp(frac + offs[i], 0, 1);
            float a1 = Mathf.DegToRad(-60f + 120f * p - 9.25f);
            float a2 = Mathf.DegToRad(-60f + 120f * p + 9.25f);
            DrawArc(Vector2.Zero, r, a1, a2, 32, Palette.RGB(255, 255, 255, 230 - i * 90), 5);
            DrawArc(Vector2.Zero, r - 12, a1, a2, 32, Palette.RGB(255, 255, 255, 115 - i * 45), 3);
        }
        DrawSetTransform(Vector2.Zero);
    }
}
