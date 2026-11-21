using System.Collections.Generic;
using UnityEngine;

// One building. Behaviour comes from its NodeDef, so Miner, Refinery, Market and Quest node are the same class.
public partial class Node : MonoBehaviour   // the inspection window's rows are in NodeInspect.cs
{
    [System.NonSerialized] public NodeDef def;
    [System.NonSerialized] public World world;
    public readonly List<Node> outs = new List<Node>();

    readonly Dictionary<ItemDef, int> queue = new Dictionary<ItemDef, int>();
    readonly Dictionary<ItemDef, int> inflight = new Dictionary<ItemDef, int>();
    readonly Dictionary<ItemDef, int> held = new Dictionary<ItemDef, int>();
    readonly Dictionary<ItemDef, Node> lastFrom = new Dictionary<ItemDef, Node>();   // who last sent this item type here
    RecipeDef recipe;           // a Refinery has none until the first item it receives decides it (LockRecipe)
    int phase;
    float t;
    bool blocked;
    int rr;
    float pulse, burst;

    TileDef ground;   // the tile a Miner overlaps, which decides what it mines
    Mesh body;
    LineRenderer prog, smelt, burstLine;   // smelt: an Oven's inner ring
    float progR;                            // radius of the progress ring
    TextMesh center, nameText, statusText;
    LineRenderer outer;         // the outer outline, which flashes red when a Refinery's storage is full
    Mesh fillMesh;              // a Refinery's body fills up like a bar with what it has stored
    Transform fillTicks;        // one line per batch of the recipe's first input
    float fillShown = -1;
    SpriteRenderer icon;        // the item a Miner or Quest node handles
    Transform recipeRow;        // a Refinery shows its recipe as pictures
    RecipeDef rowRecipe;
    ItemDef shownItem;

    static readonly Color Collect = new Color(.55f, .78f, 1f);
    static readonly Color Work = new Color(.45f, .88f, .55f);
    static readonly Color Block = new Color(1f, .76f, .45f);
    static readonly Color Full = new Color(.92f, .3f, .24f);
    static readonly Color Edge = new Color(.36f, .26f, .22f);
    static readonly Color Cream = new Color(1f, .95f, .85f);
    static readonly Color Gold = new Color(1f, .84f, .28f);
    static readonly Color GoldLight = new Color(1f, .93f, .55f);
    static readonly Color GoldDark = new Color(.8f, .56f, .12f);
    static readonly Color Violet = new Color(.82f, .72f, 1f);

    // as big as a map tile (radius 0.9); snapped neighbours therefore touch and share their border like a honeycomb
    const float HexOut = World.NodeRadius, HexIn = .66f, HexMid = .78f, BandW = .18f;
    static readonly Color Ink = new Color(.18f, .11f, .08f);     // text on the pale body: near-black brown
    static readonly Color Soft = new Color(.3f, .21f, .17f);
    const float TintStrength = .7f;                               // how far a body colour is pushed toward cream, so the text stays readable
    const float IconY = .2f, NameY = -.2f, StatusY = -.4f;
    const float RowY = .22f, RowNameY = -.15f, RowStatusY = -.38f;
    readonly Dictionary<ItemDef, int> produced = new Dictionary<ItemDef, int>();
    ItemDef tintItem;

    bool Seller { get { return def.behaviour == NodeBehaviour.Seller; } }
    bool Refinery { get { return def.behaviour == NodeBehaviour.Processor; } }
    bool Storage { get { return def.behaviour == NodeBehaviour.Storage; } }
    bool Picks { get { return Refinery && def.anyRecipe; } }   // a Crusher: takes the input of any recipe and picks the recipe per batch
    bool Crafts { get { return Refinery || Oven; } }           // nodes that work a recipe and send its output on

    // ---- Oven and Steam Turbine (Build Nodes: they work once built) ----
    bool Oven { get { return def.behaviour == NodeBehaviour.Oven; } }
    bool Turbine { get { return def.behaviour == NodeBehaviour.Turbine; } }
    public bool IsOven { get { return Oven && !Building; } }
    public bool IsTurbine { get { return Turbine && !Building; } }

    public float speed = 1f;     // work speed: 1, or more while a Steam Turbine in reach is working (set by World.ApplyHeat)
    float heat;                  // Oven: its meter, in burn weight; Turbine: the heat reaching it from its Ovens
    float steamLeft;             // Turbine: seconds of steam left from the last water it consumed
    LineRenderer reachRing;      // Turbine: how far its boost reaches

    public float Heat { get { return IsOven || IsTurbine ? heat : 0f; } }

    // how full the heat meter counts as, 0 to 1: from def.fullFrom up it is simply full (an Oven flickering between 99 and 100 looks and boosts the same)
    public float HeatFraction { get { float p = Mathf.Clamp01(heat / def.maxHeat); return p >= def.fullFrom ? 1f : p; } }

    // the speed multiplier a working Turbine gives: 1 without heat or water, def.maxBoost at full heat
    public float Boost { get { return IsTurbine && steamLeft > 0 && heat > 0 ? 1f + (def.maxBoost - 1f) * HeatFraction : 1f; } }

    // Miners, Refineries and Storages (their chest edge) can be sped up
    public bool Boostable { get { return def.behaviour == NodeBehaviour.Extractor || def.behaviour == NodeBehaviour.Processor || IsChest; } }

    // Items are burnt at once, but only while they fit into the meter without being wasted; the rest waits (up to itemSlots). The meter burns down by itself.
    void OvenTick(float dt)
    {
        heat = Mathf.Max(0f, heat - def.burnRate * dt);
        while (true)
        {
            ItemDef best = null;
            foreach (var kv in queue)
                if (kv.Value > 0 && kv.Key.burnWeight > 0 && heat + kv.Key.burnWeight <= def.maxHeat && (best == null || kv.Key.burnWeight > best.burnWeight)) best = kv.Key;
            if (best == null) break;
            Add(queue, best, -1);
            heat += best.burnWeight;
            pulse = Mathf.Max(pulse, .15f);
        }
    }

