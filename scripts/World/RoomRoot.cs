using System.Collections.Generic;
using Godot;

namespace AshHollow;

/// <summary>
/// 房间场景根:在场景中配置房间 id/尺寸/横幅;启动时从子节点收集
/// 地形矩形(StaticBody2D)、连接点(Connection)、敌人出生点(EnemySpawn)、篝火与玩家出生点。
/// </summary>
public partial class RoomRoot : Node2D
{
    [Export] public string RoomId = "";
    [Export] public Vector2I RoomSize = new(1280, 720);
    [Export] public string Banner;
    [Export] public bool Combat = true;

    public List<Rect2> SolidRects { get; private set; } = new();
    public List<Connection> Connections { get; private set; } = new();
    public List<EnemySpawn> EnemySpawns { get; private set; } = new();
    public Vector2 PlayerSpawn { get; private set; }
    public Bonfire Bonfire { get; private set; }

    /// <summary>雾门/门动画时间(由 GameManager 每帧注入)。</summary>
    public float AnimTime;

    public override void _Ready()
    {
        SolidRects.Clear();
        var terrain = GetNodeOrNull<Node2D>("Terrain");
        if (terrain != null)
        {
            foreach (var child in terrain.GetChildren())
            {
                if (child is StaticBody2D sb && sb.GetChildCount() > 0
                    && sb.GetChild(0) is CollisionShape2D cs && cs.Shape is RectangleShape2D rect)
                {
                    SolidRects.Add(new Rect2(cs.Position - rect.Size / 2f, rect.Size));
                }
            }
        }

        Connections.Clear();
        var conns = GetNodeOrNull<Node2D>("Connections");
        if (conns != null)
        {
            foreach (var child in conns.GetChildren())
            {
                if (child is Connection c)
                {
                    Connections.Add(c);
                }
            }
        }

        EnemySpawns.Clear();
        var enemies = GetNodeOrNull<Node2D>("Enemies");
        if (enemies != null)
        {
            foreach (var child in enemies.GetChildren())
            {
                if (child is EnemySpawn es)
                {
                    EnemySpawns.Add(es);
                }
            }
        }

        var spawn = GetNodeOrNull<Marker2D>("PlayerSpawn");
        PlayerSpawn = spawn?.GlobalPosition ?? Vector2.Zero;

        Bonfire = GetNodeOrNull<Bonfire>("Bonfire");
    }

    public override void _Draw()
    {
        foreach (var conn in Connections)
        {
            if (conn.Kind == "fog")
            {
                DrawFogGate(conn.InteractRect, conn.Sealed == "boss_alive" && !MainGame.Game.BossDefeated);
            }
            else
            {
                var r = conn.InteractRect;
                DrawRect(r, Palette.RGB(20, 18, 24));
                DrawRect(r, Palette.RGB(110, 100, 80), false, 3);
                DrawLine(
                    new Vector2(r.Position.X + r.Size.X / 2f, r.Position.Y + 4),
                    new Vector2(r.Position.X + r.Size.X / 2f, r.End.Y - 4),
                    Palette.RGB(150, 138, 110), 2);
            }
        }
    }

    void DrawFogGate(Rect2 rect, bool isSealed)
    {
        var band = new Rect2(rect.Position.X, 200, 36, 340);
        Color fill = isSealed ? Palette.RGB(110, 115, 130, 95) : Palette.RGB(200, 205, 215, 60);
        Color circle = isSealed ? Palette.RGB(150, 155, 170, 80) : Palette.RGB(226, 229, 236, 95);
        DrawRect(band, fill);
        for (int i = 0; i < 6; i++)
        {
            float yy = 30 + Mathf.PosMod(AnimTime * (16 + i * 7) + i * 53, 280);
            float xx = 6 + 24 * (0.5f + 0.5f * Mathf.Sin(AnimTime * 2 + i * 2.1f));
            DrawCircle(new Vector2(band.Position.X + xx, band.Position.Y + yy), 9, circle);
        }
    }
}
