using UnityEngine;

// What an upgrade changes. None = a placeholder that does nothing yet. Add a value here and one case where it is used to make a new kind of upgrade.
// StartCash: every level starts with `amount` money. StarterTiles: `amount` more starter tiles of `tile` may be placed per level.
// MarketCap: every Market can earn up to `amount` money per second. SellValue: items of tier `tier` sell for `amount` times their price.
public enum BuffEffect { None, StartCash, StarterTiles, MarketCap, SellValue }

// A permanent upgrade bought in the main menu with stars. The asset name is its id in the save file.
[CreateAssetMenu(menuName = "NodeLoad/Buff")]
public class BuffDef : ScriptableObject
{
    [TextArea] public string description;
    public int cost = 1;          // in stars
    public BuffEffect effect;
    public float amount;
    public TileDef tile;          // StarterTiles: which tile type
    public int tier;              // SellValue: which item tier

    public static BuffDef Make(string name, string description, int cost, BuffEffect effect, float amount, TileDef tile = null, int tier = 0)
    {
        var d = CreateInstance<BuffDef>();
        d.name = name;
        d.description = description;
        d.cost = cost;
        d.effect = effect;
        d.amount = amount;
        d.tile = tile;
        d.tier = tier;
        return d;
    }
}
