using Godot;

namespace AshHollow;

/// <summary>篝火(对应旧 bonfire.py):视觉 + 交互范围。</summary>
public partial class Bonfire : Node2D
{
    public const float Radius = 60;
    public float T;

    public void Update(float dt)
    {
        T += dt;
        QueueRedraw();
    }

    public bool InRange(Vector2 center) => center.DistanceSquaredTo(Position) <= Radius * Radius;

    public override void _Draw()
    {
        float flicker = 90 + 25 * Mathf.Sin(T * 9) + GD.RandRange(-10, 10);
        DrawCircle(Vector2.Zero, 66, Palette.RGB(240, 140, 40, (int)(flicker * 0.35f)));
        DrawLine(new Vector2(-26, 34), new Vector2(26, 34), Palette.RGB(90, 55, 30), 9);
        DrawLine(new Vector2(-30, 30), new Vector2(30, 30), Palette.RGB(110, 70, 40), 6);
        for (int i = 0; i < 5; i++)
        {
            float fh = 26 + 8 * Mathf.Sin(T * (7 + i) + i * 2);
            float fx = Mathf.Sin(T * 6 + i * 2.4f) * 8;
            var color = Palette.RGB(255, 160 - i * 12, 40 + i * 6);
            DrawCircle(new Vector2(fx, 14 - i * 4 - fh), Mathf.Max(2, 10 - i * 1.4f), color);
        }
        DrawLine(new Vector2(6, -26), new Vector2(6, 22), Palette.RGB(180, 180, 190), 3);
        DrawLine(new Vector2(0, -18), new Vector2(12, -18), Palette.RGB(120, 110, 60), 3);
    }
}
