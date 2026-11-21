using System.Collections.Generic;
using UnityEngine;

class Packet
{
    public ItemDef item;
    public SpriteRenderer sr;
    public float baseScale;     // 1 for pixel icons, smaller for the plain dot used when an item has no icon
    public float t;             // 0..1 along the edge
    public float age;           // seconds since it left
    public float phase;         // so items do not all bob in step
    public bool relay;          // already in flight: only passed on from another node
    public bool absorb;         // a receiver takes it, so it shrinks into the node at the end
}

// One directed connection with the items travelling on it. Drawn like a little conveyor: a cream track with a brown outline and
// brown chevrons that run along it. Items ride from the centre of one building to the centre of the next, above the buildings:
// they pop out of the producer, bob along, and shrink into a receiver. Items only passed on by a node in between keep their size.
public class Link : MonoBehaviour
{
    public Node from, to;

    // ---- special edges (null def = a normal edge that carries everything) ----
    public EdgeDef def;
    public int tier;                                     // Tier edge: the tier
    public bool exact;                                   // Tier edge: exactly this tier; otherwise every tier up to it
    public readonly List<ItemDef> items = new List<ItemDef>();   // Filter edge: the listed items
    public bool deny = true;                             // Filter edge: listed items are blocked (true) or the only ones let through (false)
    Transform badge;

    public bool IsChest { get { return def != null && def.kind == EdgeKind.Chest; } }
    public bool IsHeat { get { return def != null && def.kind == EdgeKind.Heat; } }
    public bool OutOfNetwork { get { return IsChest || IsHeat; } }   // these edges are not routes for items
    public bool HasBadge { get { return Special && !IsHeat; } }
    public bool Special { get { return def != null && def.kind != EdgeKind.Open; } }
    public Vector2 Mid { get { return (ca + cb) / 2; } }

    // may this item travel over the edge
    public bool Passes(ItemDef item)
    {
        if (def == null) return true;
        switch (def.kind)
        {
            case EdgeKind.Tier: return exact ? item.tier == tier : item.tier <= tier;
            case EdgeKind.Filter: return items.Contains(item) != deny;
            case EdgeKind.Chest: return false;   // chest and heat edges are not part of the network
            case EdgeKind.Heat: return false;
            default: return true;
        }
    }

    // the small badge in the middle of a special edge shows its setting, and is what you click to change it
    public bool OnBadge(Vector2 p) { return HasBadge && Vector2.Distance(p, Mid) < .4f; }

    public void Changed()
    {
        RefreshBadge();
        if (World.Instance != null) World.Instance.RoutesChanged();
    }

    void RefreshBadge()
    {
        if (!HasBadge) return;
        if (badge != null) Destroy(badge.gameObject);
        badge = new GameObject("Badge").transform;
        badge.SetParent(transform, false);
        Color rim = Color.Lerp(Brown, def.color, .35f);
        if (def.kind == EdgeKind.Filter) rim = deny ? new Color(.75f, .3f, .25f) : new Color(.25f, .6f, .3f);
        float k = length < 1.2f ? .65f : 1f;   // a short edge would be hidden by a full-size badge
        Gfx.Hex(badge, "Badge Rim", .36f * k, rim, Layers.Clasp + 2).transform.position = Mid;
        Gfx.Hex(badge, "Badge Body", .29f * k, Color.Lerp(Cream, def.color, .5f), Layers.Clasp + 3).transform.position = Mid;

        string text = null;
        if (def.kind == EdgeKind.Tier) text = exact || tier == 0 ? tier.ToString() : "0-" + tier;
        else if (def.kind == EdgeKind.Chest) text = "C";
        else if (items.Count == 0) text = deny ? "all" : "none";
        if (text != null)
        {
            var t = Gfx.Text(badge, "Badge Text", Mid, (text.Length > 2 ? .2f : .3f) * k, Brown, Layers.Clasp + 4);
            t.fontStyle = FontStyle.Bold;
            t.text = text;
            return;
        }
        // a filter shows the items it names (up to three)
        int n = Mathf.Min(3, items.Count);
        for (int i = 0; i < n; i++)
        {
            var s = PixelArt.ForItem(items[i], .2f);
            if (s == null) continue;
            var sr = new GameObject("Badge Item").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(badge, false);
            sr.transform.position = Mid + new Vector2((i - (n - 1) / 2f) * .21f, 0);
            sr.sprite = s;
            sr.sortingOrder = Layers.Clasp + 4;
        }
    }
    readonly List<Packet> packets = new List<Packet>();
    float length;
    Vector2 a, b;        // the visible track, hexagon edge to hexagon edge
    Vector2 ca, cb;      // centre to centre: the way the items travel
    float travelLength;

