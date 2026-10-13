using Godot;

namespace AshHollow;

/// <summary>
/// 实体基类:身体碰撞箱(与攻击判定箱分离),坐标用中心点(对应旧 pygame 的 rect.center)。
/// </summary>
public partial class Entity : Node2D
{
    public Vector2 Size = new(30, 46);
    public bool Dead;

    /// <summary>身体碰撞矩形(世界坐标)。</summary>
    public Rect2 Body => new(Position - Size / 2f, Size);

    public float CenterDist(Entity other) => Position.DistanceTo(other.Position);
}
