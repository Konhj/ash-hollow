using Godot;

namespace AshHollow;

/// <summary>远程弹丸(对应旧 main.py 的 projectiles 列表,玩家/小怪/地形通用)。</summary>
public class Projectile
{
    public Vector2 Pos;
    public Vector2 Vel;
    public float R;
    public int Dmg;
    public float Life;
    public Color Color;
    public bool Gone;

    public Rect2 Rect => new(Pos - new Vector2(R, R), new Vector2(R * 2, R * 2));

    public void Update(float dt)
    {
        Pos += Vel * dt * 60f;
        Life -= dt;
        if (Life <= 0)
        {
            Gone = true;
        }
    }
}
