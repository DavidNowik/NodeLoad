using UnityEngine;

public enum TileDecor { Tufts, Tree, Rock, Ripples }

// One kind of hexagon tile: how it looks and what it does. yields = what a Miner on it mines, hosts = whether Refineries and Markets can stand on it.
[CreateAssetMenu(menuName = "NodeLoad/Tile")]
public class TileDef : ScriptableObject
{
    public Color fill = Color.green;
    public Color edge = new Color(.96f, .9f, .74f);
    public ItemDef yields;
    public string verb = "Mining";
    public TileDecor decor;
    public bool hosts;
    public bool isBase;              // the standard tile that can be turned into a special one
    public int baseCost = 10;
    public float costGrowth = 1.15f;
    public float starterCostFactor = 5f;

    public static TileDef Make(string name, Color fill, ItemDef yields, string verb, TileDecor decor, bool hosts, int baseCost)
    {
        var d = CreateInstance<TileDef>();
        d.name = name;
        d.fill = fill;
        d.yields = yields;
        d.verb = verb;
        d.decor = decor;
        d.hosts = hosts;
        d.baseCost = baseCost;
        return d;
    }
}
