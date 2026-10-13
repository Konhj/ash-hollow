using Godot;

namespace AshHollow;

/// <summary>
/// 文本绘制助手:Godot 的 DrawString 以"基线"为锚点,而游戏 UI 习惯用中心/顶部锚点。
/// 这里统一封装,避免全项目文本上偏。
/// </summary>
public static class TextDraw
{
    /// <summary>左对齐,pos 为文本框左上角。</summary>
    public static void Left(CanvasItem c, Font font, Vector2 pos, string text, int size, Color color)
    {
        c.DrawString(font, pos + new Vector2(0, font.GetAscent(size)),
            text, HorizontalAlignment.Left, -1, size, color);
    }

    /// <summary>水平居中,pos 为文本中心。
    /// 注意:DrawString 的 alignment 仅在 width&gt;0 时生效,这里手动计算 x 偏移。</summary>
    public static void Center(CanvasItem c, Font font, Vector2 pos, string text, int size, Color color)
    {
        float w = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        float half = (font.GetAscent(size) - font.GetDescent(size)) / 2f;
        c.DrawString(font, pos + new Vector2(-w / 2f, half),
            text, HorizontalAlignment.Left, -1, size, color);
    }

    /// <summary>水平居中 + 最大宽度(超出自动缩字号,对应旧 fit_text)。</summary>
    public static void FitCenter(CanvasItem c, Font font, Vector2 pos, string text, float maxW, int size, Color color)
    {
        float w = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        int s = w > maxW ? Mathf.Max(8, (int)(size * maxW / w)) : size;
        Center(c, font, pos, text, s, color);
    }

    /// <summary>水平居中(固定字号),pos 为文本中心;返回实际渲染矩形(菜单命中检测用)。</summary>
    public static Rect2 CenterRect(CanvasItem c, Font font, Vector2 pos, string text, int size, Color color)
    {
        Center(c, font, pos, text, size, color);
        var sz = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
        return new Rect2(pos.X - sz.X / 2f, pos.Y - sz.Y / 2f, sz.X, sz.Y);
    }

    /// <summary>右对齐,pos 为文本右下角。</summary>
    public static void RightBottom(CanvasItem c, Font font, Vector2 pos, string text, int size, Color color)
    {
        float w = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        c.DrawString(font, pos + new Vector2(-w, -font.GetDescent(size)),
            text, HorizontalAlignment.Left, -1, size, color);
    }

    /// <summary>左对齐,pos 为文本左下角。</summary>
    public static void LeftBottom(CanvasItem c, Font font, Vector2 pos, string text, int size, Color color)
    {
        c.DrawString(font, pos - new Vector2(0, font.GetDescent(size)),
            text, HorizontalAlignment.Left, -1, size, color);
    }
}