    static readonly Color Brown = new Color(.36f, .26f, .22f);
    static readonly Color Cream = new Color(1f, .95f, .85f);
    const float ChevronSpacing = .5f, ItemSize = .33f, PopTime = .18f, AbsorbTime = .16f;

    // items are pooled: sprites are reused instead of created for every packet
    static readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
    static Transform itemRoot;

    // paired = one half of a two-way link: the two directions run side by side so both show
    public void Init(Node f, Node t, bool paired, EdgeDef d = null)
    {
        def = d;
        from = f;
        to = t;
        ca = f.transform.position;
        cb = t.transform.position;
        Vector2 dir = (cb - ca).normalized, side = new Vector2(-dir.y, dir.x);
        travelLength = Vector2.Distance(ca, cb);
        float w = paired ? .72f : 1f;
        Vector2 la = ca, lb = cb;
        if (paired) { la += side * .15f; lb += side * .15f; }

        // the track runs from one hexagon's edge to the other's
        float trim = World.NodeRadius * .866f / World.HexNorm(dir) + .03f;
        a = la + dir * trim;
        b = lb - dir * trim;
        if (Vector2.Dot(b - a, dir) < .05f) a = b = (la + lb) / 2;
        length = Vector2.Distance(a, b);
        RefreshBadge();
        Color trackColor = Special ? Color.Lerp(Cream, def.color, .55f) : Cream, flowColor = Special ? Color.Lerp(Brown, def.color, .25f) : Brown;

        if (length < .4f)
        {
            // buildings touching: snapped neighbours share their border, so a small joint on the seam is all that is needed
            if (paired) { if (f.GetInstanceID() < t.GetInstanceID()) Clasp((ca + cb) / 2); }
            else Chevron((ca + cb) / 2, dir, side);
            return;
        }

        Track("Outline", Brown, .3f * w, Layers.LinkOutline, null);
        Track("Track", trackColor, .17f * w, Layers.LinkTrack, null);
        var flow = Track("Flow", flowColor, .17f * w, Layers.LinkFlow, Gfx.EdgeMat);
        flow.textureMode = LineTextureMode.Tile;
        flow.textureScale = new Vector2(1f / ChevronSpacing, 1f);

        foreach (var p in new[] { a, b })
        {
            Gfx.Disc(transform, "Port", .34f * w, Brown, Layers.LinkPort).transform.position = p;
            Gfx.Disc(transform, "Port Inner", .2f * w, Cream, Layers.LinkPort + 1).transform.position = p;
        }
    }

    LineRenderer Track(string name, Color c, float width, int order, Material mat)
    {
        var lr = Gfx.Line(transform, name, c, width, order, true);
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.numCapVertices = 0;
        if (mat != null) lr.sharedMaterial = mat;
        return lr;
    }

    // one small hexagon bolted across the seam of two snapped buildings
    void Clasp(Vector2 at)
    {
        Gfx.Hex(transform, "Clasp", .17f, Brown, Layers.Clasp).transform.position = at;
        Gfx.Hex(transform, "Clasp Inner", .1f, Cream, Layers.Clasp + 1).transform.position = at;
    }

