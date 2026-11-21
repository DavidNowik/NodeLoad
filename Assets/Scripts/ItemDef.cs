using UnityEngine;

[CreateAssetMenu(menuName = "NodeLoad/Item")]
public class ItemDef : ScriptableObject
{
    public Color color = Color.white;
    public float price = 1f;
    public int tier;          // 0 = raw (wood, stone), 1 = refined once (plank, cut stone) ... edges can let only up to a tier through
    public float burnWeight;  // how much an Oven can burn it for: wood 2, plank 5; 0 = does not burn

    public static ItemDef Make(string name, Color color, float price)
    {
        var d = CreateInstance<ItemDef>();
        d.name = name;
        d.color = color;
        d.price = price;
        return d;
    }
}
