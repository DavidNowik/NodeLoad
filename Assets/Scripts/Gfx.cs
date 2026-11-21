using UnityEngine;

public static class Gfx
{
    static Sprite circle;
    static Material lineMat;
    static Font font;

    public static Sprite Circle
    {
        get
        {
            if (circle == null)
            {
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                float r = n / 2f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(r, r));
                        px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(r - d));
                    }
                tex.SetPixels(px);
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), n);
            }
            return circle;
        }
    }

    public static Material LineMat
    {
        get
        {
            if (lineMat == null) lineMat = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            return lineMat;
        }
    }

    static Material edgeMat;

    // chevrons that point along a line, for the edges; shared by all of them and scrolled together so they all flow the same way
    public static Material EdgeMat
    {
        get
        {
            if (edgeMat == null)
            {
                const int W = 64, H = 32;
                var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
                var px = new Color32[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float u = (x + .5f) / W, v = (y + .5f) / H;
                        float arm = .72f - Mathf.Abs(v - .5f) * .8f;   // the centre line of a ">"
                        float a = Mathf.Clamp01(1f - (Mathf.Abs(u - arm) - .05f) / .04f);
                        px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                t.SetPixels32(px);
                t.Apply();
                edgeMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = t, hideFlags = HideFlags.HideAndDontSave };
            }
            return edgeMat;
        }
    }

    public static void ScrollEdges(float dt)
    {
        var m = EdgeMat;
        var o = m.mainTextureOffset;
        o.x -= dt * .9f;
        m.mainTextureOffset = o;
    }

    public static Font Font
    {
        get
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    public static SpriteRenderer Disc(Transform parent, string name, float diameter, Color c, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * diameter;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Circle;
        sr.color = c;
        sr.sortingOrder = order;
        return sr;
    }

    public static LineRenderer Line(Transform parent, string name, Color c, float width, int order, bool world)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = LineMat;
        lr.startColor = lr.endColor = c;
        lr.startWidth = lr.endWidth = width;
        lr.sortingOrder = order;
        lr.useWorldSpace = world;
        lr.numCapVertices = 3;
        lr.numCornerVertices = 2;
        lr.positionCount = 0;
        return lr;
    }

    // pointy-top hexagon, vertex 0 at the top, clockwise
    public static Vector3 HexVertex(float radius, int k)
    {
        float a = (90f - 60f * k) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0);
    }

    public static void HexLoop(LineRenderer lr, float radius)
    {
        lr.loop = true;
        lr.positionCount = 6;
        for (int k = 0; k < 6; k++) lr.SetPosition(k, HexVertex(radius, k));
    }

    // like Arc, but along the hexagon edge starting at the top vertex
    public static void HexArc(LineRenderer lr, float radius, float frac)
    {
        frac = Mathf.Clamp01(frac);
        if (frac <= 0) { lr.positionCount = 0; return; }
        float e = frac * 6f;
        int full = Mathf.FloorToInt(e);
        float rest = e - full;
        bool partial = rest > 1e-4f && full < 6;
        lr.positionCount = full + 1 + (partial ? 1 : 0);
        for (int i = 0; i <= full; i++) lr.SetPosition(i, HexVertex(radius, i % 6));
        if (partial) lr.SetPosition(full + 1, Vector3.Lerp(HexVertex(radius, full % 6), HexVertex(radius, (full + 1) % 6), rest));
    }

    // rounded rectangle outline, 28 points, counter-clockwise from the right
    public static Vector2[] RoundedRect(Vector2 half, float r)
    {
        var pts = new System.Collections.Generic.List<Vector2>();
        var centers = new[] { new Vector2(half.x - r, half.y - r), new Vector2(-half.x + r, half.y - r), new Vector2(-half.x + r, -half.y + r), new Vector2(half.x - r, -half.y + r) };
        for (int c = 0; c < 4; c++)
            for (int i = 0; i <= 6; i++)
            {
                float a = (c * 90f + i * 15f) * Mathf.Deg2Rad;
                pts.Add(centers[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
        return pts.ToArray();
    }

    // vertex colours are read as linear, so convert them to look like the colour you picked
    public static Color Vc(Color c)
    {
        return QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
    }

    public static void Tint(Mesh m, Color c)
    {
        var cols = new Color[m.vertexCount];
        for (int i = 0; i < cols.Length; i++) cols[i] = Vc(c);
        m.colors = cols;
    }

    public static Mesh HexMesh(Transform parent, string name, float radius, Color c, int order)
    {
        return Hex(parent, name, radius, c, order).GetComponent<MeshFilter>().sharedMesh;
    }

    // a pointy-top hexagon filled from the bottom up to `level` (0..1) of its height; an empty level clears the mesh
    public static void HexFill(Mesh m, float radius, float level, Color c)
    {
        m.Clear();
        if (level <= .001f) return;
        float yMax = -radius + 2 * radius * Mathf.Clamp01(level);
        var poly = new System.Collections.Generic.List<Vector2>();
        for (int k = 0; k < 6; k++)
        {
            Vector2 a = HexVertex(radius, k), b = HexVertex(radius, (k + 1) % 6);
            bool ain = a.y <= yMax, bin = b.y <= yMax;
            if (ain) poly.Add(a);
            if (ain != bin) poly.Add(a + (b - a) * ((yMax - a.y) / (b.y - a.y)));   // where the edge crosses the level line
        }
        if (poly.Count < 3) return;
        var verts = new Vector3[poly.Count];
        var cols = new Color[poly.Count];
        var tris = new int[(poly.Count - 2) * 3];
        var vc = Vc(c);
        for (int i = 0; i < poly.Count; i++) { verts[i] = poly[i]; cols[i] = vc; }
        for (int i = 0; i < poly.Count - 2; i++) { tris[i * 3] = 0; tris[i * 3 + 1] = i + 1; tris[i * 3 + 2] = i + 2; }
        m.vertices = verts;
        m.colors = cols;
        m.triangles = tris;
    }


    // a filled triangle around its own centre (radius = distance to the corners), turned by `degrees`; the caller sets its position
    public static GameObject Triangle(Transform parent, string name, float radius, float degrees, Color c, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var verts = new Vector3[3];
        for (int k = 0; k < 3; k++)
        {
            float a = (degrees + 90f + k * 120f) * Mathf.Deg2Rad;
            verts[k] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius;
        }
        var col = Vc(c);
        var mesh = new Mesh { vertices = verts, colors = new[] { col, col, col }, triangles = new[] { 0, 1, 2 } };
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = LineMat;
        mr.sortingOrder = order;
        return go;
    }
    public static GameObject Hex(Transform parent, string name, float radius, Color c, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var verts = new Vector3[7];
        var cols = new Color[7];
        var tris = new int[18];
        cols[0] = Vc(c);
        for (int k = 0; k < 6; k++)
        {
            verts[k + 1] = HexVertex(radius, k);
            cols[k + 1] = Vc(c);
            tris[k * 3] = 0;
            tris[k * 3 + 1] = k + 1;
            tris[k * 3 + 2] = (k + 1) % 6 + 1;
        }
        var mesh = new Mesh { vertices = verts, colors = cols, triangles = tris };
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = LineMat;
        mr.sortingOrder = order;
        return go;
    }

    public static TextMesh Text(Transform parent, string name, Vector2 pos, float height, Color c, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var tm = go.AddComponent<TextMesh>();
        tm.font = Font;
        tm.fontSize = 64;
        tm.characterSize = height / 6.4f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = c;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = Font.material;
        mr.sortingOrder = order;
        return tm;
    }
}
