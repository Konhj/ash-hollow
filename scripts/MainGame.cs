using Godot;

namespace AshHollow;

/// <summary>
/// 主场景根节点:全局游戏编排(对应旧 pygame 的 main.py 主循环)。
/// 后续在此挂载房间管理、实体、UI。
/// </summary>
public partial class MainGame : Node2D
{
    public override void _Ready()
    {
        DataTables.LoadAll();

        int rooms = (DataTables.Rooms as System.Text.Json.Nodes.JsonObject)?.Count ?? 0;
        int items = (DataTables.Items["items"] as System.Text.Json.Nodes.JsonObject)?.Count ?? 0;
        int enemies = (DataTables.Enemies as System.Text.Json.Nodes.JsonObject)?.Count ?? 0;
        string version = DataTables.Settings["version"]?.GetValue<string>() ?? "?";

        GD.Print($"[AshHollow] v{version} | rooms={rooms} items={items} enemies={enemies} lang={I18n.Lang}");
    }
}
