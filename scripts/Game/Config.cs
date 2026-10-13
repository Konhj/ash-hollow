using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace AshHollow;

/// <summary>
/// 用户配置(对应旧 config.py):分辨率/显示模式/语言/按键提示/输入缓冲/按键映射。
/// 存放位置:ASH_HOLLOW_DIR 环境变量 → 编辑器运行用 user:// → 发布版 exe 旁。
/// </summary>
public static class Config
{
    public const string FileName = "config.json";

    public static int Resolution = 2;
    public static int DisplayMode;
    public static string Language = "zh";
    public static bool ShowKeys = true;
    public static float InputBuffer = 0.15f;

    public static Dictionary<string, Key> Bindings = new()
    {
        { "move_up", Key.W }, { "move_down", Key.S }, { "move_left", Key.A }, { "move_right", Key.D },
        { "attack", Key.J }, { "roll", Key.K }, { "offhand", Key.L }, { "estus", Key.F },
        { "interact", Key.E }, { "sprint", Key.Shift }, { "inventory", Key.Tab },
    };

    public static string BaseDir()
    {
        var env = OS.GetEnvironment("ASH_HOLLOW_DIR");
        if (!string.IsNullOrEmpty(env))
        {
            return env;
        }
        if (OS.HasFeature("editor"))
        {
            return ProjectSettings.GlobalizePath("user://");
        }
        return OS.GetExecutablePath().GetBaseDir();
    }

    public static string ConfigPath() => Path.Combine(BaseDir(), FileName);

    public static string SavePath() => Path.Combine(BaseDir(), "save.json");

    public static void Load()
    {
        try
        {
            if (!File.Exists(ConfigPath()))
            {
                return;
            }
            var root = JsonNode.Parse(File.ReadAllText(ConfigPath(), System.Text.Encoding.UTF8)) as JsonObject;
            if (root == null)
            {
                return;
            }
            if (root["resolution"] is JsonValue rv && rv.TryGetValue(out int res))
            {
                Resolution = res;
            }
            if (root["display_mode"] is JsonValue dv && dv.TryGetValue(out int dm))
            {
                DisplayMode = dm;
            }
            if (root["language"] is JsonValue lv && lv.TryGetValue(out string lang))
            {
                Language = lang == "zh" ? "zh" : "en";
            }
            if (root["show_keys"] is JsonValue sk)
            {
                ShowKeys = sk.GetValue<bool>();
            }
            if (root["input_buffer"] is JsonValue ib)
            {
                InputBuffer = Math.Clamp((float)ib.GetValue<double>(), 0f, 0.6f);
            }
            if (root["bindings"] is JsonObject bo)
            {
                foreach (var kv in bo)
                {
                    if (Bindings.ContainsKey(kv.Key) && kv.Value is JsonValue keyVal
                        && keyVal.TryGetValue(out string name)
                        && Enum.TryParse(name, true, out Key key))
                    {
                        Bindings[kv.Key] = key;
                    }
                }
            }
        }
        catch (Exception e)
        {
            GD.PushWarning($"Config.Load: {e.Message}");
        }
    }

    public static void Save()
    {
        try
        {
            var bo = new JsonObject();
            foreach (var kv in Bindings)
            {
                bo[kv.Key] = kv.Value.ToString();
            }
            var root = new JsonObject
            {
                ["resolution"] = Resolution,
                ["display_mode"] = DisplayMode,
                ["language"] = Language,
                ["show_keys"] = ShowKeys,
                ["input_buffer"] = MathF.Round(InputBuffer, 3),
                ["bindings"] = bo,
            };
            File.WriteAllText(ConfigPath(), root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                System.Text.Encoding.UTF8);
        }
        catch (Exception e)
        {
            GD.PushWarning($"Config.Save: {e.Message}");
        }
    }

    /// <summary>应用显示设置(对应旧 apply_display:窗口化/无边框/全屏 + 分辨率)。</summary>
    public static void ApplyDisplay()
    {
        DisplayServer.WindowSetTitle($"ASH HOLLOW v{S.Version}");
        switch (DisplayMode)
        {
            case 0:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
                var size = S.Resolutions[Math.Clamp(Resolution, 0, S.Resolutions.Count - 1)];
                DisplayServer.WindowSetSize(size);
                break;
            case 1:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
                DisplayServer.WindowSetSize(DisplayServer.ScreenGetSize());
                break;
            default:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
                break;
        }
    }

    public static string KeyName(Key key) => key.ToString().ToUpper();
}
