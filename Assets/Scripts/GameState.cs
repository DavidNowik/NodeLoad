using System.Collections.Generic;

// Everything that would be saved: currency, cumulative stats, unlocked tiles (by stable name).
[System.Serializable]
public class GameState
{
    public const double StartCash = 10;   // every level starts with this; money does not carry over

    public double cash = StartCash;
    public int sold;
    public int level;       // index of the current level
    public Dictionary<string, int> deliveredBy = new Dictionary<string, int>();   // per item: delivered to the Quest node so far in this level
    public Dictionary<string, int> soldBy = new Dictionary<string, int>();
    public List<string> unlockedTiles = new List<string>();   // names of the tiles unlocked so far, in order

    public int DeliveredOf(string item) { return deliveredBy.TryGetValue(item, out var n) ? n : 0; }
    public void AddDelivered(string item, int n) { deliveredBy[item] = DeliveredOf(item) + n; }

    public int SoldOf(string item) { return soldBy.TryGetValue(item, out var n) ? n : 0; }

    public void AddSold(string item)
    {
        sold++;
        soldBy[item] = SoldOf(item) + 1;
    }
}
