using System;
using System.Text.Json.Nodes;
using Godot;

namespace AshHollow;

/// <summary>
/// 数据表加载器:启动时从 data/*.json 读取全部数据驱动表。
/// 对应旧 pygame 的 rooms.py / items.py / enemies.py / settings.py / i18n.py / changelog.py。
/// </summary>
public static class DataTables
{
    public static JsonNode Settings { get; private set; }
    public static JsonNode Rooms { get; private set; }
    public static JsonNode Items { get; private set; }
    public static JsonNode Enemies { get; private set; }
    public static JsonNode I18n { get; private set; }
    public static JsonNode Changelog { get; private set; }

    public static void LoadAll()
    {
        Settings = Load("res://data/settings.json");
        Rooms = Load("res://data/rooms.json");
        Items = Load("res://data/items.json");
        Enemies = Load("res://data/enemies.json");
        I18n = Load("res://data/i18n.json");
        Changelog = Load("res://data/changelog.json");
    }

    private static JsonNode Load(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushError($"DataTables: cannot open {path}");
            return new JsonObject();
        }
        string text = file.GetAsText();
        try
        {
            return JsonNode.Parse(text);
        }
        catch (Exception e)
        {
            GD.PushError($"DataTables: bad JSON in {path}: {e.Message}");
            return new JsonObject();
        }
    }
}

/// <summary>
/// 双语支持(对应旧 pygame 的 i18n.py)。
/// </summary>
public static class I18n
{
    private static string _lang = "en";

    public static string Lang => _lang;

    public static void SetLang(string lang)
    {
        _lang = lang == "zh" ? "zh" : "en";
    }

    public static string T(string key, params object[] args)
    {
        var entry = DataTables.I18n[key] as JsonObject;
        if (entry == null)
        {
            return key;
        }
        string text = entry[_lang]?.GetValue<string>() ?? entry["en"]?.GetValue<string>() ?? key;
        return args.Length > 0 ? string.Format(text, args) : text;
    }
}
