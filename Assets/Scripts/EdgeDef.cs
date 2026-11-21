using UnityEngine;

// Open: carries everything. Tier: only items up to (or exactly) a tier. Filter: a list of items that may (Allow) or may not (Deny) pass.
// Chest: leaves a built Storage, any distance, ignores collisions, and delivers to exactly the building it points at and nowhere else.
// Heat: leaves an Oven and ends at a Steam Turbine; carries heat, not items. Free, drawn automatically when dragging out of an Oven.
public enum EdgeKind { Open, Tier, Filter, Chest, Heat }

// A special kind of edge the player can draw. Like buildings, each one costs more than the last, and it must be unlocked first:
// a level lists the edges it introduces (LevelDef.unlocksEdges).
[CreateAssetMenu(menuName = "NodeLoad/Edge")]
public class EdgeDef : ScriptableObject
{
    public string displayName;
    public EdgeKind kind;
    public int baseCost = 10;
    public float costGrowth = 1.3f;
    public Color color = Color.white;

    public static EdgeDef Make(string name, EdgeKind kind, int baseCost, float costGrowth, Color color)
    {
        var d = CreateInstance<EdgeDef>();
        d.name = d.displayName = name;
        d.kind = kind;
        d.baseCost = baseCost;
        d.costGrowth = costGrowth;
        d.color = color;
        return d;
    }
}