    // a one-way edge between buildings that touch: a bold arrowhead
    void Chevron(Vector2 at, Vector2 dir, Vector2 side)
    {
        foreach (var layer in new[] { (Cream, .26f, Layers.Clasp), (Brown, .12f, Layers.Clasp + 1) })
        {
            var lr = Gfx.Line(transform, "Arrow", layer.Item1, layer.Item2, layer.Item3, true);
            lr.positionCount = 3;
            lr.SetPosition(0, at - dir * .2f + side * .16f);
            lr.SetPosition(1, at + dir * .14f);
            lr.SetPosition(2, at - dir * .2f - side * .16f);
        }
    }

    // ---- travelling items ----
    SpriteRenderer TakeSprite(ItemDef item, out float baseScale)
    {
        if (itemRoot == null) itemRoot = new GameObject("Items").transform;
        SpriteRenderer sr = null;
        while (pool.Count > 0 && sr == null) sr = pool.Pop();   // a pooled sprite may have been destroyed with the scene
        if (sr == null)
        {
            sr = new GameObject("Item").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(itemRoot, false);
            sr.sortingOrder = Layers.Items;
        }
        var sprite = PixelArt.ForItem(item, ItemSize);
        baseScale = sprite != null ? 1f : ItemSize * .7f;
        sr.sprite = sprite != null ? sprite : Gfx.Circle;
        sr.color = sprite != null ? Color.white : item.color;
        sr.gameObject.SetActive(true);
        return sr;
    }

    public void Launch(ItemDef item, bool relay)
    {
        var p = new Packet { item = item, relay = relay, absorb = to.Accepts(item), phase = Random.value * 6.28f };
        p.sr = TakeSprite(item, out p.baseScale);
        p.sr.transform.position = ca;
        p.sr.transform.localScale = Vector3.one * (relay ? p.baseScale : 0f);
        packets.Add(p);
    }

    static float OutBack(float x)
    {
        const float c1 = 2.2f, c3 = c1 + 1f;
        float y = x - 1f;
        return 1f + c3 * y * y * y + c1 * y * y;
    }

    public void Tick(float dt)
    {
        float travel = Mathf.Max(travelLength, 1f) / World.PacketSpeed;   // seconds for the whole way
        for (int i = packets.Count - 1; i >= 0; i--)
        {
            var p = packets[i];
            p.age += dt;
            p.t += dt / travel;
            if (p.t >= 1f)
            {
                to.Receive(p.item, from, p.absorb);
                Release(p);
                packets.RemoveAt(i);
                continue;
            }

            // an item leaving a producer speeds up, one entering a receiver slows down, one passing through keeps its pace
            float u = p.t;
            if (!p.relay) u = Mathf.Lerp(u, u * u, .55f);
            if (p.absorb) u = Mathf.Lerp(u, 1f - (1f - u) * (1f - u), .55f);

            Vector2 pos = Vector2.Lerp(ca, cb, u);
            pos.y += Mathf.Sin(p.age * 13f + p.phase) * .03f;
            var tr = p.sr.transform;
            tr.position = pos;
            tr.rotation = Quaternion.Euler(0, 0, Mathf.Sin(p.age * 9f + p.phase) * 9f);

            float s = p.relay ? 1f : OutBack(Mathf.Clamp01(p.age / PopTime));
            if (p.absorb) s *= Mathf.Clamp01((1f - p.t) * travel / AbsorbTime);
            tr.localScale = Vector3.one * (p.baseScale * s);
        }
    }

    static void Release(Packet p)
    {
        if (p.sr == null) return;
        p.sr.gameObject.SetActive(false);
        pool.Push(p.sr);
    }

    public float DistanceTo(Vector2 p) { return Poly.SegDist(p, a, b); }

    public void Clear()
    {
        foreach (var p in packets)
        {
            to.Lost(p.item);
            Release(p);
        }
        packets.Clear();
        Destroy(gameObject);
    }
}
