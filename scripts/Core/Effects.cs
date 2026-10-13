using System.Collections.Generic;
using Godot;

namespace AshHollow;

/// <summary>粒子系统(对应旧 effects.py)。</summary>
public class Particles
{
    struct P
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Life, MaxLife;
        public Color Color;
        public float Size;
    }

    readonly List<P> _items = new();

    public void Spawn(Vector2 pos, Color color, int count, float speed, float life)
    {
        for (int i = 0; i < count; i++)
        {
            float ang = GD.Randf() * Mathf.Tau;
            float sp = (float)GD.RandRange(speed * 0.4f, speed);
            _items.Add(new P
            {
                Pos = pos,
                Vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp,
                Life = (float)GD.RandRange(life * 0.5f, life),
                MaxLife = life,
                Color = color,
                Size = (float)GD.RandRange(2, 4),
            });
        }
    }

    public void Update(float dt)
    {
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            var p = _items[i];
            p.Pos += p.Vel * dt * 60f;
            p.Vel += new Vector2(0, 0.06f * dt * 60f);
            p.Life -= dt;
            if (p.Life <= 0)
            {
                _items.RemoveAt(i);
            }
            else
            {
                _items[i] = p;
            }
        }
    }

    public void Draw(CanvasItem c)
    {
        foreach (var p in _items)
        {
            float a = p.Life / p.MaxLife;
            c.DrawCircle(p.Pos, p.Size, new Color(p.Color.R, p.Color.G, p.Color.B, p.Color.A * a));
        }
    }

    public void Clear() => _items.Clear();
}
