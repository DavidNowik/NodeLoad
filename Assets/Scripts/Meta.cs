using UnityEngine;

// The progress that lasts beyond one level: stars (1 for every completed level) and the upgrades bought with them.
public static class Meta
{
    public static int Stars { get { return SaveSystem.Data.stars; } }

    public static BuffDef[] AllBuffs() { return Resources.LoadAll<BuffDef>("Data/Buffs"); }

    public static bool Owns(BuffDef b) { return SaveSystem.Data.buffs.Contains(b.name); }

    public static bool CanBuy(BuffDef b) { return b.effect != BuffEffect.None && !Owns(b) && Stars >= b.cost; }


    // how many "starter unique" tiles of this type may be placed per level: none until upgrades give them
    public static int StarterLimit(TileDef t)
    {
        int n = 0;
        foreach (var b in AllBuffs())
            if (b.effect == BuffEffect.StarterTiles && b.tile == t && Owns(b)) n += Mathf.RoundToInt(b.amount);
        return n;
    }

    public static bool TryBuy(BuffDef b)
    {
        if (!CanBuy(b)) return false;
        SaveSystem.Data.stars -= b.cost;
        SaveSystem.Data.buffs.Add(b.name);
        SaveSystem.Save();
        return true;
    }

    // what an item sells for: its price times the best bought "sell value" upgrade for its tier
    public static float SellPrice(ItemDef item)
    {
        float mult = 1f;
        foreach (var b in AllBuffs())
            if (b.effect == BuffEffect.SellValue && b.tier == item.tier && Owns(b)) mult = Mathf.Max(mult, b.amount);
        return item.price * mult;
    }

    // the most money a Market earns per second: its own cap, or what the best bought "market cap" upgrade gives
    public static float MarketCap(float basic)
    {
        float cap = basic;
        foreach (var b in AllBuffs())
            if (b.effect == BuffEffect.MarketCap && Owns(b)) cap = Mathf.Max(cap, b.amount);
        return cap;
    }

    // the money every level starts with: the normal amount, or what the best bought "start cash" upgrade gives
    public static double StartCash()
    {
        double cash = GameState.StartCash;
        foreach (var b in AllBuffs())
            if (b.effect == BuffEffect.StartCash && Owns(b)) cash = System.Math.Max(cash, b.amount);
        return cash;
    }

    // a level was completed: it is remembered and earns one star
    public static void LevelCompleted(int index)
    {
        var d = SaveSystem.Data;
        if (!d.completed.Contains(index)) d.completed.Add(index);
        d.stars++;
        SaveSystem.Save();
    }
}
