using System.Collections.Generic;
using UnityEngine;

// What the player can build, loaded from assets under Assets/Resources/Data: the building types (Nodes) and the tile types (Tiles).
// Items, recipes and tiles are assets too: a new item, recipe or tile type is a new asset, no code.
public class GameData
{
    public List<NodeDef> nodes = new List<NodeDef>();
    public List<TileDef> tiles = new List<TileDef>();
    public List<LevelDef> levels = new List<LevelDef>();
    public List<EdgeDef> edges = new List<EdgeDef>();   // the special edges the player picks in the A8 menu
    public EdgeDef chestEdge;                           // drawn automatically when an edge is dragged out of a finished Storage; free, and no other way to get it
    public EdgeDef heatEdge;                            // drawn automatically when an edge is dragged out of a built Oven; free

    public static GameData Load()
    {
        var d = new GameData();
        foreach (var n in new[] { "Miner", "Refinery", "Market", "Quest", "Storage", "Oven", "Steam Turbine", "Crusher" })
        {
            var def = Resources.Load<NodeDef>("Data/Nodes/" + n);
            if (def == null) Debug.LogError("Missing NodeDef asset: Assets/Resources/Data/Nodes/" + n + ".asset (run NodeLoad > Create Tiles And Data)");
            else d.nodes.Add(def);
        }
        foreach (var t in new[] { "grass", "forest", "stone", "water" })
        {
            var def = Resources.Load<TileDef>("Data/Tiles/" + t);
            if (def == null) Debug.LogError("Missing TileDef asset: Assets/Resources/Data/Tiles/" + t + ".asset (run NodeLoad > Create Tiles And Data)");
            else d.tiles.Add(def);
        }
        foreach (var e in new[] { "Tier Edge", "Filter Edge" })
        {
            var def = Resources.Load<EdgeDef>("Data/Edges/" + e);
            if (def == null) Debug.LogError("Missing EdgeDef asset: Assets/Resources/Data/Edges/" + e + ".asset (run NodeLoad > Setup)");
            else d.edges.Add(def);
        }
        d.chestEdge = Resources.Load<EdgeDef>("Data/Edges/Chest Edge");
        if (d.chestEdge == null) Debug.LogError("Missing EdgeDef asset: Assets/Resources/Data/Edges/Chest Edge.asset (run NodeLoad > Setup)");
        d.heatEdge = Resources.Load<EdgeDef>("Data/Edges/Heat Edge");
        if (d.heatEdge == null) Debug.LogError("Missing EdgeDef asset: Assets/Resources/Data/Edges/Heat Edge.asset (run NodeLoad > Setup)");
        d.levels.AddRange(Resources.LoadAll<LevelDef>("Data/Levels"));
        d.levels.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return d;
    }
}

// One entry of the tile menu: turn a standard tile into `def`, either next to a tile of that type, or (starter) anywhere for a limited number of times.
public struct TileOffer
{
    public TileDef def;
    public bool starter;
}
