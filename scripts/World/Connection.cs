using Godot;

namespace AshHollow;

/// <summary>
/// 房间连接点(门/雾门):在房间场景中编辑,带目标房间、类型、封印与出生 Marker2D。
/// Area2D 仅作为编辑器可见容器(碰撞层 0,不参与物理)。
/// </summary>
public partial class Connection : Area2D
{
    [Export] public string TargetRoom = "";
    [Export] public string Kind = "door";
    [Export] public string Sealed;

    /// <summary>交互矩形(由子 CollisionShape2D 定义)。</summary>
    public Rect2 InteractRect
    {
        get
        {
            var cs = GetChild<CollisionShape2D>(0);
            if (cs?.Shape is RectangleShape2D rect)
            {
                return new Rect2(cs.Position - rect.Size / 2f, rect.Size);
            }
            return new Rect2(Position, Vector2.Zero);
        }
    }

    public Vector2 SpawnPos => GetNode<Marker2D>("Spawn").GlobalPosition;
}