    // The heat comes from the Ovens linked to it. One water is consumed for every 1/feedRate seconds of work, and only while there is heat.
    void TurbineTick(float dt)
    {
        heat = world.HeatInto(this);
        // snapped Turbines share their water: it moves to a neighbour that holds at least two less
        var w = def.feedItem;
        if (w != null)
            foreach (var o in outs)
                if (o.IsTurbine && Get(queue, w) >= Get(o.queue, w) + 2) { Add(queue, w, -1); Add(o.queue, w, 1); }
        if (heat > 0 && steamLeft <= 0 && def.feedItem != null && Get(queue, def.feedItem) > 0)
        {
            Add(queue, def.feedItem, -1);
            steamLeft = 1f / def.feedRate;
        }
        if (heat > 0 && steamLeft > 0)
        {
            steamLeft -= dt;
            world.DrainHeat(this, def.heatDraw * dt);   // working eats heat from the Ovens, on top of what they burn down by themselves
        }
    }

    // an Oven gives up heat to a Turbine; returns how much it could give
    public float TakeHeat(float amount)
    {
        float taken = Mathf.Min(amount, heat);
        heat -= taken;
        return taken;
    }

    // The one item a Storage holds: the first item it receives decides it, like a Refinery's recipe. Null until then.
    ItemDef stored;
    ItemDef candidate;                                   // the first item to reach an empty Storage; it waits for the player's answer
    readonly HashSet<ItemDef> refused = new HashSet<ItemDef>();   // items the player said no to: this Storage never takes them
    bool asking;

    // "Store this item here?" Yes: it is what this Storage holds from now on. No: it is never stored here, and the Storage waits for another item and asks again.
    // while the question is open this Storage marks itself with a pulsing ring, so the player sees which chest is meant
    LineRenderer askRing;

    void ShowAskRing()
    {
        if (!asking)
        {
            if (askRing != null) askRing.enabled = false;
            return;
        }
        if (askRing == null)
        {
            askRing = Gfx.Line(transform, "Ask Ring", Gold, .14f, Layers.NodeBurst + 1, false);
            Gfx.HexLoop(askRing, HexOut + .2f);
        }
        float pulse01 = .5f + .5f * Mathf.Sin(Time.unscaledTime * 6f);
        askRing.enabled = true;
        askRing.startColor = askRing.endColor = Color.Lerp(Gold, Color.white, pulse01);
        askRing.transform.localScale = Vector3.one * (1f + .06f * pulse01);
    }


