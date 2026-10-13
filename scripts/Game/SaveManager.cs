using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace AshHollow;

/// <summary>
/// 存档系统(对应旧 save.json,格式 version 3)。
/// 当前单槽;框架按 DESIGN 预留多槽(后续扩展 save_{n}.json + 最近槽索引)。
/// </summary>
public static class SaveManager
{
    public const int Version = 3;

    public static void Save(MainGame g)
    {
        var p = g.Player;
        var b = g.Boss;
        if (b.Dead)
        {
            Delete();
            return;
        }
        try
        {
            var eq = new JsonObject();
            foreach (var kv in p.Equipment)
            {
                eq[kv.Key] = kv.Value;
            }
            var killed = new JsonArray();
            foreach (var (room, index) in g.KilledEnemies)
            {
                killed.Add(new JsonArray(room, index));
            }
            var inv = new JsonArray();
            foreach (var id in p.Inventory)
            {
                inv.Add(id);
            }
            var root = new JsonObject
            {
                ["version"] = Version,
                ["room"] = g.RoomId,
                ["player"] = new JsonObject
                {
                    ["x"] = MathF.Round(p.Position.X, 1),
                    ["y"] = MathF.Round(p.Position.Y, 1),
                    ["hp"] = p.Hp,
                    ["estus"] = p.Estus,
                },
                ["boss"] = new JsonObject
                {
                    ["x"] = MathF.Round(b.Position.X, 1),
                    ["y"] = MathF.Round(b.Position.Y, 1),
                    ["hp"] = b.Hp,
                    ["aggro"] = b.Aggro,
                },
                ["inventory"] = inv,
                ["equipment"] = eq,
                ["souls"] = p.Souls,
                ["killed"] = killed,
            };
            File.WriteAllText(Config.SavePath(),
                root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                System.Text.Encoding.UTF8);
        }
        catch (Exception e)
        {
            GD.PushWarning($"SaveManager.Save: {e.Message}");
        }
    }

    public static JsonObject Load()
    {
        try
        {
            if (!File.Exists(Config.SavePath()))
            {
                return null;
            }
            var data = JsonNode.Parse(File.ReadAllText(Config.SavePath(), System.Text.Encoding.UTF8)) as JsonObject;
            if (data != null && data["player"] != null && data["boss"] != null)
            {
                return data;
            }
        }
        catch (Exception e)
        {
            GD.PushWarning($"SaveManager.Load: {e.Message}");
        }
        return null;
    }

    public static bool Exists() => File.Exists(Config.SavePath());

    public static void Delete()
    {
        try
        {
            if (File.Exists(Config.SavePath()))
            {
                File.Delete(Config.SavePath());
            }
        }
        catch (Exception e)
        {
            GD.PushWarning($"SaveManager.Delete: {e.Message}");
        }
    }
}
