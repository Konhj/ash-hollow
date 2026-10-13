using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;

namespace AshHollow;

/// <summary>
/// 全局机制常量(对应旧 settings.py),启动时从 data/settings.json 缓存。
/// 个体数值(怪物/物品)不在本类,全部留在 data/*.json 数据表。
/// </summary>
public static class S
{
    public static int Width, Height;
    public static string Version;
    public static List<Vector2I> Resolutions = new();

    public static float PlayerSpeed, SprintMult, SprintDrain;
    public static float PlayerHitstun, KnockbackDist, InputBufferTime;
    public static float HitstopHit, HitstopParry, HitstopRiposte, HitstopSlam;
    public static int PlayerMaxHp, PlayerMaxStamina;
    public static float StaminaRegen;
    public static int AttackCost, RollCost, RiposteCost;
    public static float GuardSpeedMult, GuardDrain;
    public static float RollSpeed, RollTime, RollCooldown;
    public static float AttackTime;
    public static int AttackDamage, AttackRange;
    public static float RiposteTime;
    public static int EstusMax, EstusHeal;
    public static float DrinkTime;

    public static int BossMaxHp;
    public static float BossSpeed, ChargeSpeed;
    public static int SlashDamage, ChargeDamage, SlamDamage, BlastDamage;
    public static int SlamRadius, BlastRadius;
    public static float Phase2HpFraction, Phase2WindupMult, SlamTrackSpeed;

    public static void Init()
    {
        var s = DataTables.Settings;
        Version = s["version"]?.GetValue<string>() ?? "0.0.0";
        Width = I(s, "width");
        Height = I(s, "height");
        Resolutions.Clear();
        foreach (var r in s["resolutions"].AsArray())
        {
            var a = r.AsArray();
            Resolutions.Add(new Vector2I((int)a[0].GetValue<double>(), (int)a[1].GetValue<double>()));
        }

        var p = s["player"];
        PlayerSpeed = F(p, "speed");
        SprintMult = F(p, "sprint_mult");
        SprintDrain = F(p, "sprint_drain");
        PlayerHitstun = F(p, "hitstun");
        KnockbackDist = F(p, "knockback_dist");
        InputBufferTime = F(p, "input_buffer_time");
        HitstopHit = F(p, "hitstop_hit");
        HitstopParry = F(p, "hitstop_parry");
        HitstopRiposte = F(p, "hitstop_riposte");
        HitstopSlam = F(p, "hitstop_slam");
        PlayerMaxHp = I(p, "max_hp");
        PlayerMaxStamina = I(p, "max_stamina");
        StaminaRegen = F(p, "stamina_regen");
        AttackCost = I(p, "attack_cost");
        RollCost = I(p, "roll_cost");
        RiposteCost = I(p, "riposte_cost");
        GuardSpeedMult = F(p, "guard_speed_mult");
        GuardDrain = F(p, "guard_drain");
        RollSpeed = F(p, "roll_speed");
        RollTime = F(p, "roll_time");
        RollCooldown = F(p, "roll_cooldown");
        AttackTime = F(p, "attack_time");
        AttackDamage = I(p, "attack_damage");
        AttackRange = I(p, "attack_range");
        RiposteTime = F(p, "riposte_time");
        EstusMax = I(p, "estus_max");
        EstusHeal = I(p, "estus_heal");
        DrinkTime = F(p, "drink_time");

        var b = s["boss"];
        BossMaxHp = I(b, "max_hp");
        BossSpeed = F(b, "speed");
        ChargeSpeed = F(b, "charge_speed");
        SlashDamage = I(b, "slash_damage");
        ChargeDamage = I(b, "charge_damage");
        SlamDamage = I(b, "slam_damage");
        SlamRadius = I(b, "slam_radius");
        BlastDamage = I(b, "blast_damage");
        BlastRadius = I(b, "blast_radius");
        Phase2HpFraction = F(b, "phase2_hp_fraction");
        Phase2WindupMult = F(b, "phase2_windup_mult");
        SlamTrackSpeed = F(b, "slam_track_speed");
    }

    static float F(JsonNode o, string key) => (float)o[key].GetValue<double>();
    static int I(JsonNode o, string key) => (int)o[key].GetValue<double>();
}

/// <summary>调色板(来自 data/settings.json colors)。</summary>
public static class Palette
{
    public static Color Bg, Floor, FloorDark, Wall, Red, DarkRed, Green, DarkGreen, Orange, Yellow, White, Gray;

    public static void Init()
    {
        var c = DataTables.Settings["colors"];
        Bg = C(c["bg"]);
        Floor = C(c["floor"]);
        FloorDark = C(c["floor_dark"]);
        Wall = C(c["wall"]);
        Red = C(c["red"]);
        DarkRed = C(c["dark_red"]);
        Green = C(c["green"]);
        DarkGreen = C(c["dark_green"]);
        Orange = C(c["orange"]);
        Yellow = C(c["yellow"]);
        White = C(c["white"]);
        Gray = C(c["gray"]);
    }

    static Color C(JsonNode rgb)
    {
        var a = rgb.AsArray();
        return new Color(
            (float)a[0].GetValue<double>() / 255f,
            (float)a[1].GetValue<double>() / 255f,
            (float)a[2].GetValue<double>() / 255f);
    }

    public static Color RGB(int r, int g, int b, int alpha = 255)
    {
        return new Color(r / 255f, g / 255f, b / 255f, alpha / 255f);
    }
}
