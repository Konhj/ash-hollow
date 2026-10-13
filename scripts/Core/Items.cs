using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;

namespace AshHollow;

/// <summary>物品数据表访问器(对应旧 items.py)。</summary>
public static class ItemsDb
{
    public static JsonObject Items;
    public static List<string> DefaultInventory = new();
    public static Dictionary<string, string> DefaultEquipment = new();

    public static void Load()
    {
        Items = DataTables.Items["items"] as JsonObject;
        DefaultInventory.Clear();
        foreach (var id in DataTables.Items["default_inventory"].AsArray())
        {
            DefaultInventory.Add(id.GetValue<string>());
        }
        DefaultEquipment.Clear();
        var eq = DataTables.Items["default_equipment"] as JsonObject;
        foreach (var kv in eq)
        {
            DefaultEquipment[kv.Key] = kv.Value?.GetValue<string>();
        }
    }

    public static JsonObject Get(string id)
    {
        if (id == null)
        {
            return null;
        }
        return Items.TryGetPropertyValue(id, out var v) ? v as JsonObject : null;
    }

    public static string Name(string id, string lang = null)
    {
        var item = Get(id);
        if (item == null)
        {
            return "?";
        }
        return L(item["name"], lang);
    }

    public static string Desc(string id, string lang = null)
    {
        var item = Get(id);
        if (item == null)
        {
            return "";
        }
        return L(item["desc"], lang);
    }

    public static string L(JsonNode entry, string lang = null)
    {
        var o = entry as JsonObject;
        if (o == null)
        {
            return "";
        }
        string l = lang ?? I18n.Lang;
        return o[l]?.GetValue<string>() ?? o["en"]?.GetValue<string>() ?? "";
    }

    /// <summary>读取 stats 子表数值(带默认值,缺失字段安全)。</summary>
    public static double Stat(JsonObject item, string key, double fallback = 0)
    {
        var st = item["stats"] as JsonObject;
        if (st != null && st[key] is JsonValue v)
        {
            return v.GetValue<double>();
        }
        return fallback;
    }
}
