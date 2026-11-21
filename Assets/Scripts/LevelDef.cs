using UnityEngine;

// Special tiles a level puts on its fresh map: `count` standard tiles become `tile`.
// hidden = somewhere under the "?" tiles outside the starting area; otherwise inside the starting area.
// lockedRing (hidden only): 1 = only in the first ring of "?" tiles around the open area, 2 = the second ring, ... 0 = anywhere.
[System.Serializable]
public struct TileSpawn
{
    public TileDef tile;
    public int count;
    public bool hidden;
    public int lockedRing;
}

// Something the Quest node has to receive: `amount` of `item`.
[System.Serializable]
public struct LevelGoal
{
    public ItemDef item;
    public int amount;

    public LevelGoal(ItemDef item, int amount)
    {
        this.item = item;
        this.amount = amount;
    }
}

// One level: deliver every goal to the Quest node. Levels run in asset name order (Level 01, Level 02, ...).
// Every level is played on a new map; without spawns it gets one forest tile in the starting area.
[CreateAssetMenu(menuName = "NodeLoad/Level")]
public class LevelDef : ScriptableObject
{
    public LevelGoal[] goals;
    public float timeLimit;          // seconds to deliver everything; 0 = no limit
    public bool clockAtStart;        // the clock runs from the moment the level loads, instead of from placing the Quest node
    public float openRings = 2.5f;   // tiles up to this ring distance start open (2.5 = the centre and two rings, 19 tiles), the rest are locked
    public int spawnGap = 3;         // special start tiles are placed at least this many tiles away from each other (if the map has room)
    public EdgeDef[] unlocksEdges;   // special edges that become available from this level on
    public TileSpawn[] spawns;

    // "100 plank" or "300 plank, 300 cut stone"
    public string GoalText
    {
        get
        {
            string s = "";
            foreach (var g in goals) s += (s.Length > 0 ? ", " : "") + g.amount + " " + g.item.name;
            return s;
        }
    }

    public static LevelDef Make(string name, LevelGoal[] goals, params TileSpawn[] spawns)
    {
        var d = CreateInstance<LevelDef>();
        d.name = name;
        d.goals = goals;
        d.spawns = spawns;
        return d;
    }
}
