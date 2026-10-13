using Godot;

namespace AshHollow;

/// <summary>敌人出生点:在房间场景中编辑(位置 = 出生坐标,eid 指向敌人模板,巡逻点为本房间坐标)。</summary>
public partial class EnemySpawn : Node2D
{
    [Export] public string Eid = "hollow_soldier";
    [Export] public Vector2[] PatrolPoints = System.Array.Empty<Vector2>();

    public Vector2 Pos => GlobalPosition;
}
