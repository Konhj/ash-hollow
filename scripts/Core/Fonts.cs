using Godot;

namespace AshHollow;

/// <summary>
/// 字体管理(对应旧 pygame fonts/:cinzel 标题 / EB Garamond 英文 / Noto Serif SC 中文)。
/// 中文字体为完整字体,勿切子集(changelog 任意文本会缺字)。
/// </summary>
public static class Fonts
{
    public const int Small = 20;
    public const int Normal = 26;
    public const int Big = 52;
    public const int Huge = 80;
    public const int TitleSize = 72;

    public static Font Title { get; private set; }
    public static Font Body { get; private set; }
    public static Font Zh { get; private set; }

    public static void Load()
    {
        Title = LoadFile("res://assets/fonts/cinzel.ttf");
        Body = LoadFile("res://assets/fonts/eb-garamond.ttf");
        Zh = LoadFile("res://assets/fonts/noto-serif-sc.ttf");
    }

    private static Font LoadFile(string path)
    {
        var f = new FontFile();
        var err = f.LoadDynamicFont(path);
        if (err != Error.Ok)
        {
            GD.PushError($"Fonts: cannot load {path} ({err})");
            return null;
        }
        return f;
    }

    /// <summary>含 CJK 字符用中文字体,否则用英文正文字体。</summary>
    public static Font Pick(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Body;
        }
        foreach (char ch in text)
        {
            if (ch >= 0x2E80)
            {
                return Zh;
            }
        }
        return Body;
    }
}
