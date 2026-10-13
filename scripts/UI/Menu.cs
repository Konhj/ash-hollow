using System;
using System.Collections.Generic;
using Godot;

namespace AshHollow;

/// <summary>菜单项。</summary>
public class MenuItem
{
    public Func<string> Label = () => "";
    public Action Action;
    public Action<int> Adjust;
    public Func<bool> Visible = () => true;
    public Func<bool> Red = () => false;
    public Func<bool> Enabled = () => true;
}

/// <summary>
/// 通用菜单(对应旧 main.py 的 Menu 类):选项栈、键盘/鼠标导航、左右调整、按键捕获。
/// </summary>
public class Menu
{
    public string Title = "";
    public List<MenuItem> Items = new();
    public int Selected;
    public int StartY = 300;
    public int Step = 56;
    public int Columns = 1;
    public Func<(string text, Color color)> Hint;
    public Func<string> Warning;
    public bool TitleFont;
    public string Capture;
    public List<Rect2> HitRects = new();

    readonly Stack<(string title, List<MenuItem> items, int sel, int startY, int step, int columns,
        Func<(string, Color)> hint, Func<string> warning, bool titleFont)> _stack = new();

    public void Set(string title, List<MenuItem> items, int startY = 300, int step = 56,
        int columns = 1, Func<(string, Color)> hint = null, bool titleFont = false,
        Func<string> warning = null)
    {
        Title = title;
        Items = items;
        Selected = 0;
        StartY = startY;
        Step = step;
        Columns = columns;
        Hint = hint;
        TitleFont = titleFont;
        Warning = warning;
        HitRects.Clear();
    }

    public void Push(string title, List<MenuItem> items, int startY = 300, int step = 56,
        int columns = 1, Func<(string, Color)> hint = null, bool titleFont = false,
        Func<string> warning = null)
    {
        _stack.Push((Title, Items, Selected, StartY, Step, Columns, Hint, Warning, TitleFont));
        Set(title, items, startY, step, columns, hint, titleFont, warning);
    }

    public bool Pop()
    {
        if (_stack.Count == 0)
        {
            return false;
        }
        var (title, items, sel, startY, step, columns, hint, warning, titleFont) = _stack.Pop();
        Title = title;
        Items = items;
        Selected = sel;
        StartY = startY;
        Step = step;
        Columns = columns;
        Hint = hint;
        Warning = warning;
        TitleFont = titleFont;
        HitRects.Clear();
        return true;
    }

    public List<int> VisibleIndices()
    {
        var outList = new List<int>();
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].Visible())
            {
                outList.Add(i);
            }
        }
        return outList;
    }

    int Rows(int n) => Columns == 2 ? (n + 1) / 2 : n;

    public void Update(Func<Key, bool> just, Vector2? mouse, bool clicked)
    {
        if (Capture != null)
        {
            return;
        }
        var visible = VisibleIndices();
        int n = visible.Count;
        if (n == 0)
        {
            return;
        }
        int rows = Rows(n);

        if (mouse.HasValue && HitRects.Count == n)
        {
            for (int i = 0; i < HitRects.Count; i++)
            {
                if (HitRects[i].HasPoint(mouse.Value))
                {
                    Selected = i;
                    break;
                }
            }
        }

        if (Columns == 2)
        {
            if (just(Key.Up) || just(Key.W))
            {
                Selected = Mathf.Max(0, Selected - 1);
            }
            if (just(Key.Down) || just(Key.S))
            {
                Selected = Mathf.Min(n - 1, Selected + 1);
            }
            if (just(Key.Left) || just(Key.A))
            {
                if (Selected >= rows)
                {
                    Selected -= rows;
                }
            }
            if (just(Key.Right) || just(Key.D))
            {
                if (Selected + rows < n)
                {
                    Selected += rows;
                }
            }
        }
        else
        {
            if (just(Key.Up) || just(Key.W))
            {
                Selected = (Selected - 1 + n) % n;
            }
            if (just(Key.Down) || just(Key.S))
            {
                Selected = (Selected + 1) % n;
            }
        }

        var item = Items[visible[Selected]];
        if (Columns == 1)
        {
            if (just(Key.Left) && item.Adjust != null)
            {
                item.Adjust(-1);
            }
            if (just(Key.Right) && item.Adjust != null)
            {
                item.Adjust(1);
            }
        }
        if (just(Key.Enter) && item.Action != null)
        {
            item.Action();
        }

        if (clicked && mouse.HasValue && HitRects.Count == n)
        {
            for (int i = 0; i < HitRects.Count; i++)
            {
                if (HitRects[i].HasPoint(mouse.Value))
                {
                    var mi = Items[visible[i]];
                    if (mi.Adjust != null)
                    {
                        mi.Adjust(mouse.Value.X >= HitRects[i].GetCenter().X ? 1 : -1);
                    }
                    else if (mi.Action != null && mi.Enabled())
                    {
                        mi.Action();
                    }
                    break;
                }
            }
        }
    }

    public void Draw(CanvasItem c, Font titleFont = null)
    {
        if (!string.IsNullOrEmpty(Title))
        {
            var font = TitleFont && titleFont != null ? titleFont : Fonts.Pick(Title);
            TextDraw.Center(c, font, new Vector2(S.Width / 2f, 150), Title,
                TitleFont ? Fonts.TitleSize : Fonts.Big, Palette.RGB(220, 220, 228));
        }
        if (Warning != null)
        {
            var wtext = Warning();
            if (!string.IsNullOrEmpty(wtext))
            {
                TextDraw.Center(c, Fonts.Pick(wtext), new Vector2(S.Width / 2f, 240), wtext,
                    Fonts.Normal, Palette.RGB(235, 170, 80));
            }
        }
        var visible = VisibleIndices();
        int n = visible.Count;
        int rows = Rows(n);
        HitRects.Clear();
        for (int i = 0; i < n; i++)
        {
            var item = Items[visible[i]];
            bool sel = i == Selected;
            int col = Columns == 2 ? i / rows : 0;
            int row = i % rows;
            float x = Columns == 2 ? S.Width / 2f - 300 + col * 600 : S.Width / 2f;
            float y = StartY + row * Step;

            Color color;
            if (sel)
            {
                color = Palette.Yellow;
            }
            else if (item.Red())
            {
                color = Palette.RGB(255, 90, 90);
            }
            else
            {
                color = Palette.Gray;
            }
            if (!item.Enabled())
            {
                color = Palette.RGB(70, 70, 78);
            }
            string label = item.Label();
            if (sel)
            {
                label = "> " + label + " <";
            }
            var font = Fonts.Pick(label);
            int itemSize = Columns == 2 ? Fonts.Normal : Fonts.Big;
            var size = font.GetStringSize(label, HorizontalAlignment.Left, -1, itemSize);
            var rect = new Rect2(x - size.X / 2f - 20, y - size.Y / 2f, size.X + 40, size.Y);
            TextDraw.Center(c, font, new Vector2(x, y), label, itemSize, color);
            HitRects.Add(rect.GrowIndividual(20, 9, 20, 9));
        }
        if (Capture != null)
        {
            TextDraw.RightBottom(c, Fonts.Body, new Vector2(S.Width - 24, S.Height - 18),
                I18n.T("press_any_key"), Fonts.Small, Palette.Yellow);
        }
        if (Hint != null)
        {
            var (text, color) = Hint();
            if (!string.IsNullOrEmpty(text))
            {
                TextDraw.Center(c, Fonts.Pick(text), new Vector2(S.Width / 2f, 692), text,
                    Fonts.Small, color);
            }
        }
    }
}