    // a working Steam Turbine draws the area its boost reaches as a faint orange circle
    void ShowReach()
    {
        if (!IsTurbine)
        {
            if (reachRing != null) reachRing.enabled = false;
            return;
        }
        if (reachRing == null)
        {
            reachRing = Gfx.Line(transform, "Reach", new Color(1f, .55f, .2f, .5f), .06f, Layers.NodeBody - 1, false);
            reachRing.loop = true;
            reachRing.positionCount = 72;
            for (int i = 0; i < 72; i++)
            {
                float a = i * Mathf.PI * 2 / 72;
                reachRing.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * def.effectRadius);
            }
        }
        reachRing.enabled = true;
        float alpha = steamLeft > 0 ? .55f : .22f;
        reachRing.startColor = reachRing.endColor = new Color(1f, .55f, .2f, alpha);
    }
    void TryAsk()
    {
        var hud = world.hud;
        if (hud == null || hud.DialogOpen) return;   // one question at a time; try again next tick
        asking = true;
        var item = candidate;
        hud.Ask("Store " + item.name + " here?\n(No: never this item)",
            () => { asking = false; stored = item; candidate = null; world.RoutesChanged(); },
            () =>
            {
                asking = false;
                refused.Add(item);
                Add(queue, item, -Get(queue, item));   // what already arrived is thrown away
                candidate = null;
                world.RoutesChanged();
            }, transform.position);
    }


    // ---- chest edge: a built Storage hands its item to exactly one building, over an edge of any length ----
    public Link chest;                                   // the one active chest edge out of this Storage
    readonly Dictionary<ItemDef, int> pinned = new Dictionary<ItemDef, int>();   // items delivered over a chest edge: this node keeps them, never passes them on
    float chestBudget;

    public bool IsChest { get { return Storage && !Building; } }

    // sends up to the asset's rate per second, but only what the target can use itself and has room for
    void ChestTick(float dt)
    {
        if (chest == null || stored == null) { chestBudget = 0; return; }
        var target = chest.to;
        chestBudget = Mathf.Min(chestBudget + def.outputRate * dt, Mathf.Max(1f, def.outputRate * .25f));
        while (chestBudget >= 1f && Get(queue, stored) > 0 && target.Accepts(stored) && target.Load(stored) < target.CapacityFor(stored))
        {
            chestBudget -= 1f;
            Add(queue, stored, -1);
            Send(target, stored, false);
        }
    }

    // how many of an item the node can hold: a Storage as many as its asset says, everything else the usual queue size
    int Capacity { get { return Storage ? def.capacity : Oven || Turbine ? def.itemSlots : World.QueueCap; } }

    // ---- Build Nodes: a node whose def has a buildCost works only after the materials have been delivered to it ----
    readonly Dictionary<ItemDef, int> progress = new Dictionary<ItemDef, int>();   // what has been delivered so far
    bool built;
    Transform buildRow;          // the needed items as pictures with "have/need" under each
    TextMesh[] buildCounts;
    static readonly Color Blueprint = new Color(.74f, .85f, 1f), BlueprintLine = new Color(.25f, .5f, .95f), Done = new Color(.04f, .45f, .16f);

    bool Building { get { return def.buildCost != null && def.buildCost.Length > 0 && !built; } }

    int NeedOf(ItemDef item)
    {
        if (def.buildCost != null) foreach (var i in def.buildCost) if (i.item == item) return i.count;
        return 0;
    }

    // how many of this item the node takes: while building exactly what is still missing, otherwise its storage size
    // The Quest node counts everything the moment it arrives, so it never holds a queue: it refuses nothing it wants, however much is on its way.
    // (A limit of 12 on the way would cap a long edge to about 13 items per second and block the Miners behind it.)
    // An Oven has itemSlots places for ALL its fuel together (10 wood, or 4 wood and 6 planks), not 10 per kind.
    int CapacityFor(ItemDef item)
    {
        if (Building) return NeedOf(item);
        if (def.behaviour == NodeBehaviour.Quest) return int.MaxValue;
        if (Oven && item.burnWeight > 0)
        {
            int others = 0;
            foreach (var kv in queue) if (kv.Key != item && kv.Key.burnWeight > 0) others += Load(kv.Key);
            foreach (var kv in inflight) if (kv.Key != item && kv.Key.burnWeight > 0 && !queue.ContainsKey(kv.Key)) others += Load(kv.Key);
            return Mathf.Max(0, def.itemSlots - others);
        }
        return Capacity;
    }

    int BuildTotal { get { int s = 0; foreach (var i in def.buildCost) s += i.count; return s; } }
    int BuildHave { get { int s = 0; foreach (var i in def.buildCost) s += Mathf.Min(i.count, Get(progress, i.item)); return s; } }

    // a delivered item goes straight into the construction; the last one finishes it
    void Construct(ItemDef item)
    {
        Add(inflight, item, -1);
        Add(progress, item, 1);
        pulse = Mathf.Max(pulse, .18f);
        if (BuildHave < BuildTotal) return;
        built = true;
        pulse = .3f;
        burst = .6f;
        if (buildRow != null) Destroy(buildRow.gameObject);
        buildRow = null;
        buildCounts = null;
        Gfx.Tint(body, Oven ? new Color(1f, .86f, .72f) : Turbine ? new Color(.8f, .9f, 1f) : Cream);
        world.RoutesChanged();
        FloatText.Spawn(transform.position + Vector3.up * 1f, "Built!", new Color(.7f, 1f, .75f));
    }

    // [item] [item] with "have/need" under each: the green number shows a finished part
    void UpdateBuildRow()
    {
        var need = def.buildCost;
        if (buildRow == null)
        {
            buildRow = new GameObject("Build Needs").transform;
            buildRow.SetParent(transform, false);
            buildRow.localPosition = new Vector3(0, RowY, 0);
            buildCounts = new TextMesh[need.Length];
            for (int i = 0; i < need.Length; i++)
            {
                float x = (i - (need.Length - 1) / 2f) * .52f;
                ItemPicture(buildRow, need[i].item, .38f, new Vector2(x, .08f));
                buildCounts[i] = Mark(buildRow, "", new Vector2(x, -.16f), .17f);
            }
        }
        for (int i = 0; i < need.Length; i++)
        {
            int have = Get(progress, need[i].item);
            string s = have + "/" + need[i].count;
            if (buildCounts[i].text != s) buildCounts[i].text = s;
            buildCounts[i].color = have >= need[i].count ? Done : Ink;
        }
    }

    // the order in which receivers are offered an item: Refineries (and nodes only relaying it) first, then Storage, then the Quest node, then Markets
    int Rank { get { return Building ? 0 : Seller ? 3 : def.behaviour == NodeBehaviour.Quest ? 2 : Storage ? 1 : 0; } }

    public void Init(World w, NodeDef d, TileDef g)
    {
        world = w;
        def = d;
        ground = g;
        if (Oven && d.recipes != null && d.recipes.Length > 0) recipe = d.recipes[0];   // an Oven has its one recipe from the start

        // every node is the same cute hexagon: body, two outlines, and a coloured band between them that is the progress slider
        body = Gfx.HexMesh(transform, "Body", HexIn, Cream, Layers.NodeBody);
        // an Oven splits the band in two thin rings: the outer one is its heat, the inner one how far along it is smelting
        float ringW = Oven ? BandW / 2 : BandW;
        progR = Oven ? HexMid + ringW / 2 : HexMid;
        var trackColor = Seller ? GoldDark : new Color(.93f, .86f, .74f);
        Gfx.HexArc(Gfx.Line(transform, "Slider Track", trackColor, ringW, Layers.NodeBand, false), progR, 1f);
        prog = Gfx.Line(transform, "Slider", Work, ringW, Layers.NodeSlider, false);
        if (Oven)
        {
            Gfx.HexArc(Gfx.Line(transform, "Smelt Track", trackColor, ringW, Layers.NodeBand, false), HexMid - ringW / 2, 1f);
            smelt = Gfx.Line(transform, "Smelt Slider", Work, ringW, Layers.NodeSlider, false);
        }
        outer = Gfx.Line(transform, "Outline Outer", Edge, .07f, Layers.NodeOutline, false);
        Gfx.HexLoop(outer, HexOut);
        Gfx.HexLoop(Gfx.Line(transform, "Outline Inner", Edge, .07f, Layers.NodeOutline, false), HexIn);
        burstLine = Gfx.Line(transform, "Burst", Work, .09f, Layers.NodeBurst, false);
        Gfx.HexLoop(burstLine, HexOut);
        burstLine.enabled = false;
        if (Seller) Gfx.Tint(body, Gold);
        else if (def.behaviour == NodeBehaviour.Quest) Gfx.Tint(body, Violet);
        else if (Oven) Gfx.Tint(body, new Color(1f, .86f, .72f));
        else if (Turbine) Gfx.Tint(body, new Color(.8f, .9f, 1f));
        else if (def.behaviour == NodeBehaviour.Extractor && g != null && g.yields != null) Gfx.Tint(body, Color.Lerp(g.yields.color, Cream, TintStrength));
        if (Building) Gfx.Tint(body, Blueprint);   // blueprint blue until it is built

        if (Seller)
        {
            // a coin: a raised lighter face inside the rim, with the euro sign pressed into it
            Gfx.Hex(transform, "Face", HexIn * .8f, GoldLight, Layers.NodeBand);
            Gfx.HexLoop(Gfx.Line(transform, "Face Rim", GoldDark, .05f, Layers.NodeBand, false), HexIn * .8f);
        }

        icon = new GameObject("Icon").AddComponent<SpriteRenderer>();
        icon.transform.SetParent(transform, false);
        icon.transform.localPosition = new Vector3(0, IconY, 0);
        icon.sortingOrder = Layers.NodeIcon;
        icon.enabled = false;

        // all text sits inside the hexagon
        if (Refinery)
        {
            // the body is also a bar: it fills from the bottom with the items stored, in the colour of the item
            var fill = new GameObject("Fill");
            fill.transform.SetParent(transform, false);
            fillMesh = new Mesh();
            fill.AddComponent<MeshFilter>().sharedMesh = fillMesh;
            var mr = fill.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Gfx.LineMat;
            mr.sortingOrder = Layers.NodeBand;
        }
        float nameY = Refinery ? RowNameY : NameY, statusY = Refinery ? RowStatusY : StatusY;
        center = Gfx.Text(transform, "Center", new Vector2(0, IconY), Seller ? .6f : .2f, Ink, Layers.NodeText + 1);
        nameText = Gfx.Text(transform, "Name", new Vector2(0, nameY), .16f, Ink, Layers.NodeText + 1);
        statusText = Gfx.Text(transform, "Status", new Vector2(0, statusY), .13f, Soft, Layers.NodeText + 1);
        center.fontStyle = nameText.fontStyle = statusText.fontStyle = FontStyle.Bold;
        if (Seller)
        {
            // the euro sign pressed into the coin: a darker copy just below and right of it
            var shadow = Gfx.Text(transform, "Center Shadow", new Vector2(.025f, IconY - .025f), .6f, GoldDark, Layers.NodeText);
            shadow.fontStyle = FontStyle.Bold;
            shadow.text = "€";
            Set(center, "€", Ink);
        }
    }

    public bool Hit(Vector2 p) { return Vector2.Distance(transform.position, p) < World.NodeRadius + .1f; }

    // ---- data helpers ----
    static int Get(Dictionary<ItemDef, int> d, ItemDef i) { return d.TryGetValue(i, out var v) ? v : 0; }
    static void Add(Dictionary<ItemDef, int> d, ItemDef i, int n) { d[i] = Get(d, i) + n; }
    static int Total(Dictionary<ItemDef, int> d) { int s = 0; foreach (var kv in d) s += kv.Value; return s; }

    float CycleTime { get { return Crafts ? recipe.time : def.cycleTime; } }
    ItemDef Output { get { return def.behaviour == NodeBehaviour.Extractor ? ground.yields : Crafts ? (recipe != null ? recipe.output : null) : null; } }
    string Verb { get { return def.behaviour == NodeBehaviour.Extractor ? ground.verb : Crafts ? (recipe != null ? recipe.verb : def.displayName) : "Selling"; } }

    // The recipes this Refinery could still take an item for: only its own once it has one, otherwise all of them (a Crusher always takes all).
    IEnumerable<RecipeDef> ActiveRecipes()
    {
        if (recipe != null && !def.anyRecipe) yield return recipe;
        else foreach (var r in def.recipes) yield return r;
    }

    // A Refinery with a recipe takes only that recipe's inputs; everything else passes through it like through a relay.
    public bool Accepts(ItemDef item)
    {
        if (Seller) return true;
        if (Building) return NeedOf(item) > 0;
        if (Storage) return stored != null ? stored == item : !refused.Contains(item) && (candidate == null || candidate == item);
        if (Oven)
        {
            if (item.burnWeight > 0) return true;   // fuel
            foreach (var r in ActiveRecipes()) foreach (var i in r.inputs) if (i.item == item) return true;
            return false;
        }
        if (Turbine) return item == def.feedItem;
        if (def.behaviour == NodeBehaviour.Quest) return world.Wants(item);
        if (def.behaviour != NodeBehaviour.Processor) return false;
        foreach (var r in ActiveRecipes()) foreach (var i in r.inputs) if (i.item == item) return true;
        return false;
    }

    // the first item a Refinery receives decides its recipe for good (until it is cut loose, see ResetChoice)
    void LockRecipe(ItemDef item)
    {
        if (def.anyRecipe) return;
        foreach (var r in def.recipes)
            foreach (var i in r.inputs)
                if (i.item == item) { recipe = r; world.RoutesChanged(); return; }
    }

    // cutting the edges of a Refinery or Storage while it is empty frees it again, so the next first input decides anew
    public void ResetChoice()
    {
        if (!(Refinery && recipe != null) && !(Storage && (stored != null || refused.Count > 0))) return;
        if (Total(queue) + Total(held) + Total(inflight) > 0) return;
        recipe = null;
        stored = null;
        candidate = null;
        refused.Clear();
        world.RoutesChanged();
        FloatText.Spawn(transform.position + Vector3.up * 1f, "free again", new Color(.85f, .9f, 1f));
    }

    // how many of an item the node keeps waiting for itself: one batch. Anything above that is surplus, which a Refinery shares
    // with the Refineries next to it (see Route); items it cannot use at all are passed on as a relay.
    int Keep(ItemDef item)
    {
        if (Oven && item.burnWeight > 0) return Capacity;   // fuel waits for the meter, it is never passed on
        if (!Crafts) return 0;
        int m = 0;
        foreach (var r in ActiveRecipes()) foreach (var i in r.inputs) if (i.item == item) m = Mathf.Max(m, i.count);
        return m;
    }

    // everything of this item the node holds or has coming: waiting, on its way, and the batch being worked on
    int Load(ItemDef item) { return Get(queue, item) + Get(inflight, item) + Get(held, item) + (Building ? Get(progress, item) : 0); }

    // can it take one more of this item: it has room itself, or the receivers linked to it do (see PoolHasRoom)
    public bool CanTake(ItemDef item) { return Load(item) < CapacityFor(item) || PoolHasRoom(item); }

    // A full receiver can still take more if the receivers linked to it (snapped neighbours, or ones you linked) have room:
    // it passes its surplus on to them (Forward). So the pool of directly connected receivers counts as one store.
    bool PoolHasRoom(ItemDef item)
    {
        var seen = new HashSet<Node> { this };
        var todo = new Queue<Node>();
        todo.Enqueue(this);
        while (todo.Count > 0)
        {
            var cur = todo.Dequeue();
            foreach (var o in cur.outs)
            {
                if (!world.LinkPasses(cur, o, item) || !seen.Add(o) || o.Seller || !o.Accepts(item)) continue;
                if (o.Load(item) < o.CapacityFor(item)) return true;
                todo.Enqueue(o);
            }
        }
        return false;
    }

    // Items travel over the edges to a receiver (a Refinery that takes the item, a Market). Nodes on the way only pass them on:
    // each step goes to a neighbour that is one edge closer to a receiver, so items always arrive and never circle.
    // exclude: the node the item came from. share: a receiver passing its surplus to a neighbouring receiver; that only goes to
    // another Refinery-like receiver (never a Market) that holds at least two items less, so the load evens out and nothing bounces back.
    Node Route(ItemDef item, bool commit, Node exclude, bool share = false)
    {
        var dist = world.DistMap(item);
        int mine;
        if (!dist.TryGetValue(this, out mine)) return null;   // no receiver reachable from here
        int want = mine == 0 ? 0 : mine - 1;
        int n = outs.Count;
        // in order of Rank: a Refinery that can use it, then a Storage, then the Quest node, a Market last; each only gets what the ones before cannot take (they are full)
        for (int pass = 0; pass < (share ? 1 : 4); pass++)
            for (int j = 0; j < n; j++)
            {
                int idx = (rr + j) % n;
                var d = outs[idx];
                if (d == exclude || d.Rank != pass || !world.LinkPasses(this, d, item)) continue;
                if (share && d.Load(item) + 1 >= Load(item)) continue;
                int dd;
                if (!dist.TryGetValue(d, out dd) || dd != want) continue;
                if (d.Load(item) < d.CapacityFor(item) || (!share && d.PoolHasRoom(item)))
                {
                    if (commit) rr = idx + 1;
                    return d;
                }
            }
        return null;
    }

    // Markets, Quest nodes and Storages only receive
    bool Sink { get { return def.IsSink; } }

    bool CanSend() { return Sink || (Output != null && Route(Output, false, null) != null); }

    // relay: the item was only passed on from another node, so it is already in flight and does not pop out again
    void Send(Node to, ItemDef item, bool relay)
    {
        Add(to.inflight, item, 1);
        var link = chest != null && chest.to == to ? chest : world.GetLink(this, to);   // GetLink only knows the network edges, not the chest edge
        link.Launch(item, relay);
    }

    public void Receive(ItemDef item, Node from, bool absorbed)
    {
        if (Building) { Construct(item); return; }
        if (from != null && from.chest != null && from.chest.to == this) Add(pinned, item, 1);   // sent over a chest edge: stays here
        Add(inflight, item, -1);
        Add(queue, item, 1);
        lastFrom[item] = from;
        if (Refinery && recipe == null) LockRecipe(item);
        if (Storage && stored == null)
        {
            // an empty Storage asks about the first item (see TryAsk); items it said no to, or that come while it asks about another, are thrown away
            if (refused.Contains(item) || (candidate != null && candidate != item)) { Add(queue, item, -1); return; }
            if (candidate == null) { candidate = item; world.RoutesChanged(); }
        }
        if (absorbed) pulse = Mathf.Max(pulse, .18f);   // a little nudge when something is delivered
    }

    public void Lost(ItemDef item) { Add(inflight, item, -1); }

    // ---- production ----
    bool Ready()
    {
        if (def.behaviour == NodeBehaviour.Extractor) return true;

        if (Picks && recipe == null)
        {
            // a Crusher starts the first recipe it has all inputs for
            foreach (var r in def.recipes)
            {
                bool all = true;
                foreach (var i in r.inputs) if (Get(queue, i.item) < i.count) all = false;
                if (all) { recipe = r; break; }
            }
        }
        if (recipe == null) return false;
        bool ok = true;
        foreach (var i in recipe.inputs)
        {
            int take = Mathf.Min(i.count - Get(held, i.item), Get(queue, i.item));
            if (take > 0) { Add(held, i.item, take); Add(queue, i.item, -take); }
            if (Get(pinned, i.item) > Get(queue, i.item)) pinned[i.item] = Get(queue, i.item);
            if (Get(held, i.item) < i.count) ok = false;
        }
        return ok;
    }

    void Pop()
    {
        pulse = .3f;
        burst = .6f;
        var o = Output;
        int extra = Crafts ? recipe.outputCount - 1 : 0;   // a batch that makes several: one leaves now, the rest wait in the queue and are passed on like relayed items
        held.Clear();
        Add(produced, o, 1 + extra);
        if (Picks) recipe = null;   // the next batch picks its recipe again
        Send(Route(o, true, null), o, false);
        if (extra > 0) Add(queue, o, extra);
    }

    public void Relocate(Vector2 p, TileDef g)
    {
        transform.position = p;
        SetGround(g);
    }

    // the tile under the building changed (it moved, or the background tile was converted): a Miner mines what its tile yields
    public void SetGround(TileDef g)
    {
        ground = g;
        if (def.behaviour == NodeBehaviour.Extractor && g != null && g.yields != null) Gfx.Tint(body, Color.Lerp(g.yields.color, Cream, TintStrength));
    }

    // a quick click makes a miner produce one item right now, or a refinery finish its current batch early (the normal cycle goes on)
    public void Trigger()
    {
        if (Building || (Oven && heat <= 0)) return;
        if (Picks && phase == 0) Ready();   // a Crusher picks its recipe first
        if (Sink || blocked || !CanSend()) return;
        if (phase == 0 && !Ready()) return;
        t = 0; phase = 0;
        Pop();
    }

    // ---- selling ----
    // A Market has no timer: everything that arrives is sold at once. It only earns up to its income cap per second;
    // what is over goes to another connected Market that still has room, or is lost.
    float income;   // money left in this second's budget; may go negative after a big item, which pays itself back over time

    public bool HasRoom { get { return income >= 0; } }
    public float IncomeCap { get { return Meta.MarketCap(def.incomeCap); } }
    public float IncomeBudget { get { return IncomeCap * Mathf.Max(1f, def.capWindow); } }   // what can be earned within one window: unused seconds are credited up to this

    float lostMoney, nextLostShow;   // money thrown away because the cap was reached, shown as one red number every half second

    void SellTick(float dt)
    {
        income = Mathf.Min(IncomeBudget, income + IncomeCap * dt);
        foreach (var item in new List<ItemDef>(queue.Keys))
            while (Get(queue, item) > 0)
            {
                Add(queue, item, -1);
                var target = HasRoom ? this : world.OtherSellerWithRoom(this);
                if (target != null) target.SellOne(item);
                else lostMoney += Meta.SellPrice(item);   // no Market with room: the item and its money are lost
            }
        if (lostMoney > 0 && Time.time >= nextLostShow)
        {
            FloatText.Spawn(transform.position + Vector3.up * 1.3f, "-€" + NumFmt.Short(lostMoney), new Color(1f, .25f, .2f));
            lostMoney = 0;
            nextLostShow = Time.time + .5f;
        }
    }

    // everything that arrives counts toward the level goal at once
    void QuestTick()
    {
        var goal = world.Goal;
        if (goal == null) return;
        foreach (var g in goal.goals)
        {
            int n = Get(queue, g.item);
            if (n == 0) continue;
            Add(queue, g.item, -n);
            pulse = .3f;
            burst = .6f;
            world.Deliver(g.item, n);
            if (world.Goal != goal) return;   // that was the last one and the level is over
        }
    }

    // ---- the Quest node shows every goal as a picture with "delivered/needed" under it ----
    Transform goalRow;
    TextMesh[] goalCounts;
    LevelDef goalFor;

    void UpdateGoalRow()
    {
        var goal = world.Goal;
        if (goal == null || goal.goals == null) return;
        int n = goal.goals.Length;
        if (goalRow == null || goalFor != goal)
        {
            if (goalRow != null) Destroy(goalRow.gameObject);
            goalFor = goal;
            goalRow = new GameObject("Goal Needs").transform;
            goalRow.SetParent(transform, false);
            goalRow.localPosition = new Vector3(0, RowY, 0);
            goalCounts = new TextMesh[n];
            for (int i = 0; i < n; i++)
            {
                float x = (i - (n - 1) / 2f) * (n > 1 ? .6f : .52f);
                ItemPicture(goalRow, goal.goals[i].item, .38f, new Vector2(x, .08f));
                goalCounts[i] = Mark(goalRow, "", new Vector2(x, -.16f), n > 1 ? .13f : .17f);
            }
        }
        for (int i = 0; i < n; i++)
        {
            int have = world.state.DeliveredOf(goal.goals[i].item.name);
            string s = have + "/" + goal.goals[i].amount;
            if (goalCounts[i].text != s) goalCounts[i].text = s;
            goalCounts[i].color = have >= goal.goals[i].amount ? Done : Ink;
        }
    }

    void SellOne(ItemDef item)
    {
        income -= Meta.SellPrice(item);
        world.Sale(item);
        pulse = .3f;
        burst = .6f;
        FloatText.Spawn(transform.position + Vector3.up * 1.0f, "+€" + NumFmt.Short(Meta.SellPrice(item)), new Color(1f, .9f, .55f));
    }

    ItemDef MainItem()
    {
        ItemDef best = null;
        int n = -1;
        foreach (var kv in produced) if (kv.Value > n) { n = kv.Value; best = kv.Key; }
        return best != null ? best : Output;
    }

    void Forward()
    {
        foreach (var item in new List<ItemDef>(queue.Keys))
        {
            while (Get(queue, item) > Keep(item) && Get(queue, item) > Get(pinned, item))   // items a chest edge delivered stay here
            {
                Node from;
                lastFrom.TryGetValue(item, out from);
                var d = Route(item, true, from, Keep(item) > 0);
                if (d == null) break;
                Add(queue, item, -1);
                Send(d, item, true);
            }
        }
    }

    bool relayFirst;   // alternates every tick, so a node that both produces and relays shares a busy outlet fairly

    public void Tick(float dt)
    {
        if (Sink)
        {
            if (Seller) SellTick(dt);
            else if (def.behaviour == NodeBehaviour.Quest) QuestTick();
            else if (IsTurbine) TurbineTick(dt);
            else if (IsChest)
            {
                if (stored == null && candidate != null && !asking) TryAsk();
                ChestTick(dt * speed);   // a Storage keeps what arrives and hands it on over its chest edge
            }
            pulse = Mathf.Max(0, pulse - dt);
            burst = Mathf.Max(0, burst - dt);
            Refresh();
            return;
        }
        if (Building)
        {
            pulse = Mathf.Max(0, pulse - dt);
            burst = Mathf.Max(0, burst - dt);
            Refresh();
            return;
        }
        if (Oven) OvenTick(dt);
        relayFirst = !relayFirst;
        if (relayFirst) Forward();
        float rem = dt * speed;   // a Steam Turbine in reach makes Miners and Refineries work faster
        if (Oven && heat <= 0) rem = 0;   // an Oven works its recipe only while it has heat
        for (int g = 0; g < 40; g++)
        {
            if (blocked)
            {
                if (!CanSend()) break;
                blocked = false; t = 0; phase = 0;
                Pop();
                continue;
            }
            if (phase == 0)
            {
                if (Ready()) { phase = 1; t = 0; } else break;
            }
            float time = CycleTime, need = time - t;
            if (rem >= need)
            {
                rem -= need;
                if (!CanSend()) { t = time; blocked = true; continue; }
                t = 0; phase = 0;
                Pop();
            }
            else { t += rem; break; }
        }
        if (!relayFirst) Forward();
        pulse = Mathf.Max(0, pulse - dt);
        burst = Mathf.Max(0, burst - dt);
        Refresh();
    }

    // ---- view ----
    static void Set(TextMesh tm, string s, Color c)
    {
        if (tm.text != s) tm.text = s;
        tm.color = c;
    }

    // the picture in the middle: a Miner or Quest node shows its item, a Refinery its recipe
    void RefreshArt()
    {
        if (Building) UpdateBuildRow();
        if (def.behaviour == NodeBehaviour.Quest) UpdateGoalRow();
        ItemDef want = def.behaviour == NodeBehaviour.Extractor ? Output
            : Storage ? (stored != null ? stored : candidate) : IsTurbine ? def.feedItem : null;
        if (want != shownItem)
        {
            shownItem = want;
            var s = PixelArt.ForItem(want, .5f);
            icon.sprite = s;
            icon.enabled = s != null;
        }
        if (IsOven && icon.sprite == null)
        {
            icon.sprite = PixelArt.Named("fire", .5f);   // a finished Oven shows a flame
            icon.enabled = icon.sprite != null;
        }
        if (Refinery && (Picks ? recipeRow == null : recipe != rowRecipe || recipeRow == null)) BuildRecipeRow();
    }

    // [input x2]  ->  [output], all as little pictures
    void BuildRecipeRow()
    {
        rowRecipe = recipe;
        if (recipeRow != null) Destroy(recipeRow.gameObject);
        recipeRow = new GameObject("Recipe").transform;
        recipeRow.SetParent(transform, false);
        recipeRow.localPosition = new Vector3(0, RowY, 0);
        if (fillTicks != null) Destroy(fillTicks.gameObject);
        fillShown = -1;
        if (Picks)
        {
            // [input] / [input] -> [output]: every input of its recipes is welcome
            var all = def.recipes;
            const float pic = .3f, outPic = .46f;
            float width = all.Length * pic + (all.Length - 1) * .14f + .5f + outPic;
            float px = -width / 2;
            for (int i = 0; i < all.Length; i++)
            {
                if (i > 0) { Mark(recipeRow, "/", new Vector2(px + .07f, 0), .2f); px += .14f; }
                ItemPicture(recipeRow, all[i].inputs[0].item, pic, new Vector2(px + pic / 2, 0));
                px += pic;
            }
            px += .08f;
            Mark(recipeRow, "→", new Vector2(px + .1f, 0), .24f);
            px += .2f + .08f;
            ItemPicture(recipeRow, all[0].output, outPic, new Vector2(px + outPic / 2, 0));
            if (all[0].outputCount > 1) Mark(recipeRow, all[0].outputCount.ToString(), new Vector2(px + outPic - .02f, -.14f), .17f).GetComponent<MeshRenderer>().sortingOrder = Layers.NodeText + 2;
            return;
        }
        if (recipe == null)
        {
            // no recipe yet: whatever arrives first decides it
            Mark(recipeRow, "? → ?", Vector2.zero, .3f);
            return;
        }

        const float inSize = .33f, outSize = .5f, gap = .08f, arrowW = .2f;
        float total = 0;
        for (int i = 0; i < recipe.inputs.Length; i++) total += inSize + (i > 0 ? .14f : 0);
        total += arrowW + outSize + gap * 2;

        float x = -total / 2;
        for (int i = 0; i < recipe.inputs.Length; i++)
        {
            if (i > 0) { Mark(recipeRow, "+", new Vector2(x + .07f, 0), .2f); x += .14f; }
            var inp = recipe.inputs[i];
            ItemPicture(recipeRow, inp.item, inSize, new Vector2(x + inSize / 2, 0));
            if (inp.count > 1)
            {
                var n = Mark(recipeRow, inp.count.ToString(), new Vector2(x + inSize - .02f, -.14f), .17f);
                n.GetComponent<MeshRenderer>().sortingOrder = Layers.NodeText + 2;
            }
            x += inSize;
        }
        x += gap;
        Mark(recipeRow, "→", new Vector2(x + arrowW / 2, 0), .24f);
        x += arrowW + gap;
        ItemPicture(recipeRow, recipe.output, outSize, new Vector2(x + outSize / 2, 0));
        if (recipe.outputCount > 1) Mark(recipeRow, recipe.outputCount.ToString(), new Vector2(x + outSize - .02f, -.14f), .17f).GetComponent<MeshRenderer>().sortingOrder = Layers.NodeText + 2;

        // a thin line across the body for every full batch of the first input, so the level shows how many batches are stored
        if (fillTicks != null) Destroy(fillTicks.gameObject);
        fillTicks = new GameObject("Fill Ticks").transform;
        fillTicks.SetParent(transform, false);
        int need = Mathf.Max(1, recipe.inputs[0].count);
        for (int k = need; k < World.QueueCap; k += need)
        {
            float y = -HexIn + 2 * HexIn * k / World.QueueCap;
            float half = Mathf.Abs(y) <= HexIn / 2 ? HexIn * .866f : (HexIn - Mathf.Abs(y)) * 1.732f;
            var tick = Gfx.Line(fillTicks, "Tick", Cream, .025f, Layers.NodeSlider, false);
            tick.positionCount = 2;
            tick.SetPosition(0, new Vector3(-half + .04f, y, 0));
            tick.SetPosition(1, new Vector3(half - .04f, y, 0));
        }
        fillShown = -1;
    }

    // what the Refinery holds of its first input: the batch being worked on plus what waits in storage
    int Stock
    {
        get
        {
            if (Storage) return stored == null ? 0 : Get(queue, stored);
            if (Picks) { int s = 0; foreach (var r in def.recipes) s += Get(queue, r.inputs[0].item) + Get(held, r.inputs[0].item); return s; }
            return recipe == null ? 0 : Get(queue, recipe.inputs[0].item) + Get(held, recipe.inputs[0].item);
        }
    }

    bool StorageFull
    {
        get
        {
            if (Storage) return stored != null && Load(stored) >= Capacity;
            if (Picks) { foreach (var r in def.recipes) if (Load(r.inputs[0].item) >= World.QueueCap) return true; return false; }
            return Refinery && recipe != null && Load(recipe.inputs[0].item) >= World.QueueCap;
        }
    }

    // the item the bar is coloured by: the recipe's first input, for a Crusher the one it holds most of
    ItemDef FillItem()
    {
        if (!Picks) return recipe.inputs[0].item;
        ItemDef best = def.recipes[0].inputs[0].item;
        foreach (var r in def.recipes) if (Load(r.inputs[0].item) > Load(best)) best = r.inputs[0].item;
        return best;
    }

    void RefreshFill()
    {
        if (recipe == null && !Picks)
        {
            if (fillShown != 0) { fillShown = 0; Gfx.HexFill(fillMesh, HexIn, 0, Cream); }
            outer.startColor = outer.endColor = Edge;
            return;
        }
        bool full = StorageFull;
        float level = full ? 1f : Mathf.Clamp01((float)Stock / World.QueueCap);
        float flash = .5f + .5f * Mathf.Sin(Time.time * 9f);
        var item = FillItem();
        Color c = Color.Lerp(item.color, Cream, .25f);
        if (full) c = Color.Lerp(c, Full, .35f + .25f * flash);
        if (!Mathf.Approximately(level, fillShown) || full)
        {
            fillShown = level;
            Gfx.HexFill(fillMesh, HexIn, level, c);
        }
        // a full store is visible from far away: the whole outline flashes red
        outer.startColor = outer.endColor = full ? Color.Lerp(Edge, Full, flash) : Edge;
    }

    static TextMesh Mark(Transform parent, string s, Vector2 pos, float size)
    {
        var tm = Gfx.Text(parent, "Mark", pos, size, Ink, Layers.NodeText + 1);
        tm.fontStyle = FontStyle.Bold;
        tm.text = s;
        return tm;
    }

    // an item's pixel icon, or a coloured dot when it has none
    static void ItemPicture(Transform parent, ItemDef item, float size, Vector2 pos)
    {
        var sprite = PixelArt.ForItem(item, size);
        if (sprite != null)
        {
            var sr = new GameObject("Item").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localPosition = pos;
            sr.sprite = sprite;
            sr.sortingOrder = Layers.NodeIcon;
        }
        else Gfx.Disc(parent, "Item", size * .8f, item.color, Layers.NodeIcon).transform.localPosition = pos;
    }

    void Refresh()
    {
        float p = 0;
        Color c = blocked ? Block : phase == 0 ? Collect : Work;
        if (Seller)
        {
            // the band is the income meter: how much of this second's cap is being used
            float used = Mathf.Clamp01((IncomeBudget - income) / IncomeBudget);
            p = used;
            c = used < .6f ? Work : Color.Lerp(Work, Block, Mathf.InverseLerp(.6f, .9f, used));
            if (used > .9f) c = Color.Lerp(Block, Full, Mathf.InverseLerp(.9f, 1f, used));
        }
        else if (def.behaviour == NodeBehaviour.Quest)
        {
            p = world.QuestFraction;
            c = Work;
        }
        else if (Building)
        {
            // the band is the overall progress of the construction
            p = (float)BuildHave / BuildTotal;
            c = Collect;
            outer.startColor = outer.endColor = BlueprintLine;
        }
        else if (Storage)
        {
            // the band fills with what is stored; the whole outline flashes red when it is full
            p = Mathf.Clamp01((float)Stock / Capacity);
            float flash = .5f + .5f * Mathf.Sin(Time.time * 9f);
            c = StorageFull ? Color.Lerp(Work, Gold, .6f + .4f * flash) : Work;   // a full chest glows golden, not red
            outer.startColor = outer.endColor = StorageFull ? Color.Lerp(Edge, Gold, flash) : Edge;
        }
        else if (Oven)
        {
            // the band is the heat meter
            p = HeatFraction;
            c = Color.Lerp(new Color(1f, .78f, .3f), new Color(1f, .35f, .12f), p);
        }
        else if (Turbine)
        {
            p = HeatFraction;
            c = steamLeft > 0 ? new Color(1f, .5f, .15f) : Block;
        }
        if (speed > 1.01f && !Sink) c = Color.Lerp(c, new Color(1f, .5f, .15f), .6f);   // sped up by a Turbine: the band glows orange
        else if (phase == 1 && !Oven) p = t / CycleTime;   // an Oven's band stays its heat meter
        else if (Refinery && recipe != null)
        {
            int need = 0;
            foreach (var i in recipe.inputs) need += i.count;
            p = need > 0 ? (float)Total(held) / need : 0;
        }
        Gfx.HexArc(prog, progR, p);
        prog.startColor = prog.endColor = c;
        if (smelt != null)
        {
            Gfx.HexArc(smelt, HexMid - BandW / 4, phase == 1 ? t / CycleTime : 0f);
            smelt.startColor = smelt.endColor = blocked ? Block : Work;
        }
        if (Refinery && fillMesh != null) RefreshFill();

        transform.localScale = Vector3.one * (1f + .18f * pulse / .3f);
        burstLine.enabled = burst > 0;
        if (burst > 0)
        {
            float k = 1f - burst / .6f;
            burstLine.transform.localScale = Vector3.one * (1f + k * .9f);
            var bc = new Color(Work.r, Work.g, Work.b, 1f - k);
            burstLine.startColor = burstLine.endColor = bc;
        }

        // a Refinery's colour follows what it produced most; a Miner takes its colour from its tile
        if (Refinery)
        {
            var main = MainItem();
            if (main != tintItem)
            {
                tintItem = main;
                Gfx.Tint(body, main != null ? Color.Lerp(main.color, Cream, TintStrength) : Cream);
            }
        }
        if (Storage) ShowAskRing();
        ShowReach();
        if (Storage && stored != tintItem)
        {
            tintItem = stored;
            Gfx.Tint(body, stored != null ? Color.Lerp(stored.color, Cream, TintStrength) : Cream);
        }
        RefreshArt();
        if (!Seller) Set(center, shownItem != null && !icon.enabled ? shownItem.name : "", Ink);

        bool working = phase == 1 && !blocked;
        // a Quest node with a time limit shows the clock instead of its name
        bool clock = def.behaviour == NodeBehaviour.Quest && world.QuestTimeText != null;
        Set(nameText, clock ? world.QuestTimeText : working ? Verb + "..." : def.displayName, clock && world.questTimeLeft < 10 ? new Color(.8f, .1f, .05f) : working ? new Color(.04f, .36f, .14f) : Ink);

        string st = blocked ? (def.behaviour == NodeBehaviour.Extractor && outs.Count == 0 ? "no output link" : "blocked")
            : Seller ? "max €" + IncomeCap.ToString("0") + "/s"
            : def.behaviour == NodeBehaviour.Quest ? ""
            : Building ? "building " + BuildHave + "/" + BuildTotal
            : Oven ? "heat " + Mathf.RoundToInt(HeatFraction * def.maxHeat) + "/" + Mathf.RoundToInt(def.maxHeat) + "  " + Total(queue) + "/" + def.itemSlots
            : Turbine ? (steamLeft > 0 ? "×" + Boost.ToString("0.00") + "  water " + Get(queue, def.feedItem) : heat <= 0 ? "no heat" : "no water")
            : Storage ? (stored == null ? (candidate != null ? "asking..." : "waiting for an item") : (StorageFull ? "FULL " : "") + Stock + "/" + Capacity)
            : Refinery ? (recipe == null && !Picks ? "any first input" : (StorageFull ? "FULL " + World.QueueCap : Stock.ToString()) + "/" + World.QueueCap) : "";
        if (speed > 1.01f && !Sink) st = (st.Length > 0 ? st + "  " : "") + "×" + speed.ToString("0.0");   // sped up by a Turbine
        Set(statusText, st, blocked || (StorageFull && Refinery) ? new Color(.65f, .18f, .02f) : StorageFull ? new Color(.62f, .42f, .02f) : Soft);
    }
}
