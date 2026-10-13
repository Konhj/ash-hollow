using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;

namespace AshHollow;

/// <summary>房间连接点(门/雾门)。</summary>
public class ConnDef
{
    public string To;
    public Rect2 Rect;
    public Vector2 Spawn;
    public string Kind;
    public string Sealed;
}

/// <summary>房间数据(对应旧 rooms.py ROOMS 条目)。</summary>
public class RoomDef
{
    public string Id;
    public Vector2 Size;
    public Vector2 PlayerSpawn;
    public Vector2? BonfirePos;
    public string Banner;
    public bool Combat;
    public List<Rect2> SolidRects = new();
    public List<ConnDef> Connections = new();
    public List<(string Eid, Vector2 Pos)> EnemySpawns = new();
}

/// <summary>房间数据表加载器。</summary>
public static class RoomDb
{
    public static Dictionary<string, RoomDef> Rooms = new();

    public static void Load()
    {
        Rooms.Clear();
        var root = DataTables.Rooms as JsonObject;
        foreach (var kv in root)
        {
            var o = kv.Value as JsonObject;
            var r = new RoomDef { Id = kv.Key };
            r.Size = Vec(o["size"]);
            r.PlayerSpawn = Vec(o["player_spawn"]);
            if (o["bonfire"] is JsonArray)
            {
                r.BonfirePos = Vec(o["bonfire"]);
            }
            r.Banner = o["banner"]?.GetValue<string>();
            r.Combat = o["combat"]?.GetValue<bool>() ?? false;

            foreach (var sr in o["solid_rects"].AsArray())
            {
                var a = sr.AsArray();
                r.SolidRects.Add(new Rect2(
                    (float)a[0].GetValue<double>(), (float)a[1].GetValue<double>(),
                    (float)a[2].GetValue<double>(), (float)a[3].GetValue<double>()));
            }
            foreach (var cn in o["connections"].AsArray())
            {
                var c = cn.AsObject();
                var ra = c["rect"].AsArray();
                r.Connections.Add(new ConnDef
                {
                    To = c["to"].GetValue<string>(),
                    Rect = new Rect2(
                        (float)ra[0].GetValue<double>(), (float)ra[1].GetValue<double>(),
                        (float)ra[2].GetValue<double>(), (float)ra[3].GetValue<double>()),
                    Spawn = Vec(c["spawn"]),
                    Kind = c["kind"].GetValue<string>(),
                    Sealed = c["sealed"]?.GetValue<string>(),
                });
            }
            if (o["enemies"] is JsonArray ea)
            {
                foreach (var en in ea)
                {
                    var arr = en.AsArray();
                    r.EnemySpawns.Add((arr[0].GetValue<string>(), Vec(arr[1])));
                }
            }
            Rooms[kv.Key] = r;
        }
    }

    static Vector2 Vec(JsonNode n)
    {
        var a = n.AsArray();
        return new Vector2((float)a[0].GetValue<double>(), (float)a[1].GetValue<double>());
    }

    public static RoomDef Get(string id) => Rooms.TryGetValue(id, out var r) ? r : null;
}
