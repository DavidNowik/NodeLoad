using System.Collections.Generic;
using UnityEngine;

// Owns the buildings, links and game state, and runs the simulation tick. The tiles come from the scene.
public class World : MonoBehaviour
{
    public const float NodeRadius = .9f, PacketSpeed = 4f;   // a building fills a tile
    public const int QueueCap = 12, MaxNodes = 24;
    public const float MaxLinkRange = 6.5f;   // longest edge, centre to centre (about four tiles)
    const float SnapRange = .11f;             // every snap (a tile corner, a slot next to a building of its kind) happens within this many units of the exact spot
    const float SnapWhileMoving = .5f;        // both of the above are this much smaller while a building is being relocated
    const float LockedReach = .8f;            // a building may overlap a locked tile a little: it is refused only when its centre is closer than this part of a tile step

    public bool autoEdge;                     // toggled in the bottom bar: new buildings link themselves to nearby ones

    public static World Instance;

    public GameData data;
    public GameState state = new GameState();
    public Hud hud;
    public readonly List<Node> nodes = new List<Node>();
    public readonly List<Link> links = new List<Link>();
    public readonly List<Tile> tiles = new List<Tile>();
    Mist mist;
    int tickStart;

    void Awake()
    {
        Instance = this;
        foreach (var old in FindObjectsOfType<Node>()) Destroy(old.gameObject);
        data = GameData.Load();
        // the level chosen in the main menu, or the first one not completed yet
        int start = SaveSystem.PendingLevel >= 0 ? SaveSystem.PendingLevel : SaveSystem.Data.NextLevel;
        SaveSystem.PendingLevel = -1;
        state.level = Mathf.Clamp(start, 0, Mathf.Max(0, data.levels.Count - 1));
        SetupCamera();

        foreach (var t in FindObjectsOfType<Tile>())
        {
            t.Rebuild();
            tiles.Add(t);
        }
        mist = FindObjectOfType<Mist>();
        NewMap();

        var tool = gameObject.AddComponent<BuildTool>();
        tool.world = this;
        hud = gameObject.AddComponent<Hud>();
        hud.world = this;
        hud.tool = tool;
    }

    // Every play and every level starts on a fresh map with the starting money: buildings and edges are gone, all tiles are standard again, the starting area
    // (the centre and two rings, 19 tiles) is open and everything further out is hidden until unlocked. The level's special tiles
    // are then put at random places, some in the starting area and some under the "?" tiles.
    void NewMap()
    {
        foreach (var n in nodes.ToArray()) RemoveNode(n);
        state.unlockedTiles.Clear();
        state.cash = Meta.StartCash();
        state.deliveredBy.Clear();
        questTimeLeft = Goal != null && Goal.clockAtStart && Goal.timeLimit > 0 ? Goal.timeLimit : -1;
        levelOver = false;
        var plain = data.tiles.Find(t => t.isBase);
        foreach (var t in tiles)
        {
            t.def = plain;
            t.starter = false;
            t.locked = Tile.RingF(t.transform.position) > (Goal != null ? Goal.openRings : 2.5f);
        }
        SpawnTiles();
        foreach (var t in tiles) t.Rebuild();
        if (mist != null) mist.ResetClear();
        SetupCamera();
    }

    // which ring of "?" tiles a locked tile is in: 1 = the first ring around the open area, 2 = the next ...
    int LockedRingOf(Tile tile)
    {
        int ring = Mathf.RoundToInt(Tile.RingF(tile.transform.position));
        var rings = new SortedSet<int>();
        foreach (var t in tiles) if (t.locked) rings.Add(Mathf.RoundToInt(Tile.RingF(t.transform.position)));
        int n = 1;
        foreach (var r in rings)
        {
            if (r == ring) return n;
            n++;
        }
        return 0;
    }

    // how many tiles away the nearest special (not standard) tile is; a large number when there is none yet
    float SpecialGap(Tile tile)
    {
        float gap = 99f;
        foreach (var o in tiles)
            if (!o.def.isBase) gap = Mathf.Min(gap, HexNorm((Vector2)o.transform.position - (Vector2)tile.transform.position) / Tile.Step);
        return gap;
    }

    void SpawnTiles()
    {
        var list = new List<TileSpawn>();
        var level = Goal;
        if (level != null && level.spawns != null) list.AddRange(level.spawns);
        if (list.Count == 0)
        {
            var forest = data.tiles.Find(t => t.name == "forest");
            if (forest != null) list.Add(new TileSpawn { tile = forest, count = 1 });
        }
        foreach (var s in list)
            for (int i = 0; i < s.count; i++)
            {
                var pool = tiles.FindAll(t => t.def.isBase && (s.hidden ? t.locked : !t.locked && Tile.RingF(t.transform.position) > .5f));
                if (s.hidden && s.lockedRing > 0) pool = pool.FindAll(t => LockedRingOf(t) == s.lockedRing);
                if (pool.Count == 0) break;
                // special tiles keep their distance from each other; when no tile is far enough, the farthest ones are used
                int gap = level != null ? level.spawnGap : 3;
                var far = pool.FindAll(t => SpecialGap(t) >= gap);
                if (far.Count == 0)
                {
                    float best = 0;
                    foreach (var t in pool) best = Mathf.Max(best, SpecialGap(t));
                    far = pool.FindAll(t => SpecialGap(t) >= best - .01f);
                }
                far[Random.Range(0, far.Count)].def = s.tile;
            }
    }

