using Godot;

namespace AshHollow;

/// <summary>
/// 实体基类:CharacterBody2D(身体碰撞走 Godot 物理引擎;攻击判定箱仍是数据矩形,与碰撞体分离)。
/// 碰撞层:地形=1,实体=2(实体掩码=地形|实体,互不可穿过)。
/// </summary>
public partial class Entity : CharacterBody2D
{
    public const uint LayerTerrain = 1;
    public const uint LayerEntities = 2;

    /// <summary>逻辑体型(攻击判定/绘制用,数据驱动),与物理碰撞体独立。</summary>
    public Vector2 Size = new(30, 46);

    public bool Dead;

    /// <summary>逻辑身体矩形(世界坐标),用于攻击判定与交互。</summary>
    public Rect2 Body => new(Position - Size / 2f, Size);

    public float CenterDist(Entity other) => Position.DistanceTo(other.Position);

    public override void _Ready()
    {
        CollisionLayer = LayerEntities;
        CollisionMask = LayerTerrain | LayerEntities;
    }

    /// <summary>更新/创建实体矩形碰撞体(场景中已有时更新,否则创建)。</summary>
    protected void SetCollisionShape(Vector2 size)
    {
        var cs = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (cs == null)
        {
            cs = new CollisionShape2D { Name = "CollisionShape2D" };
            AddChild(cs);
        }
        cs.Shape = new RectangleShape2D { Size = size };
    }

    /// <summary>死亡后不再阻挡他人(对应旧 pygame:尸体从碰撞列表剔除)。</summary>
    protected void DisableCollision()
    {
        SetDeferred(PropertyName.CollisionLayer, 0);
    }

    /// <summary>显隐与碰撞开关(如 Boss 仅在竞技场活动)。</summary>
    public void SetCollisionEnabled(bool enabled)
    {
        SetDeferred(PropertyName.CollisionLayer, enabled ? LayerEntities : 0);
    }
}
