using System.Collections.Generic;
using UnityEngine;

// One hexagon of the map. Its type (def) decides its colour, surface and what can be built on it.
[ExecuteAlways]
public class Tile : MonoBehaviour
{
    public const float Radius = .9f;
    public static readonly float Step = Radius * 1.7320508f;   // distance between the centres of two touching tiles

    public TileDef def;
    [System.NonSerialized] public bool starter;   // made with a limited "starter unique" conversion
    [System.NonSerialized] public bool locked;    // dark with a padlock until unlocked

    GameObject vis;
    int sig;

    // distance from the map centre in tile steps (hexagon rings); whole numbers at tile centres
    public static float RingF(Vector2 p)
    {
        float r = p.y / (Step * .866f);
        float q = p.x / Step - r * .5f;
        return Mathf.Max(Mathf.Abs(q), Mathf.Abs(r), Mathf.Abs(q + r));
    }

    void OnEnable() { Rebuild(); }

    void OnDisable()
    {
        if (vis == null) return;
        if (Application.isPlaying) Destroy(vis); else DestroyImmediate(vis);
        vis = null;
        sig = 0;
    }

    void Update() { if (!Application.isPlaying) Rebuild(); }

    // negative inside the hexagon (pointy-top) around the centre
    public static float HexDist(Vector2 d, float radius)
    {
        float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y);
        return Mathf.Max(ax, ax * .5f + ay * .866f) - radius * .866f;
    }

    public bool Contains(Vector2 p) { return HexDist(p - (Vector2)transform.position, Radius) <= 0f; }

    public void Rebuild()
    {
        if (def == null) return;
        Vector2 pos = transform.position;
        int s = def.GetInstanceID() * 31 + Mathf.RoundToInt(pos.x * 100) * 17 + Mathf.RoundToInt(pos.y * 100) + (starter ? 7 : 0) + (locked ? 13 : 0);
        if (vis != null && s == sig) return;
        sig = s;
        if (vis != null) { if (Application.isPlaying) Destroy(vis); else DestroyImmediate(vis); }

        vis = new GameObject("TileVisuals " + name);
        vis.transform.position = pos;
        var t = vis.transform;
        Gfx.Hex(t, "Shadow", Radius, new Color(0, 0, 0, .25f), -5).transform.localPosition = new Vector3(.06f, -.16f, 0);
        Gfx.Hex(t, "Body", Radius, def.fill, -4);
        Gfx.Hex(t, "Top", Radius * .86f, Color.Lerp(def.fill, Color.white, .12f), -3);
        var edge = Gfx.Line(t, "Edge", starter ? new Color(1f, .8f, .3f) : def.edge, starter ? .12f : .07f, -2, false);
        Gfx.HexLoop(edge, Radius);

        var rnd = new System.Random(Mathf.RoundToInt(pos.x * 10) * 31 + Mathf.RoundToInt(pos.y * 10));
        int count = def.decor == TileDecor.Tufts ? 8 : 5;
        var placed = new List<Vector2>();
        for (int tries = 0; placed.Count < count && tries < 200; tries++)
        {
            var p = new Vector2((float)(rnd.NextDouble() * 2 - 1), (float)(rnd.NextDouble() * 2 - 1)) * Radius * .8f;
            if (HexDist(p, Radius) > -.2f) continue;
            bool close = false;
            foreach (var q in placed) if (Vector2.Distance(p, q) < (def.decor == TileDecor.Tufts ? .25f : .4f)) close = true;
            if (close) continue;
            placed.Add(p);
            Vector3 at = pos + p;
            if (def.decor == TileDecor.Tree)
            {
                Gfx.Disc(t, "Tree", .36f, new Color(.1f, .38f, .17f), 1).transform.position = at;
                Gfx.Disc(t, "Tree Top", .25f, new Color(.25f, .62f, .3f), 2).transform.position = at + new Vector3(-.03f, .04f, 0);
            }
            else if (def.decor == TileDecor.Rock)
            {
                // a jagged little crag: a dark triangle with a lighter one on top, each turned a different way
                float turn = (float)rnd.NextDouble() * 60f - 30f;
                Gfx.Triangle(t, "Rock", .2f, turn, new Color(.5f, .55f, .63f), 1).transform.position = at;
                Gfx.Triangle(t, "Rock Light", .09f, turn, new Color(.76f, .81f, .88f), 2).transform.position = at + new Vector3(-.01f, .04f, 0);
            }
            else if (def.decor == TileDecor.Ripples)
            {
                // a short wavy line of lighter water
                var ripple = Gfx.Line(t, "Ripple", Color.Lerp(def.fill, Color.white, .55f), .06f, 1, true);
                ripple.positionCount = 4;
                for (int k = 0; k < 4; k++) ripple.SetPosition(k, at + new Vector3(-.21f + k * .14f, (k % 2 == 0 ? .04f : -.04f), 0));
            }
            else Gfx.Disc(t, "Tuft", .09f, Color.Lerp(def.fill, new Color(.1f, .4f, .15f), .45f), 1).transform.position = at;
        }

        if (locked)
        {
            // a closed tile is the real tile under a dark, slightly see-through cover with a padlock, so a stone tile can already be spotted
            Gfx.Hex(t, "Cover", Radius, new Color(.02f, .025f, .04f, .66f), 3);
            Gfx.HexLoop(Gfx.Line(t, "Cover Edge", new Color(.16f, .18f, .24f, .9f), .05f, 3, false), Radius);
            var lockSprite = PixelArt.Named("lock", .5f);
            if (lockSprite != null)
            {
                var sr = new GameObject("Lock").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(t, false);
                sr.sprite = lockSprite;
                sr.sortingOrder = 4;
                sr.color = new Color(1f, 1f, 1f, .92f);
            }
            else Gfx.Text(t, "Question", new Vector2(0, 0), .75f, new Color(1f, 1f, 1f, .9f), 4).text = "?";
        }

        foreach (var tr in vis.GetComponentsInChildren<Transform>(true)) tr.gameObject.hideFlags = HideFlags.HideAndDontSave;
    }
}