    bool newMapPending;

    void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.orthographic = true;
        cam.orthographicSize = 5.6f;
        cam.transform.position = new Vector3(0, 0, -10);
        cam.clearFlags = CameraClearFlags.SolidColor;
    }

    void Update()
    {
        float dt = Mathf.Min(.2f, Time.deltaTime);
        ApplyHeat();
        // a different node goes first each frame, so nobody always gets the free slot at a busy Market
        for (int i = 0; i < nodes.Count; i++) nodes[(i + tickStart) % nodes.Count].Tick(dt);
        tickStart++;
        foreach (var l in links) l.Tick(dt);
        TimeTick(dt);
        Gfx.ScrollEdges(dt);
        if (newMapPending) { newMapPending = false; NewMap(); }
    }

    // ---- economy ----
    int Owned(NodeDef d)
    {
        int n = 0;
        foreach (var x in nodes) if (x.def == d) n++;
        return n;
    }

    // price of the next building of this kind when `owned` already exist
    static int CostAt(NodeDef d, int owned)
    {
        if (d.firstFree && owned == 0) return 0;
        return Mathf.CeilToInt(d.baseCost * Mathf.Pow(d.costGrowth, d.firstFree ? owned - 1 : owned));
    }

    public int NewCost(NodeDef d) { return CostAt(d, Owned(d)); }

    // half of what the latest one cost, so buying it again afterwards costs the same as before
    public double Refund(Node n) { return .5 * CostAt(n.def, Owned(n.def) - 1); }

    // holding the right button on a node for a second sells it: its edges are cut, then it is removed and half of its price comes back
    public void DeleteNode(Node n)
    {
        double refund = Refund(n);
        RemoveNode(n);
        state.cash += refund;
        if (refund > 0) FloatText.Spawn(n.transform.position + Vector3.up * .8f, "+€" + NumFmt.Short(refund), new Color(1f, .9f, .55f));
    }

    void RemoveNode(Node n)
    {
        ClearEdges(n, false);
        nodes.Remove(n);
        graphVersion++;
        if (n.def.behaviour == NodeBehaviour.Quest && !(Goal != null && Goal.clockAtStart)) questTimeLeft = -1;   // without a Quest node there is no clock (unless it runs from the start)
        Destroy(n.gameObject);
    }

    public void Sale(ItemDef item)
    {
        state.cash += Meta.SellPrice(item);
        state.AddSold(item.name);
    }

    // ---- levels ----
    public LevelDef Goal { get { return state.level < data.levels.Count ? data.levels[state.level] : null; } }

    // ---- heat: Ovens heat Steam Turbines over heat edges, and a working turbine speeds up the buildings around it ----
    Link HeatLink(Node a, Node b)
    {
        foreach (var l in links) if (l.IsHeat && l.from == a && l.to == b) return l;
        return null;
    }

    // the Ovens heating a turbine: those with a heat edge to it or to any Turbine snapped to it (snapped Turbines share heat like they share water)
    public List<Node> HeatSources(Node turbine)
    {
        var group = new HashSet<Node> { turbine };
        var todo = new Queue<Node>();
        todo.Enqueue(turbine);
        while (todo.Count > 0)
            foreach (var o in todo.Dequeue().outs)
                if (o.IsTurbine && group.Add(o)) todo.Enqueue(o);
        var sources = new List<Node>();
        foreach (var l in links) if (l.IsHeat && group.Contains(l.to) && !sources.Contains(l.from)) sources.Add(l.from);
        return sources;
    }

    // the heat that reaches a turbine: what its Ovens hold, up to the size of its meter
    public float HeatInto(Node turbine)
    {
        float h = 0;
        foreach (var o in HeatSources(turbine)) h += o.Heat;
        return Mathf.Min(h, turbine.def.maxHeat);
    }

    // a turbine takes its heat from the linked Ovens, the hottest first
    public void DrainHeat(Node turbine, float amount)
    {
        var sources = HeatSources(turbine);
        sources.Sort((a, b) => b.Heat.CompareTo(a.Heat));
        foreach (var s in sources)
        {
            amount -= s.TakeHeat(amount);
            if (amount <= 0) break;
        }
    }

    // every building that can be sped up gets the boosts of all the working turbines within reach, multiplied (x2 and x2 is x4)
    void ApplyHeat()
    {
        foreach (var n in nodes) n.speed = 1f;
        foreach (var t in nodes)
        {
            if (!t.IsTurbine) continue;
            float boost = t.Boost;
            if (boost <= 1f) continue;
            foreach (var n in nodes)
                if (n.Boostable && Vector2.Distance(n.transform.position, t.transform.position) <= t.def.effectRadius) n.speed *= boost;
        }
    }

    // how many of a goal item are still missing (0 for an item that is no goal or already complete)
    public int Missing(ItemDef item)
    {
        if (Goal == null) return 0;
        foreach (var g in Goal.goals) if (g.item == item) return Mathf.Max(0, g.amount - state.DeliveredOf(item.name));
        return 0;
    }

    public bool Wants(ItemDef item) { return Missing(item) > 0; }

    // how much of everything the Quest node needs has been delivered, 0 to 1
    public float QuestFraction
    {
        get
        {
            if (Goal == null) return 1f;
            float have = 0, need = 0;
            foreach (var g in Goal.goals)
            {
                have += Mathf.Min(g.amount, state.DeliveredOf(g.item.name));
                need += g.amount;
            }
            return need > 0 ? have / need : 1f;
        }
    }

    // "12/600" for a small label
    public string QuestShort
    {
        get
        {
            if (Goal == null) return "all done";
            int have = 0, need = 0;
            foreach (var g in Goal.goals)
            {
                have += Mathf.Min(g.amount, state.DeliveredOf(g.item.name));
                need += g.amount;
            }
            return have + "/" + need;
        }
    }

    // "plank 12/300, cut stone 0/300"
    public string QuestProgress()
    {
        if (Goal == null) return "all done";
        string s = "";
        foreach (var g in Goal.goals) s += (s.Length > 0 ? ", " : "") + g.item.name + " " + state.DeliveredOf(g.item.name) + "/" + g.amount;
        return s;
    }

    // the Quest node hands in what it received; what is more than the goal needs is not counted
    public void Deliver(ItemDef item, int n)
    {
        if (levelOver) return;
        int before = Missing(item);
        state.AddDelivered(item.name, Mathf.Min(n, before));
        if (before > 0 && Missing(item) == 0) RoutesChanged();   // this goal is full: the Quest node takes no more of it
        foreach (var g in Goal.goals) if (Missing(g.item) > 0) return;
        CompleteLevel();
    }

    // starts the current level again on a new map, with the starting money
    public void ResetLevel() { newMapPending = true; }

    // ---- time limit: a level with a timeLimit starts its clock when the Quest node is placed; running out ends the level ----
    public float questTimeLeft = -1;   // seconds left, or negative while no clock is running
    bool levelOver;                    // completed or failed: the game stands still behind the screen that is shown

    public string QuestTimeText
    {
        get
        {
            if (questTimeLeft < 0) return null;
            int s = Mathf.CeilToInt(questTimeLeft);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }
    }

    void TimeTick(float dt)
    {
        if (questTimeLeft < 0 || levelOver) return;
        questTimeLeft -= dt;
        if (questTimeLeft > 0) return;
        questTimeLeft = -1;
        levelOver = true;
        hud.ShowLevelFailed(Goal, QuestProgress());
    }

    // the level is won: it is remembered in the save file and earns a star; the success screen leads back to the level menu
    void CompleteLevel()
    {
        var done = Goal;
        levelOver = true;
        questTimeLeft = -1;
        Meta.LevelCompleted(state.level);
        hud.ShowLevelComplete(done, state.level + 1 < data.levels.Count ? data.levels[state.level + 1] : null);
    }

    // ---- unlocking tiles ----
    // Tiles outside the starting area are blacked out with a "?". Unlocking one costs more each time:
    // 10, 20 ... 100, then 200, 300 ... 1000, then 2000, 3000 ...
    public int UnlockCost() { return UnlockCostAt(state.unlockedTiles.Count); }

    // price of the tile unlocked after `k` others
    static int UnlockCostAt(int k)
    {
        if (k < 10) return 10 * (k + 1);
        if (k < 19) return (k - 8) * 100;
        return (k - 17) * 1000;
    }

    public const float RingDiscount = .3f;   // unlocking a whole ring at once costs this much less than one by one

    // The next ring is the innermost one that still has "?" tiles. Opening all of them at once costs what unlocking them one by one
    // would cost from here on (the price rises with each one), minus the discount.
    public List<Tile> NextRingTiles()
    {
        int ring = int.MaxValue;
        foreach (var t in tiles) if (t.locked) ring = Mathf.Min(ring, Mathf.RoundToInt(Tile.RingF(t.transform.position)));
        return tiles.FindAll(t => t.locked && Mathf.RoundToInt(Tile.RingF(t.transform.position)) == ring);
    }

    // one by one price of the next ring, and the price with the discount
    public void NextRingCost(out int oneByOne, out int discounted)
    {
        int count = NextRingTiles().Count, sum = 0;
        for (int i = 0; i < count; i++) sum += UnlockCostAt(state.unlockedTiles.Count + i);
        oneByOne = sum;
        discounted = Mathf.CeilToInt(sum * (1f - RingDiscount));
    }

    public void UnlockNextRing()
    {
        var ring = NextRingTiles();
        NextRingCost(out _, out int cost);
        if (ring.Count == 0 || state.cash < cost) return;
        state.cash -= cost;
        foreach (var t in ring)
        {
            state.unlockedTiles.Add(t.name);
            t.locked = false;
            t.Rebuild();
            if (mist != null && Tile.RingF(t.transform.position) > 5.5f) mist.Expand(new Vector2(10f, 10f));
        }
    }

    bool HasUnlockedNeighbour(Tile t)
    {
        foreach (var o in tiles)
            if (!o.locked && Vector2.Distance(o.transform.position, t.transform.position) < Tile.Step * 1.1f) return true;
        return false;
    }

    public bool CanUnlock(Tile t, out string why)
    {
        why = "";
        if (!HasUnlockedNeighbour(t)) { why = "reach it from an unlocked tile"; return false; }
        if (state.cash < UnlockCost()) { why = "not enough money"; return false; }
        return true;
    }

    public void Unlock(Tile t)
    {
        if (!t.locked || !CanUnlock(t, out _)) return;
        state.cash -= UnlockCost();
        state.unlockedTiles.Add(t.name);
        t.locked = false;
        t.Rebuild();
        // reaching the outermost ring clears the mist, and with it the edge of the map shows
        if (mist != null && Tile.RingF(t.transform.position) > 5.5f) mist.Expand(new Vector2(10f, 10f));
    }

    // ---- placement ----
    public Tile TileAt(Vector2 p)
    {
        foreach (var t in tiles) if (t.Contains(p)) return t;
        return null;
    }

    public TileDef GroundAt(Vector2 p)
    {
        var t = TileAt(p);
        return t != null && !t.locked ? t.def : null;
    }

    // A Miner works as soon as it overlaps a tile that yields something (it need not be centred on it).
    // Placed on a corner it touches three tiles; the nearest one that yields decides what it mines.
    public TileDef MineGroundAt(Vector2 p)
    {
        Tile best = null;
        float bd = Tile.Radius * 1.15f;
        foreach (var t in tiles)
        {
            if (t.locked || t.def.yields == null) continue;
            float d = Vector2.Distance(t.transform.position, p);
            if (d <= bd) { bd = d; best = t; }
        }
        return best != null ? best.def : null;
    }

    // a building may not reach over tiles that are still locked ("?")
    public bool TouchesLocked(Vector2 p)
    {
        foreach (var t in tiles) if (t.locked && Vector2.Distance(t.transform.position, p) < Tile.Step * LockedReach) return true;
        return false;
    }

    public TileDef GroundFor(NodeDef d, Vector2 p) { return d.behaviour == NodeBehaviour.Extractor ? MineGroundAt(p) : GroundAt(p); }

    // Miners snap to Miners and Refineries to Refineries. Anything else has to keep a visible gap, so the edges of both stay readable.
    static bool CanSnap(NodeDef a, NodeDef b) { return a == b && (!a.IsSink || a.behaviour == NodeBehaviour.Turbine) && a.behaviour != NodeBehaviour.Oven; }

    // hexagon-shaped distance (pointy top): equals the apothem at a hexagon's edge, so two same-size hexagons touch when it is 2 apothems
    public static float HexNorm(Vector2 d)
    {
        float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y);
        return Mathf.Max(ax, ax * .5f + ay * .866f);
    }

    // ignore: a building that is being moved does not block its own new spot
    public bool Overlaps(NodeDef d, Vector2 p, Node ignore = null)
    {
        foreach (var n in nodes)
        {
            if (n == ignore) continue;
            float need = CanSnap(d, n.def) ? Tile.Step * .98f : Tile.Step * 1.1f;
            if (HexNorm(p - (Vector2)n.transform.position) < need) return true;
        }
        return false;
    }

    // Buildings are placed freely, independent of the tile grid. What is under the centre decides what they can do:
    // a Miner needs a tile that yields something, Refineries and Markets need a tile that hosts buildings.
    public bool CanPlace(NodeDef d, Vector2 p, Node ignore = null)
    {
        if (ignore == null && (nodes.Count >= MaxNodes || (d.maxOwned > 0 && Owned(d) >= d.maxOwned))) return false;
        if (d.behaviour == NodeBehaviour.Quest && Goal == null) return false;
        if (Overlaps(d, p, ignore) || TouchesLocked(p) || EdgeThrough(p, ignore)) return false;
        var g = GroundFor(d, p);
        if (g == null) return false;
        return d.behaviour == NodeBehaviour.Extractor ? g.yields != null : g.hosts;
    }

    // ---- tiles ----
    // What the tile menu offers on this level: every special tile the level puts on its map (forest, then stone from level 2),
    // each in two ways, the normal one next to a tile of its kind and the starter one anywhere.
    public List<TileOffer> Offers()
    {
        var list = new List<TileOffer>();
        var seen = new HashSet<TileDef>();
        var g = Goal;
        if (g != null && g.spawns != null)
            foreach (var s in g.spawns)
                if (s.tile != null && seen.Add(s.tile))
                {
                    list.Add(new TileOffer { def = s.tile, starter = false });
                    list.Add(new TileOffer { def = s.tile, starter = true });
                }
        if (list.Count == 0)
        {
            var forest = data.tiles.Find(t => t.name == "forest");
            if (forest != null)
            {
                list.Add(new TileOffer { def = forest, starter = false });
                list.Add(new TileOffer { def = forest, starter = true });
            }
        }
        return list;
    }

    // The map is pre-built. The player only turns standard (grass) tiles into special ones:
    // next to a tile of the same type for the normal price, or anywhere as a limited, pricier "starter unique" tile.
    public int StarterUsed(TileDef d)
    {
        int n = 0;
        foreach (var t in tiles) if (t.def == d && t.starter) n++;
        return n;
    }

    public int ConvertCost(TileDef to, bool starter)
    {
        int n = 0;
        foreach (var t in tiles) if (t.def == to) n++;
        float c = to.baseCost * Mathf.Pow(to.costGrowth, n);
        return Mathf.CeilToInt(starter ? c * to.starterCostFactor : c);
    }

    bool HasNeighbour(Tile t, TileDef type)
    {
        foreach (var o in tiles)
            if (o != t && o.def == type && Vector2.Distance(o.transform.position, t.transform.position) < Tile.Step * 1.1f) return true;
        return false;
    }

    public bool CanConvert(Tile t, TileDef to, bool starter, out string why)
    {
        why = "";
        if (t == null) { why = "no tile"; return false; }
        if (!t.def.isBase) { why = "only standard tiles can change"; return false; }
        if (t.locked) { why = "locked, unlock it first"; return false; }
        if (starter)
        {
            if (StarterUsed(to) >= Meta.StarterLimit(to)) { why = "no starter tiles left"; return false; }
        }
        else if (!HasNeighbour(t, to)) { why = "needs an adjacent " + to.name + " tile"; return false; }
        if (state.cash < ConvertCost(to, starter)) { why = "not enough money"; return false; }
        return true;
    }

    public void Convert(Tile t, TileDef to, bool starter)
    {
        if (!CanConvert(t, to, starter, out _)) return;
        state.cash -= ConvertCost(to, starter);
        t.def = to;
        t.starter = starter;
        t.Rebuild();
        // buildings never block a change; the ones standing on or next to the tile pick up what it is now
        foreach (var n in nodes) n.SetGround(GroundFor(n.def, n.transform.position));
    }

    // Miners snap edge to edge onto other Miners and Refineries onto other Refineries; every building stays an individual node.
    // Everything else stays exactly where it is put; there is no snapping to the background tiles.
    public Vector2 PlaceAt(NodeDef d, Vector2 p, Node ignore = null)
    {
        if (d.behaviour == NodeBehaviour.Seller) return p;
        Vector2 best = p;
        // how close the mouse has to be to a slot for the building to snap onto it; smaller while a building is being moved
        float shrink = ignore != null ? SnapWhileMoving : 1f;
        float bd = SnapRange * shrink;
        bool snapped = false;
        foreach (var n in nodes)
        {
            if (n.def != d || n == ignore) continue;
            for (int k = 0; k < 6; k++)
            {
                float a = k * 60f * Mathf.Deg2Rad;
                Vector2 slot = (Vector2)n.transform.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Tile.Step;
                float dist = Vector2.Distance(slot, p);
                if (dist < bd && !Overlaps(d, slot, ignore)) { bd = dist; best = slot; snapped = true; }
            }
        }
        if (snapped) return best;

        float vd = SnapRange * shrink;
        if (d.behaviour != NodeBehaviour.Extractor)
        {
            // Refineries and the other buildings are as big as a tile: a small snap onto the centre of a tile, so they sit in it exactly
            foreach (var t in tiles)
            {
                float dist = Vector2.Distance(t.transform.position, p);
                if (dist < vd && !Overlaps(d, t.transform.position, ignore)) { vd = dist; best = t.transform.position; }
            }
            return best;
        }

        // No Miner nearby: a small snap onto the "perfect" spots, the corners of a tile. Miners as big as a tile sit
        // edge to edge there, so exactly three fit around each tile (every other corner of it).
        foreach (var t in tiles)
        {
            if (Vector2.Distance(t.transform.position, p) > Tile.Radius + vd) continue;
            for (int k = 0; k < 6; k++)
            {
                float a = (90f + k * 60f) * Mathf.Deg2Rad;
                Vector2 corner = (Vector2)t.transform.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Tile.Radius;
                float dist = Vector2.Distance(corner, p);
                if (dist < vd && !Overlaps(d, corner, ignore)) { vd = dist; best = corner; }
            }
        }
        return best;
    }

    public void Place(NodeDef d, Vector2 p)
    {
        p = PlaceAt(d, p);
        int c = NewCost(d);
        if (!CanPlace(d, p) || state.cash < c) return;
        state.cash -= c;
        Spawn(d, p);
    }

    void Spawn(NodeDef d, Vector2 p)
    {
        var go = new GameObject(d.displayName);
        go.transform.SetParent(transform);
        go.transform.position = p;
        var n = go.AddComponent<Node>();
        n.Init(this, d, GroundFor(d, p));
        nodes.Add(n);
        graphVersion++;
        LinkNeighbours(n);
        if (d.behaviour == NodeBehaviour.Quest && Goal != null && Goal.timeLimit > 0)
        {
            if (!Goal.clockAtStart) questTimeLeft = Goal.timeLimit;   // the clock starts with the Quest node
            state.deliveredBy.Clear();
        }
    }

    // snapped onto a building of its own kind: link both ways with each neighbour straight away; auto edge adds the rest
    void LinkNeighbours(Node n)
    {
        foreach (var o in nodes)
            if (Adjacent(n, o)) { AddLink(n, o, true); AddLink(o, n, true); }
        if (autoEdge) AutoLink(n);
    }

    // holding the left button still on a building for a second picks it up; dropping it somewhere valid moves it.
    // Its old edges are cut, since the distances change, and it links to its new snapped neighbours like a newly placed one.
    public void MoveNode(Node n, Vector2 at)
    {
        ClearEdges(n, false);
        n.Relocate(at, GroundFor(n.def, at));
        graphVersion++;
        LinkNeighbours(n);
    }

    // ---- links ----
    public Link GetLink(Node a, Node b)
    {
        foreach (var l in links) if (l.from == a && l.to == b && !l.OutOfNetwork) return l;
        return null;
    }


    // ---- special edges: price, unlocking, drawing ----
    public int EdgeOwned(EdgeDef d)
    {
        int n = 0;
        foreach (var l in links) if (l.def == d) n++;
        return n;
    }

    static int EdgeCostAt(EdgeDef d, int owned) { return Mathf.CeilToInt(d.baseCost * Mathf.Pow(d.costGrowth, owned)); }
    public int EdgeCost(EdgeDef d) { return d.kind == EdgeKind.Chest || d.kind == EdgeKind.Heat ? 0 : EdgeCostAt(d, EdgeOwned(d)); }

    // a level introduces special edges (LevelDef.unlocksEdges); from then on they are available in every level
    public bool EdgeUnlocked(EdgeDef d)
    {
        int reached = Mathf.Max(state.level, SaveSystem.Data.NextLevel);
        for (int i = 0; i < data.levels.Count && i <= reached; i++)
        {
            var u = data.levels[i].unlocksEdges;
            if (u != null && System.Array.IndexOf(u, d) >= 0) return true;
        }
        return false;
    }

    public bool AnyEdgeUnlocked()
    {
        foreach (var e in data.edges) if (EdgeUnlocked(e)) return true;
        return false;
    }

    // may an item travel over the edge from a to b (a plain edge, or none, lets everything through)
    public bool LinkPasses(Node a, Node b, ItemDef item)
    {
        var l = GetLink(a, b);
        return l == null || l.Passes(item);
    }

    // draws a special edge from a to b and pays for it; null when it is not allowed.
    // A chest edge leaves a built Storage, may be any length and ignores everything on the way; only one is active per chest, a new one replaces it.
    // The others follow the normal rules and replace a plain edge between the same two buildings.
    public Link AddSpecial(Node a, Node b, EdgeDef def, out string why)
    {
        why = null;
        if (a == b) return null;
        if (def.kind == EdgeKind.Heat)
        {
            // a heat edge leaves a finished Oven and ends at a finished Steam Turbine; dragging the same one again removes it
            var existing = HeatLink(a, b);
            if (existing != null) { Remove(existing); return null; }
            if (!a.IsOven) { why = "heat leaves an Oven"; return null; }
            if (!b.IsTurbine) { why = "heat only goes into a Steam Turbine"; return null; }
            return AddLink(a, b, false, def);
        }
        if (def.kind != EdgeKind.Chest && !EdgeUnlocked(def)) { why = "not unlocked yet"; return null; }   // the chest edge comes with the Storage
        // replacing an edge of the same kind only costs the difference (the old one is refunded half)
        var replaced = def.kind == EdgeKind.Chest ? a.chest : GetLink(a, b);
        int owned = EdgeOwned(def);
        double need = def.kind == EdgeKind.Chest ? 0 : replaced != null && replaced.def == def ? EdgeCostAt(def, owned - 1) * .5 : EdgeCost(def);
        if (state.cash < need) { why = "needs €" + Mathf.CeilToInt((float)need); return null; }
        if (def.kind == EdgeKind.Chest)
        {
            if (!a.IsChest) { why = "a chest edge leaves a finished Storage"; return null; }
            if (a.chest != null) Remove(a.chest);
        }
        else
        {
            if (a.def.IsSink) { why = "nothing leaves a " + a.def.displayName; return null; }
            if (Adjacent(a, b) || !InRange(a, b) || EdgeBlocked(a, b) || PathExists(b, a)) { why = "cannot link here"; return null; }
            var old = GetLink(a, b);
            if (old != null) Remove(old);
        }
        state.cash -= EdgeCost(def);
        return AddLink(a, b, false, def);
    }
    static bool PathExists(Node a, Node b) { return PathExists(a, b, new HashSet<Node>()); }

    static bool PathExists(Node a, Node b, HashSet<Node> seen)
    {
        if (a == b) return true;
        if (!seen.Add(a)) return false;
        foreach (var o in a.outs) if (PathExists(o, b, seen)) return true;
        return false;
    }

    // How many edges each node is from a receiver of this item (0 = it takes the item itself). Items always move to a neighbour that is
    // one edge closer, so they travel over the edges to a receiver and can never run in circles. Cached until the graph changes.
    readonly Dictionary<ItemDef, Dictionary<Node, int>> distCache = new Dictionary<ItemDef, Dictionary<Node, int>>();
    int graphVersion, cacheVersion = -1, cacheFrame = -1;

    // something changed what a node accepts (a Refinery got its recipe or lost it): the distance maps must be rebuilt
    public void RoutesChanged() { graphVersion++; }

    public Dictionary<Node, int> DistMap(ItemDef item)
    {
        // rebuilt when the graph changes and once per frame, because which receivers still have room changes all the time
        if (cacheVersion != graphVersion || cacheFrame != Time.frameCount) { distCache.Clear(); cacheVersion = graphVersion; cacheFrame = Time.frameCount; }
        if (distCache.TryGetValue(item, out var cached)) return cached;
        var dist = new Dictionary<Node, int>();
        var queue = new Queue<Node>();
        // only receivers that can take another one count: a full Refinery must not pull every Miner next to it into a dead end
        foreach (var n in nodes) if (n.Accepts(item) && n.CanTake(item)) { dist[n] = 0; queue.Enqueue(n); }
        while (queue.Count > 0)
        {
            var x = queue.Dequeue();
            foreach (var y in nodes)
                if (!dist.ContainsKey(y) && y.outs.Contains(x) && LinkPasses(y, x, item)) { dist[y] = dist[x] + 1; queue.Enqueue(y); }
        }
        distCache[item] = dist;
        return dist;
    }

    // another Market in the same connected network (edges followed in either direction) that can still earn this second
    public Node OtherSellerWithRoom(Node from)
    {
        var seen = new HashSet<Node> { from };
        var queue = new Queue<Node>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var x = queue.Dequeue();
            foreach (var y in nodes)
            {
                if (seen.Contains(y) || !(x.outs.Contains(y) || y.outs.Contains(x))) continue;
                seen.Add(y);
                if (y.def.behaviour == NodeBehaviour.Seller && y.HasRoom) return y;
                queue.Enqueue(y);
            }
        }
        return null;
    }

    Link AddLink(Node a, Node b, bool paired, EdgeDef def = null)
    {
        graphVersion++;
        var go = new GameObject("Link");
        go.transform.SetParent(transform);
        var l = go.AddComponent<Link>();
        l.Init(a, b, paired, def);
        links.Add(l);
        if (l.IsChest) a.chest = l;   // a chest edge is not part of the network
        else if (!l.IsHeat) a.outs.Add(b);   // a heat edge carries heat, not items
        return l;
    }

    public static bool InRange(Node a, Node b) { return Vector2.Distance(a.transform.position, b.transform.position) <= MaxLinkRange; }

    // an edge may not run through another building or cross another edge (edges that meet at a shared building are fine)
    public bool EdgeBlocked(Node a, Node b)
    {
        Vector2 p = a.transform.position, q = b.transform.position;
        foreach (var n in nodes)
            if (n != a && n != b && Poly.SegDist(n.transform.position, p, q) < NodeRadius * .9f) return true;
        foreach (var l in links)
        {
            if (l.OutOfNetwork) continue;   // chest and heat edges have no collision
            if (l.from == a || l.from == b || l.to == a || l.to == b) continue;
            if (Poly.SegmentsCross(p, q, l.from.transform.position, l.to.transform.position)) return true;
        }
        return false;
    }

    public bool EdgeRunsThrough(Vector2 p) { return EdgeThrough(p, null); }

    // would a building at p sit on top of an existing edge (ignoring the edges of the building being moved)
    bool EdgeThrough(Vector2 p, Node ignore)
    {
        foreach (var l in links)
        {
            if (l.OutOfNetwork) continue;
            if (ignore != null && (l.from == ignore || l.to == ignore)) continue;
            if (Poly.SegDist(p, l.from.transform.position, l.to.transform.position) < NodeRadius * .85f) return true;
        }
        return false;
    }

    public bool CanLink(Node a, Node b) { return a != b && InRange(a, b) && !EdgeBlocked(a, b); }

    // the sensible direction: Miners feed Refineries and Markets, Refineries feed Markets, Markets take from everybody
    static bool Feeds(NodeDef from, NodeDef to)
    {
        var f = from.behaviour;
        var t = to.behaviour;
        bool sink = to.IsSink;
        bool oven = t == NodeBehaviour.Oven;   // takes fuel from Miners and sand from Crushers
        bool buyer = t == NodeBehaviour.Seller || t == NodeBehaviour.Quest || t == NodeBehaviour.Storage;
        return (f == NodeBehaviour.Extractor && (t == NodeBehaviour.Processor || sink || oven)) || (f == NodeBehaviour.Processor && (sink || oven)) || (f == NodeBehaviour.Oven && buyer);
    }

    // links a new building to the nearest suitable ones in range (at most 3 in and 3 out), in the sensible direction
    void AutoLink(Node n)
    {
        var near = nodes.FindAll(o => o != n && CanLink(n, o) && !Adjacent(n, o));
        near.Sort((x, y) => Vector2.Distance(x.transform.position, n.transform.position).CompareTo(Vector2.Distance(y.transform.position, n.transform.position)));
        int outCount = 0, inCount = 0;
        foreach (var o in near)
        {
            if (Feeds(n.def, o.def) && outCount < 3 && GetLink(n, o) == null && !PathExists(o, n)) { AddLink(n, o, false); outCount++; }
            else if (Feeds(o.def, n.def) && inCount < 3 && GetLink(o, n) == null && !PathExists(n, o)) { AddLink(o, n, false); inCount++; }
        }
    }

    // two same-type buildings sitting edge to edge (snapped)
    public static bool Adjacent(Node a, Node b)
    {
        return a != b && CanSnap(a.def, b.def) && HexNorm((Vector2)a.transform.position - (Vector2)b.transform.position) <= Tile.Step * 1.02f;
    }

    // same drag again removes the link; refused: self, out of a Market or Quest node, anything closing a loop.
    // Snapped neighbours are the exception: they may always be linked in both directions.
    public void Toggle(Node a, Node b)
    {
        var existing = GetLink(a, b);
        if (existing != null) { Remove(existing); return; }
        if (a == b || a.def.IsSink) return;
        if (!InRange(a, b)) return;
        if (Adjacent(a, b))
        {
            var reverse = GetLink(b, a);
            if (reverse != null) Remove(reverse);
            AddLink(a, b, true);
            AddLink(b, a, true);
            return;
        }
        if (EdgeBlocked(a, b) || PathExists(b, a)) return;
        AddLink(a, b, false);
    }

    // triggers the node and the buildings of its own kind sitting directly next to it (snapped); linked ones are not affected
    public void Trigger(Node n)
    {
        n.Trigger();
        foreach (var o in nodes) if (Adjacent(n, o)) o.Trigger();
    }

    // removes every edge into and out of the node; keepSnapped leaves the links to snapped neighbours of its own kind
    public void ClearEdges(Node n, bool keepSnapped)
    {
        foreach (var l in links.ToArray())
        {
            if (l.from != n && l.to != n) continue;
            if (keepSnapped && Adjacent(n, l.from == n ? l.to : l.from)) continue;
            Remove(l);
        }
    }

    public void Remove(Link l)
    {
        graphVersion++;
        if (l.Special && !l.OutOfNetwork) state.cash += .5 * EdgeCostAt(l.def, EdgeOwned(l.def) - 1);   // half of what the latest one cost
        links.Remove(l);
        if (l.IsChest) l.from.chest = null;
        else if (!l.IsHeat) l.from.outs.Remove(l.to);
        l.Clear();
    }

    // ---- picking ----
    public Node NodeAt(Vector2 p)
    {
        foreach (var n in nodes) if (n.Hit(p)) return n;
        return null;
    }

    public Link LinkAt(Vector2 p, float tol)
    {
        foreach (var l in links) if (!l.IsChest && l.DistanceTo(p) < tol) return l;
        return null;
    }

    // the edge whose badge is at this point: clicking it opens its settings
    public Link BadgeAt(Vector2 p)
    {
        foreach (var l in links) if (l.OnBadge(p)) return l;
        return null;
    }
}
