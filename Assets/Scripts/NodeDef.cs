using UnityEngine;

public enum NodeBehaviour { Extractor, Processor, Seller, Quest, Storage, Oven, Turbine }

// Extractor: 0 inputs, output comes from the tile it overlaps.
// Processor: any number of recipes, each with any number of inputs. Output can feed another Processor.
// Seller: sells any item at once for cash, up to an income cap per second.
// Quest: takes the item of the current level (LevelDef) and counts it toward the goal.
// Storage: keeps up to `capacity` of one item (the first one it receives decides which). It is offered items before the Quest node and Markets.
// Oven: burns items (ItemDef.burnWeight) into a heat meter that burns down by itself; heat edges lead from it to Steam Turbines.
//       While it has heat it also works its recipe (sand -> glass) and sends the result over normal edges.
// Turbine: takes heat edges and water, and while it has both speeds up the Miners, Refineries and Storages around it.
[CreateAssetMenu(menuName = "NodeLoad/Node")]
public class NodeDef : ScriptableObject
{
    public string displayName;
    public NodeBehaviour behaviour;
    public int baseCost = 10;
    public float costGrowth = 1.5f;   // each further one costs this many times more
    public bool firstFree;            // the first one costs 0 and the curve starts with the second
    public int maxOwned;              // 0 = no limit
    public float cycleTime = 1f;
    public float incomeCap = 10f;     // Seller: most money it can earn per second; the rest goes to another Market or is lost
    public float capWindow = 3f;     // Seller: the cap is checked over this many seconds, so earnings can be credited (unused seconds count, up to this many)
    public float outputRate = 5f;     // Storage: items per second it can hand on over its chest edge
    public int capacity = 100;        // Storage: how many of its one item it holds
    public Ingredient[] buildCost;    // Build Node: needs these items delivered after it is placed, then it works (empty = ready at once)
    public RecipeDef[] recipes;       // Oven: its one recipe, worked on only while it has heat
    public bool anyRecipe;            // Processor: takes the input of any of its recipes, no first-input lock (a Crusher takes stone or cut stone alike)

    // Oven and Turbine
    public float maxHeat = 100f;      // size of the heat meter, in burn weight
    public float burnRate = 1f;       // Oven: heat lost per second
    public int itemSlots = 10;        // Oven and Turbine: how many items wait inside
    public float effectRadius = 5f;   // Turbine: the boost reaches buildings this far away (units)
    public float fullFrom = .95f;     // Oven and Turbine: this share of the meter or more counts as full (same display, same boost)
    public float maxBoost = 2f;       // Turbine: the speed multiplier at full heat (1 at no heat)
    public ItemDef feedItem;          // Turbine: what it consumes while it works
    public float feedRate = 1f;       // Turbine: how many of it per second
    public float heatDraw = 2f;       // Turbine: heat it takes from its linked Ovens per second while it works

    // Markets, Quest nodes, Storages and Turbines only receive: no item edges lead out of them (an Oven sends what it makes, e.g. glass)
    public bool IsSink { get { return behaviour == NodeBehaviour.Seller || behaviour == NodeBehaviour.Quest || behaviour == NodeBehaviour.Storage || behaviour == NodeBehaviour.Turbine; } }

    public static NodeDef Make(string name, NodeBehaviour behaviour, int baseCost, float cycleTime, RecipeDef[] recipes)
    {
        var d = CreateInstance<NodeDef>();
        d.name = d.displayName = name;
        d.behaviour = behaviour;
        d.baseCost = baseCost;
        d.cycleTime = cycleTime;
        d.recipes = recipes;
        return d;
    }
}
