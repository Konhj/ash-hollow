using System.Collections.Generic;
using Godot;

namespace AshHollow;

/// <summary>
/// 碰撞求解器(对应旧 pygame main.py 的 resolve_terrain / resolve_entity_collision):
/// 地形与实体碰撞共用同一套矩形解析,实体间成对分离(沿最小穿透轴各推一半)。
/// </summary>
public static class Collision
{
    public static void ResolveTerrain(Entity e, List<Rect2> solidRects)
    {
        foreach (var s in solidRects)
        {
            var r = e.Body;
            if (!r.Intersects(s, includeBorders: true))
            {
                continue;
            }
            float left = r.End.X - s.Position.X;
            float right = s.End.X - r.Position.X;
            float top = r.End.Y - s.Position.Y;
            float bottom = s.End.Y - r.Position.Y;
            float m = Mathf.Min(Mathf.Min(left, right), Mathf.Min(top, bottom));
            if (m == left)
            {
                e.Position = new Vector2(s.Position.X - e.Size.X / 2f, e.Position.Y);
            }
            else if (m == right)
            {
                e.Position = new Vector2(s.End.X + e.Size.X / 2f, e.Position.Y);
            }
            else if (m == top)
            {
                e.Position = new Vector2(e.Position.X, s.Position.Y - e.Size.Y / 2f);
            }
            else
            {
                e.Position = new Vector2(e.Position.X, s.End.Y + e.Size.Y / 2f);
            }
        }
    }

    public static void ResolveEntities(List<Entity> bodies)
    {
        for (int i = 0; i < bodies.Count; i++)
        {
            for (int j = i + 1; j < bodies.Count; j++)
            {
                var a = bodies[i];
                var b = bodies[j];
                var ar = a.Body;
                var br = b.Body;
                if (!ar.Intersects(br, includeBorders: true))
                {
                    continue;
                }
                float dx = ar.GetCenter().X - br.GetCenter().X;
                float dy = ar.GetCenter().Y - br.GetCenter().Y;
                if (dx == 0 && dy == 0)
                {
                    dx = 1;
                }
                float ox = (ar.Size.X / 2f + br.Size.X / 2f) - Mathf.Abs(dx);
                float oy = (ar.Size.Y / 2f + br.Size.Y / 2f) - Mathf.Abs(dy);
                if (ox < oy)
                {
                    float push = ox / 2f;
                    float s = dx >= 0 ? 1 : -1;
                    a.Position += new Vector2(s * push, 0);
                    b.Position -= new Vector2(s * push, 0);
                }
                else
                {
                    float push = oy / 2f;
                    float s = dy >= 0 ? 1 : -1;
                    a.Position += new Vector2(0, s * push);
                    b.Position -= new Vector2(0, s * push);
                }
            }
        }
    }
}
